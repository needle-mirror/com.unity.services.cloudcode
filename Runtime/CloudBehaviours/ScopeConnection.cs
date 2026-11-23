using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using UnityEngine;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Keeps a push subscription open for one Cloud Code scope at a time, surfacing the subscription
    /// lifecycle as events. The subscription token comes from a module function that runs in that scope.
    /// </summary>
    internal sealed class ScopeConnection
    {
        /// <summary>Raised once the subscription for the current scope is open.</summary>
        public event Action Opened;

        /// <summary>
        /// Raised when the subscription could not be opened or was closed remotely. The connection is closed
        /// afterwards.
        /// </summary>
        public event Action<CloudBehaviourClientException> Failed;

        public event Action<IMessageReceivedEvent> MessageReceived;

        /// <summary>
        /// Raised when the open subscription's connection state changes, as
        /// <see cref="PlayerConnection.ConnectionStateChanged"/> describes.
        /// </summary>
        public event Action<EventConnectionState> ConnectionStateChanged;

        public bool IsOpen { get; private set; }

        readonly ICloudCodeService m_CloudCode;
        readonly string m_Module;
        readonly string m_TokenFunction;
        readonly Func<Task> m_WaitUntilReady;

        ISubscriptionEvents m_Subscription;
        CloudCodeScope m_Scope;
        SubscriptionEventCallbacks m_Callbacks;
        EventConnectionState? m_ObservedState;

        // Advanced on every open and release so a suspended subscribe can tell it was superseded.
        int m_SubscriptionAttempt;

        public ScopeConnection(ICloudCodeService cloudCode, string module, string tokenFunction, Func<Task> waitUntilReady = null)
        {
            m_CloudCode = cloudCode;
            m_Module = module;
            m_TokenFunction = tokenFunction;
            m_WaitUntilReady = waitUntilReady;
        }

        public async void Open(CloudCodeScope scope)
        {
            if (Equals(scope, m_Scope))
                return;

            Release();
            m_Scope = scope;
            var subscriptionAttempt = m_SubscriptionAttempt;

            if (m_WaitUntilReady != null)
            {
                try
                {
                    await m_WaitUntilReady();
                }
                catch (OperationCanceledException)
                {
                    // Only a warm-up reset cancels it, and the warm-up is an optimization: the token call works without it.
                }
                catch (Exception e)
                {
                    if (subscriptionAttempt != m_SubscriptionAttempt)
                        return;

                    m_Scope = null;
                    Failed?.Invoke(new CloudBehaviourClientException(
                        CloudBehaviourClientExceptionReason.InvokerUnavailable,
                        $"Scope channel for {scope.Type} {scope.Id}.",
                        e));
                    return;
                }

                if (subscriptionAttempt != m_SubscriptionAttempt)
                    return;
            }

            var callbacks = new SubscriptionEventCallbacks();
            callbacks.MessageReceived += OnSubscriptionMessageReceived;
            callbacks.Kicked += OnSubscriptionKicked;
            callbacks.ConnectionStateChanged += OnSubscriptionConnectionStateChangedWhileOpening;
            callbacks.Error += OnSubscriptionError;
            m_Callbacks = callbacks;

            ISubscriptionEvents subscription;
            try
            {
                subscription = await m_CloudCode.SubscribeToScopeMessagesAsync(m_Module, m_TokenFunction, scope, callbacks);
            }
            catch (Exception e)
            {
                if (subscriptionAttempt != m_SubscriptionAttempt)
                    return;

                DetachCallbacks();
                m_Scope = null;
                Failed?.Invoke(new CloudBehaviourClientException(
                    CloudBehaviourClientExceptionReason.SubscriptionError,
                    $"Scope channel for {scope.Type} {scope.Id}.",
                    e));
                return;
            }

            if (subscriptionAttempt != m_SubscriptionAttempt)
            {
                _ = UnsubscribeSafely(subscription);
                return;
            }

            m_Subscription = subscription;

            if (m_ObservedState == null || m_ObservedState == EventConnectionState.Subscribed)
                CompleteOpen();
        }

        public void Close()
        {
            Release();
        }

        void Release()
        {
            m_SubscriptionAttempt++;
            DetachCallbacks();

            if (m_Subscription != null)
            {
                var subscription = m_Subscription;
                m_Subscription = null;
                _ = UnsubscribeSafely(subscription);
            }

            m_Scope = null;
            IsOpen = false;
            m_ObservedState = null;
        }

        void OnSubscriptionMessageReceived(IMessageReceivedEvent msg)
        {
            MessageReceived?.Invoke(msg);
        }

        // A subscriber joining an existing channel is replayed its last state before the subscribe returns, so this decides whether that channel can be opened.
        void OnSubscriptionConnectionStateChangedWhileOpening(EventConnectionState state)
        {
            m_ObservedState = state;

            if (state == EventConnectionState.Error)
                Debug.LogWarning("[ScopeConnection] The scope channel is in an error state; the connection opens when the channel recovers.");

            if (state == EventConnectionState.Subscribed && m_Subscription != null)
                CompleteOpen();
        }

        void OnSubscriptionConnectionStateChanged(EventConnectionState state)
        {
            ConnectionStateChanged?.Invoke(state);
        }

        void CompleteOpen()
        {
            m_Callbacks.ConnectionStateChanged -= OnSubscriptionConnectionStateChangedWhileOpening;
            m_Callbacks.ConnectionStateChanged += OnSubscriptionConnectionStateChanged;
            IsOpen = true;
            Opened?.Invoke();
        }

        void DetachCallbacks()
        {
            if (m_Callbacks == null)
                return;

            m_Callbacks.MessageReceived -= OnSubscriptionMessageReceived;
            m_Callbacks.Kicked -= OnSubscriptionKicked;
            m_Callbacks.ConnectionStateChanged -= OnSubscriptionConnectionStateChangedWhileOpening;
            m_Callbacks.ConnectionStateChanged -= OnSubscriptionConnectionStateChanged;
            m_Callbacks.Error -= OnSubscriptionError;
            m_Callbacks = null;
        }

        // Not a failure: the channel can recover from Error, and ConnectionStateChanged reports the state.
        void OnSubscriptionError(string error)
        {
            Debug.LogWarning($"[ScopeConnection] The scope channel's message subscription reported an error: {error}");
        }

        void OnSubscriptionKicked()
        {
            Release();
            Failed?.Invoke(new CloudBehaviourClientException(CloudBehaviourClientExceptionReason.Kicked));
        }

        static async Task UnsubscribeSafely(ISubscriptionEvents subscription)
        {
            try
            {
                await subscription.UnsubscribeAsync();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }
}
