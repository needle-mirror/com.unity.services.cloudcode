using System;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Tracks the signed-in player and keeps a Cloud Code player-message subscription open for them,
    /// surfacing the combined authentication and subscription lifecycle as events.
    /// </summary>
    /// <remarks>
    /// A player is only reported as signed in once the push subscription is open, so
    /// <see cref="IsSignedIn"/> lags authentication by the round trip that opens the channel.
    /// Nothing happens until <see cref="Enable"/> is called.
    /// </remarks>
    public class PlayerConnection
    {
        /// <summary>
        /// Raised with the player's id once that player is authenticated <b>and</b> their message
        /// subscription is open. <see cref="PlayerId"/> and <see cref="IsSignedIn"/> are already set
        /// when this runs.
        /// </summary>
        public event Action<string> SignedIn;

        /// <summary>
        /// Raised when the player signs out or their session expires, carrying the id of the player
        /// who <i>was</i> signed in. Not raised by <see cref="Disable"/>, nor by the self-disable that
        /// follows a failure, nor for a player who was never announced through <see cref="SignedIn"/>
        /// because their subscription had not opened yet.
        /// </summary>
        public event Action<string> SignedOut;

        /// <summary>
        /// Raised when the connection cannot be established or is lost. Either failure disables the
        /// connection, so call <see cref="Enable"/> to recover — but not from within this handler, since a
        /// failure that reproduces immediately would recurse.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///     <item>
        ///         <description>
        ///             <see cref="CloudBehaviourExceptionReason.SubscriptionError"/> — the subscription
        ///             could not be opened.
        ///         </description>
        ///     </item>
        ///     <item>
        ///         <description>
        ///             <see cref="CloudBehaviourExceptionReason.Kicked"/> — the channel was closed
        ///             remotely. The player is still authenticated, but nothing reopens the channel.
        ///         </description>
        ///     </item>
        /// </list>
        /// </remarks>
        public event Action<CloudBehaviourException> Failed;

        /// <summary>
        /// Raised for each message received on the player's subscription, while one is open.
        /// </summary>
        public event Action<IMessageReceivedEvent> MessageReceived;

        /// <summary>
        /// The services registry this connection resolves authentication and Cloud Code from.
        /// </summary>
        public IUnityServices Services => m_Services ?? UnityServices.Instance;

        readonly IUnityServices m_Services;

        /// <summary>
        /// The id of the currently signed-in player, or <c>null</c> while none is.
        /// </summary>
        public string PlayerId {get; private set;}

        /// <summary>
        /// Whether a player is signed in <b>and</b> their message subscription is open. False while a
        /// player is authenticated but the subscription has not completed, and after any failure, which
        /// disables the connection.
        /// </summary>
        public bool IsSignedIn {get; private set;}

        ISubscriptionEvents m_SubscriptionEvents;
        IAuthenticationService AuthenticationService => Services.GetAuthenticationService();
        ICloudCodeService CloudCodeService => Services.GetCloudCodeService();

        bool m_Enabled;

        // Advanced on every sign-in/out/expire/disable so a suspended OnSignedIn can tell
        // its subscription attempt was superseded.
        int m_SubscriptionAttempt;

        /// <summary>
        /// Initializes a new connection against the given services registry.
        /// </summary>
        /// <param name="services">
        /// The registry to resolve authentication and Cloud Code from, or <c>null</c> to use
        /// <see cref="UnityServices.Instance"/>.
        /// </param>
        public PlayerConnection(IUnityServices services)
        {
            m_Services = services;
        }

        /// <summary>
        /// Starts tracking authentication, opening a subscription for whoever is signed in now or signs
        /// in later. Defers until services finish initializing if they have not yet. Calling this on an
        /// already-enabled connection does nothing. This is also the recovery path after
        /// <see cref="Failed"/>, which leaves the connection disabled.
        /// </summary>
        public void Enable()
        {
            if (m_Enabled)
                return;

            m_Enabled = true;

            if (Services.State == ServicesInitializationState.Initialized)
            {
                OnServicesInitialized();
            }
            else
            {
                Services.Initialized += OnServicesInitialized;
            }
        }

        void OnServicesInitialized()
        {
            Services.Initialized -= OnServicesInitialized;
            AuthenticationService.SignedIn += OnSignedIn;
            AuthenticationService.SignedOut += OnSignedOut;
            AuthenticationService.Expired += OnSignedOut;

            if (AuthenticationService.IsSignedIn)
            {
                OnSignedIn();
            }
        }

        async void OnSignedIn()
        {
            var subscriptionAttempt = ++m_SubscriptionAttempt;

            try
            {
                StopMessageCallbacksAndClearLoginState();

                var subscription = await CloudCodeService.SubscribeToPlayerMessagesAsync();

                // A Disable/SignOut/newer SignIn during the await supersedes this sign-in.
                // We acquired a reference via Subscribe above; release it since we won't adopt it.
                if (subscriptionAttempt != m_SubscriptionAttempt)
                {
                    _ = UnsubscribeSafely(subscription);
                    return;
                }

                m_SubscriptionEvents = subscription;
                m_SubscriptionEvents.Callbacks.MessageReceived += OnSubscriptionMessageReceived;
                m_SubscriptionEvents.Callbacks.Kicked += OnSubscriptionKicked;

                PlayerId = AuthenticationService.PlayerId;
                IsSignedIn = true;
            }
            catch (Exception e)
            {
                // Catch and notify exceptions here as async Subscribe can throw in this
                // async void of which is required for Auth callbacks.
                // A superseded attempt's failure is not the current connection's; a newer attempt may own it.
                if (subscriptionAttempt != m_SubscriptionAttempt)
                    return;

                Disable();
                Failed?.Invoke(new CloudBehaviourException(
                    CloudBehaviourExceptionReason.SubscriptionError, innerException: e));
                return;
            }

            SignedIn?.Invoke(PlayerId);
        }

        void StopMessageCallbacksAndClearLoginState()
        {
            if (m_SubscriptionEvents != null)
            {
                var subscription = m_SubscriptionEvents;
                subscription.Callbacks.MessageReceived -= OnSubscriptionMessageReceived;
                subscription.Callbacks.Kicked -= OnSubscriptionKicked;
                m_SubscriptionEvents = null;
                _ = UnsubscribeSafely(subscription);
            }

            PlayerId = null;
            IsSignedIn = false;
        }

        /// <summary>
        /// Stops tracking authentication, releases any open subscription and clears the login state
        /// without raising <see cref="SignedOut"/>. Calling this on a connection that is not enabled
        /// does nothing.
        /// </summary>
        public void Disable()
        {
            if (!m_Enabled)
                return;

            m_SubscriptionAttempt++;

            Services.Initialized -= OnServicesInitialized;
            StopAuthenticationCallbacks();
            StopMessageCallbacksAndClearLoginState();

            m_Enabled = false;
        }

        void StopAuthenticationCallbacks()
        {
            if (AuthenticationService != null)
            {
                AuthenticationService.SignedIn -= OnSignedIn;
                AuthenticationService.SignedOut -= OnSignedOut;
                AuthenticationService.Expired -= OnSignedOut;
            }
        }

        void OnSignedOut()
        {
            m_SubscriptionAttempt++;
            var player = PlayerId;
            StopMessageCallbacksAndClearLoginState();

            // A sign-out landing before the subscription opened has no SignedIn to pair with.
            if (player != null)
                SignedOut?.Invoke(player);
        }

        async Task UnsubscribeSafely(ISubscriptionEvents subscription)
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

        void OnSubscriptionMessageReceived(IMessageReceivedEvent msg)
        {
            MessageReceived?.Invoke(msg);
        }

        void OnSubscriptionKicked()
        {
            Disable();
            Failed?.Invoke(new CloudBehaviourException(
                CloudBehaviourExceptionReason.Kicked));
        }
    }
}
