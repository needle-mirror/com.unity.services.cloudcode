using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Internal.Apis.CloudCode;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Authentication.Internal;
using Unity.Services.Wire.Internal;
using UnityEngine;

namespace Unity.Services.CloudCode.Subscriptions
{
    /// <summary>
    /// Owns the lifecycle of Cloud Code push-message channels: one per mode (Player/Project), and one per
    /// <see cref="CloudCodeScope"/>. All subscribers of a channel share a single wire channel, guarded by a
    /// per-channel async gate that serializes subscribe and teardown so they never collide on it. Each
    /// subscriber gets its own <see cref="ISubscriptionEvents"/> whose callbacks the provider fans the
    /// shared channel's events into. The channel is torn down when the last subscriber leaves.
    /// </summary>
    class SubscriptionProvider
    {
        internal uint m_MaxUnsubscribeRetries = 3u;
        internal TimeSpan m_UnsubscribeRetryBaseDelay = TimeSpan.FromSeconds(0.5d);

        readonly IWire m_Wire;
        readonly ICloudCodeApiClient m_ApiClient;
        readonly ICloudProjectId m_CloudProjectId;
        readonly IPlayerId m_PlayerIdService;

        /// <summary>Keyed by a <see cref="TokenProvider.TokenProviderMode"/> or a <see cref="CloudCodeScope"/>.</summary>
        readonly Dictionary<object, ChannelGuard> m_ChannelGuards = new();

        internal int ChannelGuardCount => m_ChannelGuards.Count;

        /// <summary>
        /// Bumped on every player change. Captured before a subscribe's awaits and re-checked after, so a
        /// switch away and back to the same player - invisible to a plain id comparison - is still detected.
        /// </summary>
        int m_PlayerChangedCount;

        /// <summary>
        /// The per-channel state guarding one shared wire channel: the async <see cref="Gate"/> that
        /// serializes subscribe and teardown so they never overlap on the same server channel, the single
        /// <see cref="Channel"/> that all its subscribers share (null when none is up), and the
        /// <see cref="Subscribers"/> the channel's events are fanned out to. A guard is created on first use and
        /// removed once it has no channel, no subscribers and no caller holding or awaiting its gate, so scope keys
        /// do not accumulate and nobody is ever left waiting on a removed guard.
        /// </summary>
        sealed class ChannelGuard
        {
            internal ChannelGuard(object key) => Key = key;

            internal readonly object Key;
            internal int GateCallers;
#if UNITY_WEBGL && !UNITY_EDITOR
            internal readonly BinaryAsyncGate Gate = new();
#else
            internal readonly SemaphoreSlim Gate = new(1, 1);
#endif
            internal SubscriptionChannel Channel;
            internal readonly List<Subscriber> Subscribers = new();
        }

        public SubscriptionProvider(IWire wire, ICloudCodeApiClient apiClient, ICloudProjectId cloudProjectId,
                                    IPlayerId playerIdService)
        {
            m_Wire = wire;
            m_ApiClient = apiClient;
            m_CloudProjectId = cloudProjectId;
            m_PlayerIdService = playerIdService;
            // The player id service's lifetime is the same as the program's
            // one, so it "okay" to not unsubscribe OnPlayerChangedInvalidateAll
            m_PlayerIdService.PlayerIdChanged += OnPlayerChangedInvalidateAll;
        }

        /// <summary>Counts the caller as a user before it awaits the gate; pair with <see cref="ReleaseChannelGuard"/>.</summary>
        ChannelGuard AcquireChannelGuard(object key)
        {
            if (!m_ChannelGuards.TryGetValue(key, out var guard))
            {
                guard = new ChannelGuard(key);
                m_ChannelGuards[key] = guard;
            }
            guard.GateCallers++;
            return guard;
        }

        void ReleaseChannelGuard(ChannelGuard guard)
        {
            guard.GateCallers--;
            RemoveIfIdle(guard);
            guard.Gate.Release();
        }

        void RemoveIfIdle(ChannelGuard guard)
        {
            // A subscriber callback may already have replaced this guard under the same key.
            if (guard.GateCallers == 0 && guard.Channel == null && guard.Subscribers.Count == 0
                && m_ChannelGuards.TryGetValue(guard.Key, out var current) && ReferenceEquals(current, guard))
            {
                m_ChannelGuards.Remove(guard.Key);
            }
        }

        /// <summary>
        /// Subscribes a new subscriber to the shared channel for <paramref name="mode"/>, creating and
        /// subscribing the channel on the first subscriber or joining the existing one otherwise. The
        /// returned handle carries this subscriber's own callbacks and its own subscribe/unsubscribe.
        /// </summary>
        /// <param name="callbacks">The callbacks this subscriber receives channel events on; a new instance is used if null.</param>
        /// <param name="mode">Which channel to subscribe to (player-scoped or project-wide).</param>
        /// <returns>A task resolving to this subscriber's <see cref="ISubscriptionEvents"/> handle once subscribed.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the Wire SDK dependency is unavailable.</exception>
        /// <exception cref="CloudCodeException">Thrown when the channel subscription fails.</exception>
        public Task<ISubscriptionEvents> SubscribeAsync(SubscriptionEventCallbacks callbacks,
            TokenProvider.TokenProviderMode mode)
        {
            return SubscribeAsync(callbacks, mode, new TokenProvider(m_ApiClient, m_CloudProjectId, mode));
        }

        /// <summary>
        /// Subscribes to the shared channel of <paramref name="scope"/>. A joiner's
        /// <paramref name="tokenProvider"/> is unused: the channel keeps the one it was created with.
        /// </summary>
        /// <param name="callbacks">The callbacks this subscriber receives channel events on; a new instance is used if null.</param>
        /// <param name="scope">The scope whose channel to subscribe to.</param>
        /// <param name="tokenProvider">Fetches the scope channel's token when the channel is created.</param>
        /// <returns>A task resolving to this subscriber's <see cref="ISubscriptionEvents"/> handle once subscribed.</returns>
        public Task<ISubscriptionEvents> SubscribeAsync(SubscriptionEventCallbacks callbacks, CloudCodeScope scope,
            IChannelTokenProvider tokenProvider)
        {
            return SubscribeAsync(callbacks, (object)scope, tokenProvider);
        }

        async Task<ISubscriptionEvents> SubscribeAsync(SubscriptionEventCallbacks callbacks, object key,
            IChannelTokenProvider tokenProvider)
        {
            if (m_Wire == null)
            {
                throw new InvalidOperationException(
                    "Cannot subscribe to Cloud Code messages without the wire SDK dependency.");
            }

            /* TODO: Handle null instead of allocating a
             * default instance of SubscriptionEventCallbacks.
             */
            var subscriber = new Subscriber(this, key, tokenProvider, callbacks ?? new SubscriptionEventCallbacks());
            await RegisterAsync(subscriber);
            return subscriber;
        }

        async Task RegisterAsync(Subscriber subscriber)
        {
            var channelGuard = AcquireChannelGuard(subscriber.Key);
            await channelGuard.Gate.WaitAsync();
            try
            {
                // Always check against the current player before registering and drop if stale.
                if (channelGuard.Channel != null && HasPlayerChanged(channelGuard.Channel))
                {
                    DropChannelAndNotify(channelGuard);
                }

                if (channelGuard.Subscribers.Contains(subscriber))
                {
                    return;
                }
                channelGuard.Subscribers.Add(subscriber);

                var existingChannel = channelGuard.Channel != null;
                // Create the channel if none exists and notify.
                if (!existingChannel)
                {
                    try
                    {
                        channelGuard.Channel = await CreateChannelAsync(subscriber.TokenProvider, channelGuard);
                    }
                    catch
                    {
                        channelGuard.Subscribers.Remove(subscriber);
                        throw;
                    }
                }
                else if (channelGuard.Channel.LastKnownState.HasValue)
                {
                    // Replay the current state only for a joiner; the creator already saw it live via FanOut.
                    NotifySubscriber(subscriber, channelGuard.Channel.LastKnownState.Value);
                }
            }
            finally
            {
                ReleaseChannelGuard(channelGuard);
            }
        }

        bool HasPlayerChanged(SubscriptionChannel channel)
        {
            return channel.PlayerId != m_PlayerIdService.PlayerId;
        }

        void DropChannelAndNotify(ChannelGuard channelGuard)
        {
            var orphaned = channelGuard.Subscribers.ToArray();
            if (channelGuard.Channel != null)
            {
                channelGuard.Channel.IsRetired = true;
            }
            channelGuard.Channel = null;
            channelGuard.Subscribers.Clear();
            foreach (var subscriber in orphaned)
            {
                NotifySubscriber(subscriber, EventConnectionState.Unsubscribed);
            }
        }

        void NotifySubscriber(Subscriber subscriber, EventConnectionState state)
        {
            try
            {
                subscriber.Callbacks.InvokeEventConnectionStateChanged(state);
            }
            catch (Exception e)
            {
                // Isolate a throwing subscriber so the unsubscribe/join still completes.
                UnityEngine.Debug.LogException(e);
            }
        }

        async Task<SubscriptionChannel> CreateChannelAsync(IChannelTokenProvider tokenProvider, ChannelGuard state)
        {
            // The player and change-count this subscribe is for, captured before any await so a switch
            // mid-subscribe - including a switch away and back to the same player - is detected afterward.
            var intendedPlayer = m_PlayerIdService.PlayerId;
            var playerChangedCountPreSubscribe = m_PlayerChangedCount;

            var rawWireChannel = m_Wire.CreateChannel(tokenProvider);
            var upstreamCallbacks = new SubscriptionEventCallbacks();
            var subscription = new SubscriptionChannel(rawWireChannel, upstreamCallbacks);
            subscription.PlayerId = intendedPlayer;

            upstreamCallbacks.MessageReceived += message
                => FanOut(state, subscription, c => c.InvokeMessageReceived(message));
            upstreamCallbacks.MessageBytesReceived += bytes
                => FanOut(state, subscription, c => c.InvokeMessageBytesReceived(bytes));
            upstreamCallbacks.ConnectionStateChanged += connectionState
                => FanOut(state, subscription, c => c.InvokeEventConnectionStateChanged(connectionState));
            upstreamCallbacks.Error += error
                => FanOut(state, subscription, c => c.InvokeEventError(error));

            // A kick is terminal: wire removes the channel from its repository, so evict to match and let the
            // next subscribe rebuild.
            upstreamCallbacks.Kicked += () =>
            {
                FanOut(state, subscription, c => c.InvokeEventKicked());
                Evict(state, subscription);
            };

            try
            {
                await subscription.SubscribeAsync();
            }
            catch
            {
                subscription.IsRetired = true;
                throw;
            }

            // If any player change landed while this subscribe was in flight, the channel raced an intervening
            // Wire reset (even if the id ended up unchanged), so reject it.
            ThrowIfPlayerChangedDuringSubscribe(subscription, playerChangedCountPreSubscribe);
            return subscription;
        }

        void FanOut(ChannelGuard state, SubscriptionChannel source, Action<SubscriptionEventCallbacks> invoke)
        {
            // A retired channel (torn down / evicted) must not route to the guard's current subscribers.
            if (source.IsRetired)
            {
                return;
            }

            foreach (var subscriber in state.Subscribers.ToArray())
            {
                try
                {
                    invoke(subscriber.Callbacks);
                }
                catch (Exception e)
                {
                    // Isolate a throwing subscriber so the rest still receive the event.
                    UnityEngine.Debug.LogException(e);
                }
            }
        }

        void Evict(ChannelGuard state, SubscriptionChannel channel)
        {
            if (!ReferenceEquals(state.Channel, channel))
            {
                return;
            }
            channel.IsRetired = true;
            state.Channel = null;
            state.Subscribers.Clear();
            RemoveIfIdle(state);
        }

        void ThrowIfPlayerChangedDuringSubscribe(SubscriptionChannel channel, int playerChangedCountPreSubscribe)
        {
            if (m_PlayerChangedCount == playerChangedCountPreSubscribe)
            {
                return;
            }

            // A player change landed mid-subscribe, so Wire has since reset the connection out from under this
            // channel - retire it and best-effort clean it out.
            channel.IsRetired = true;
            _ = SafeUnsubscribeAsync(channel);
            throw new CloudCodeException(CloudCodeExceptionReason.SubscriptionError, 1337,
                "The signed-in player changed while subscribing to Cloud Code messages.", null);
        }

        void OnPlayerChangedInvalidateAll(string playerId)
        {
            m_PlayerChangedCount++;

            // A player switch resets Wire's connection and clears its subscriptions, so drop every cached
            // channel here too (notifying current subscribers) rather than reusing a channel Wire discarded.
            foreach (var state in m_ChannelGuards.Values.ToArray())
            {
                DropChannelAndNotify(state);
                RemoveIfIdle(state);
            }
        }

        async Task UnregisterAsync(Subscriber subscriber)
        {
            // A guard outlives its subscribers and queued callers, so no guard means nothing to undo.
            if (!m_ChannelGuards.ContainsKey(subscriber.Key))
            {
                return;
            }

            var channelGuard = AcquireChannelGuard(subscriber.Key);
            await channelGuard.Gate.WaitAsync();
            try
            {
                if (!channelGuard.Subscribers.Remove(subscriber))
                {
                    return;
                }

                // Notify this subscriber with Unsubscribed as if it owned the channel.
                NotifySubscriber(subscriber, EventConnectionState.Unsubscribed);

                // other subscribers still need the channel, or nothing to tear down
                if (channelGuard.Subscribers.Count > 0 || channelGuard.Channel == null)
                {
                    return;
                }

                // Last subscriber out - retire and tear down, retrying transient failures.
                var channel = channelGuard.Channel;
                channel.IsRetired = true;
                var wasSuccessfullyUnsubscribed = await UnsubscribeWithRetryAsync(channel);

                // If the guard's channel was reset while tearing down (player switch, a switch away and
                // back,or a kick eviction), don't resurrect it - Wire has flushed it out.
                if (!ReferenceEquals(channelGuard.Channel, channel))
                {
                    if (!wasSuccessfullyUnsubscribed)
                    {
                        _ = SafeUnsubscribeAsync(channel);
                    }
                    return;
                }

                // If unsubscription fails after attempted retries, we have a survivor that's
                // alive. To avoid leaks, assign it back to the gate's channel for future reuse.
                if (wasSuccessfullyUnsubscribed)
                {
                    channelGuard.Channel = null;
                }
                else
                {
                    channel.IsRetired = false;
                }
            }
            finally
            {
                ReleaseChannelGuard(channelGuard);
            }
        }

        async Task<bool> UnsubscribeWithRetryAsync(SubscriptionChannel channel)
        {
            var delay = m_UnsubscribeRetryBaseDelay;
            for (var retry = 0;; retry++)
            {
                try
                {
                    await channel.UnsubscribeAsync();
                    return true;
                }
                catch
                {
                    if (channel.LastKnownState == EventConnectionState.Unsubscribed)
                    {
                        return true;
                    }

                    // Out of retries: hand the still-live channel back for reuse.
                    if (retry >= m_MaxUnsubscribeRetries)
                    {
                        return false;
                    }

                    // Exponential backoff before retrying the unsubscribe.
                    if (delay > TimeSpan.Zero)
                    {
                        await Awaitable.WaitForSecondsAsync((float)delay.TotalSeconds);
                    }
                    delay *= 2d;
                }
            }
        }

        static async Task SafeUnsubscribeAsync(SubscriptionChannel channel)
        {
            try
            {
                await channel.UnsubscribeAsync();
            }
            catch
            {
                // Best-effort cleanup of a stale channel; wire may already have dropped it.
            }
        }

        /// <summary>
        /// One subscriber's handle to a shared channel, handed back from
        /// <see cref="SubscriptionProvider.SubscribeAsync"/>. It carries this subscriber's own
        /// <see cref="SubscriptionEventCallbacks"/>, which the provider fans the shared channel's events into.
        /// </summary>
        sealed class Subscriber : ISubscriptionEvents
        {
            readonly SubscriptionProvider m_Provider;

            internal object Key { get; }
            internal IChannelTokenProvider TokenProvider { get; }
            public SubscriptionEventCallbacks Callbacks { get; }

            internal Subscriber(SubscriptionProvider provider, object key, IChannelTokenProvider tokenProvider,
                                SubscriptionEventCallbacks callbacks)
            {
                m_Provider = provider;
                Key = key;
                TokenProvider = tokenProvider;
                Callbacks = callbacks;
            }

            public Task SubscribeAsync()
            {
                return m_Provider.RegisterAsync(this);
            }

            public Task UnsubscribeAsync()
            {
                return m_Provider.UnregisterAsync(this);
            }
        }
    }
}
