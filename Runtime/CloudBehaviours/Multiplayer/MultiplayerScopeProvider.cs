using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using Unity.Services.Multiplayer.Components;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Produces the <see cref="CloudCodeScope"/> that binds cloud behaviour calls to a multiplayer session.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A session-scoped cloud behaviour holds one server-side state instance per multiplayer session, and
    /// every call must name which instance it targets — generated bindings do this by passing a
    /// <c>CloudCodeScope(ScopeType.MultiplayerSession, sessionId)</c> with each request. This provider
    /// derives that scope from the active session and keeps it current, so game code can bind to
    /// <see cref="Scope"/> and <see cref="ScopeChanged"/> rather than tracking session lifetime itself.
    /// </para>
    /// <para>
    /// A scope needs a signed-in player and the session this provider tracks: it stays <c>null</c>
    /// until both hold, and is withdrawn as soon as either stops. When the provider was given a scope
    /// channel (<see cref="UseScopeChannel"/>), the scope is also withheld until that channel is open,
    /// so no push for the scope can be missed.
    /// </para>
    /// <para>
    /// The <c>Enable</c> overloads choose between two modes. Bare <see cref="Enable()"/> adopts whichever
    /// single session the multiplayer service reports and treats more than one as an error; every other
    /// overload names the session to follow. Nothing happens until one of them is called.
    /// </para>
    /// </remarks>
    public sealed class MultiplayerScopeProvider : ICloudCodeScopeProvider
    {
        /// <summary>
        /// Raised whenever <see cref="Scope"/> changes, including when it is withdrawn. Not raised when
        /// an update resolves to the scope that is already published.
        /// </summary>
        public event Action ScopeChanged;

        /// <summary>
        /// Raised after the provider has disabled itself, either because the player or scope channel
        /// connection failed or because bare <see cref="Enable()"/> found more than one active session and reported
        /// <see cref="CloudBehaviourClientExceptionReason.AmbiguousSessions"/>. It must be re-enabled to
        /// recover.
        /// </summary>
        /// <remarks>
        /// Re-enabling picks up whatever the current sign-in state is: it retries the subscription
        /// immediately if a player is signed in, and otherwise waits for the next sign-in. Pass the same
        /// argument you originally enabled with — a bare <see cref="Enable()"/> reverts to single-session
        /// tracking rather than resuming the session you configured.
        /// </remarks>
        public event Action<CloudBehaviourClientException> Failed;

        /// <summary>
        /// Raised for each message received on the player's subscription. Suppressed while
        /// <see cref="Scope"/> is <c>null</c>.
        /// </summary>
        public event Action<IMessageReceivedEvent> MessageReceived;

        /// <summary>
        /// Raised when the player's message subscription changes connection state, as
        /// <see cref="PlayerConnection.ConnectionStateChanged"/> describes. When a scope channel is configured it
        /// reports that channel's state instead, because the scope channel carries every push. Suppressed while
        /// <see cref="Scope"/> is <c>null</c>.
        /// </summary>
        /// <remarks>
        /// An error on the scope channel is reported here as <see cref="EventConnectionState.Error"/> and does not
        /// disable the provider, because the channel can recover on a later reconnect. A remote close is reported
        /// here first, as <see cref="EventConnectionState.Unsubscribed"/>, and then through <see cref="Failed"/>,
        /// which disables the provider. Listen to <see cref="Failed"/> to learn that the provider has disabled itself.
        /// </remarks>
        public event Action<EventConnectionState> ConnectionStateChanged;

        /// <summary>
        /// The scope targeting the tracked session, or <c>null</c> while no player is signed in, no
        /// session is resolved, or, when a scope channel is configured, that channel is not open yet.
        /// </summary>
        public CloudCodeScope Scope { get; private set; }

        /// <summary>
        /// The Cloud Code service resolved from the same registry this provider derives its scope
        /// from. Callers invoking endpoints on a provider's behalf should use this, so scope and
        /// calls never diverge across registries.
        /// </summary>
        public ICloudCodeService CloudCode => m_PlayerConnection.Services.GetCloudCodeService();

        readonly PlayerConnection m_PlayerConnection;
        ISession m_ConfiguredSession;
        MultiplayerSession m_ConfiguredMultiplayerSession;
        SessionObserver m_ConfiguredSessionObserver;
        bool m_OwnsConfiguredSessionObserver;
        IMultiplayerService m_MultiplayerService;
        ISession m_Session;
        bool m_Enabled;
        string m_ScopeChannelModule;
        string m_ScopeChannelTokenFunction;
        ScopeConnection m_ScopeConnection;
        ModuleHostWarmup m_ScopeChannelWarmup;

        /// <summary>
        /// Initializes a new provider against the given services registry.
        /// </summary>
        /// <param name="services">
        /// The registry to resolve authentication, Cloud Code and the multiplayer service from, or
        /// <c>null</c> to use <see cref="UnityServices.Instance"/>.
        /// </param>
        public MultiplayerScopeProvider(IUnityServices services = null)
        {
            m_PlayerConnection = new PlayerConnection(services);
        }

        /// <summary>
        /// Called by generated Cloud Behaviour clients; game code does not need it. Makes the provider also listen on the scope's own push channel, whose subscription token the
        /// given module function returns. Call it before <see cref="Enable()"/>: on a provider that has already
        /// been enabled it throws. Calling it again with the same names does nothing, and with different names
        /// after the channel was first built it throws.
        /// </summary>
        /// <param name="module">The module that holds the token function.</param>
        /// <param name="tokenFunction">
        /// The function that returns the reply of <c>IPushClient.GenerateScopeTokenAsync</c>.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="module"/> or <paramref name="tokenFunction"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="module"/> or <paramref name="tokenFunction"/> is empty or whitespace.</exception>
        /// <exception cref="InvalidOperationException">
        /// The provider is already enabled, or the scope channel was already built with other names.
        /// </exception>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public void UseScopeChannel(string module, string tokenFunction)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));

            if (string.IsNullOrWhiteSpace(module))
                throw new ArgumentException("A module name is required.", nameof(module));

            if (tokenFunction == null)
                throw new ArgumentNullException(nameof(tokenFunction));

            if (string.IsNullOrWhiteSpace(tokenFunction))
                throw new ArgumentException("A token function name is required.", nameof(tokenFunction));

            if (module == m_ScopeChannelModule && tokenFunction == m_ScopeChannelTokenFunction)
                return;

            if (m_Enabled)
                throw new InvalidOperationException(
                    "UseScopeChannel was called after the provider was enabled. Call it before Enable.");

            if (m_ScopeConnection != null)
                throw new InvalidOperationException(
                    "UseScopeChannel was called with a different module or token function after the scope channel was built.");

            m_ScopeChannelModule = module;
            m_ScopeChannelTokenFunction = tokenFunction;
        }

        /// <summary>
        /// Tracks the given session. Calling any <c>Enable</c> on an already-enabled provider validates
        /// its argument and then does nothing.
        /// </summary>
        /// <param name="session">The session to follow.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="session"/> is <c>null</c>. Use <see cref="Enable()"/> to track without a target.
        /// </exception>
        /// <example>
        /// <para>Pass the session returned by a create or join call:</para>
        /// <code>
        /// var session = await MultiplayerService.Instance.CreateOrJoinSessionAsync(
        ///     sessionId, new SessionOptions { MaxPlayers = 4 });
        /// provider.Enable(session);
        /// </code>
        /// </example>
        public void Enable(ISession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (m_Enabled)
                return;

            m_ConfiguredSession = session;
            Enable();
        }

        /// <summary>
        /// Tracks whichever session of the given type appears, by observing the multiplayer service.
        /// </summary>
        /// <param name="sessionType">The session type to observe, for example <c>"gameplay"</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="sessionType"/> is <c>null</c>. Use <see cref="Enable()"/> to track without a target.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="sessionType"/> is empty or whitespace. No session can carry such a type, so no
        /// scope would ever be published. Use <see cref="Enable()"/> to track without a target.
        /// </exception>
        /// <example>
        /// <para>
        /// The type is a label you choose, and it must match the one the session was created with.
        /// <c>SessionOptions.Type</c> defaults to a fresh GUID, so it has to be set explicitly on both
        /// sides or nothing will ever match:
        /// </para>
        /// <code>
        /// // wherever the session is created
        /// await MultiplayerService.Instance.CreateSessionAsync(
        ///     new SessionOptions { Type = "gameplay", MaxPlayers = 4 });
        ///
        /// // here — adopts the first session of that type to appear
        /// provider.Enable("gameplay");
        /// </code>
        /// </example>
        public void Enable(string sessionType)
        {
            if (sessionType == null)
                throw new ArgumentNullException(nameof(sessionType));

            if (string.IsNullOrWhiteSpace(sessionType))
                throw new ArgumentException(
                    "A session type is required. Use Enable() to track without a target.",
                    nameof(sessionType));

            if (m_Enabled)
                return;

            var observer = new SessionObserver(sessionType, m_PlayerConnection.Services);
            m_OwnsConfiguredSessionObserver = true;
            Enable(observer);
        }

        /// <summary>
        /// Tracks the session surfaced by the given observer.
        /// </summary>
        /// <param name="session">The observer whose session to follow.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="session"/> is <c>null</c>. Use <see cref="Enable()"/> to track without a target.
        /// </exception>
        /// <example>
        /// <para>Use this instead of <see cref="Enable(string)"/> when the observer is shared with other code:</para>
        /// <code>
        /// var observer = new SessionObserver("gameplay");
        /// observer.SessionAdded += OnSessionAdded;
        /// provider.Enable(observer);
        /// </code>
        /// </example>
        public void Enable(SessionObserver session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (m_Enabled)
                return;

            m_ConfiguredSessionObserver = session;
            Enable();
        }

        /// <summary>
        /// Tracks the session assigned to the given <see cref="MultiplayerSession"/> asset.
        /// </summary>
        /// <remarks>
        /// Sessions are matched against the asset's <c>Session</c> property, which the multiplayer
        /// package populates — for example when a <c>SessionConnector</c> creates or joins one. No scope
        /// is published while that property is still <c>null</c>.
        /// </remarks>
        /// <param name="session">The asset whose assigned session to follow.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="session"/> is <c>null</c> or has been destroyed.
        /// </exception>
        /// <example>
        /// <para>
        /// Create the asset through <c>Assets &gt; Create &gt; Services &gt; Multiplayer &gt; Multiplayer
        /// Session</c> and have a <c>SessionConnector</c> fill it in — either by assigning both to a
        /// <c>SessionConnectorBehaviour</c> in the scene, or in code:
        /// </para>
        /// <code>
        /// [SerializeField] MultiplayerSession m_Session;
        /// [SerializeField] SessionConnector m_Connector;   // its MultiplayerSession points at m_Session
        ///
        /// void Start()
        /// {
        ///     m_Connector.Execute();   // creates or joins, then assigns m_Session.Session
        ///     provider.Enable(m_Session);
        /// }
        /// </code>
        /// </example>
        public void Enable(MultiplayerSession session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session),
                    "The MultiplayerSession asset is missing or has been destroyed.");

            if (m_Enabled)
                return;

            m_ConfiguredMultiplayerSession = session;
            m_ConfiguredMultiplayerSession.SessionLifecycle.SessionAdded.AddListener(OnSessionAdded);
            Enable();
        }

        /// <summary>
        /// Tracks the session the multiplayer service reports. Only a single active session is supported
        /// on this path: if the service has more than one, the provider raises <see cref="Failed"/> with
        /// <see cref="CloudBehaviourClientExceptionReason.AmbiguousSessions"/> and disables itself — use one of
        /// the other overloads to name the session explicitly.
        /// </summary>
        public void Enable()
        {
            if (m_Enabled)
                return;

            m_Enabled = true;

            m_PlayerConnection.SignedIn += OnPlayerConnectionChanged;
            m_PlayerConnection.SignedOut += OnPlayerConnectionChanged;
            m_PlayerConnection.Failed += OnPlayerConnectionFailed;
            m_PlayerConnection.MessageReceived += OnPlayerConnectionMessageReceived;
            m_PlayerConnection.ConnectionStateChanged += OnPlayerConnectionStateChanged;
            m_PlayerConnection.Enable();

            // If player connection synchronously fails - this binding is disabled. avoid continuing.
            if (!m_Enabled)
                return;

            if (m_PlayerConnection.Services.State == ServicesInitializationState.Initialized)
            {
                OnServicesInitialized();
            }
            else
            {
                m_PlayerConnection.Services.Initialized += OnServicesInitialized;
            }
        }

        void OnServicesInitialized()
        {
            m_PlayerConnection.Services.Initialized -= OnServicesInitialized;

            m_MultiplayerService = m_PlayerConnection.Services.GetMultiplayerService();
            m_MultiplayerService.SessionAdded += OnSessionAdded;
            m_MultiplayerService.SessionRemoved += OnSessionRemoved;

            var activeSessions = m_MultiplayerService.Sessions;

            // Default configuration, always attempt to pick the first session.
            // If more than one - providers cannot deal with this ambiguity, disable the provider.
            if (!ProviderEnabledWithConfiguredSession())
            {
                if (activeSessions.Count > 1)
                {
                    Disable();

                    Failed?.Invoke(new CloudBehaviourClientException(
                        CloudBehaviourClientExceptionReason.AmbiguousSessions,
                        $"Found {activeSessions.Count} active sessions."));
                    return;
                }

                if (activeSessions.Count == 1)
                    m_Session = activeSessions.Single().Value;

                UpdateScope();
                return;
            }

            // Else, Non-default configuration, attempt to grab the session from the
            // multiplayer service if it was added before.
            var configuredSessionId =  m_ConfiguredSession?.Id ??
                m_ConfiguredMultiplayerSession?.Session?.Id ??
                m_ConfiguredSessionObserver?.Session?.Id;

            if (configuredSessionId != null)
            {
                m_Session = activeSessions.Values.FirstOrDefault(val => val.Id == configuredSessionId);
            }

            UpdateScope();
        }

        bool ProviderEnabledWithConfiguredSession()
        {
            return m_ConfiguredSession != null ||
                m_ConfiguredMultiplayerSession != null ||
                m_ConfiguredSessionObserver != null;
        }

        void UpdateScope()
        {
            var sessionId = m_Session?.Id;
            var scope = m_PlayerConnection.IsSignedIn && sessionId != null
                ? new CloudCodeScope(ScopeType.MultiplayerSession, sessionId)
                : null;

            if (m_ScopeChannelModule != null)
            {
                scope = OpenScopeChannel(scope);
            }

            // Read after opening the channel, which can announce the scope itself while it opens.
            var lastScope = Scope;
            Scope = scope;

            // Ensure that we do not trigger ScopeChanged when it doesn't actually change
            if (!Equals(Scope, lastScope))
            {
                ScopeChanged?.Invoke();
            }
        }

        // The scope is published only once its channel is open, so no push is missed after the first Hydrate.
        CloudCodeScope OpenScopeChannel(CloudCodeScope scope)
        {
            if (scope == null)
            {
                m_ScopeConnection?.Close();
                return null;
            }

            if (m_ScopeConnection == null)
            {
                m_ScopeConnection = new ScopeConnection(CloudCode, m_ScopeChannelModule, m_ScopeChannelTokenFunction, WaitForModuleHostAsync);
                m_ScopeConnection.Opened += OnScopeConnectionOpened;
                m_ScopeConnection.Failed += OnScopeConnectionFailed;
                m_ScopeConnection.MessageReceived += OnScopeConnectionMessageReceived;
                m_ScopeConnection.ConnectionStateChanged += OnScopeConnectionStateChanged;
            }

            m_ScopeConnection.Open(scope);
            return m_ScopeConnection.IsOpen ? scope : null;
        }

        Task WaitForModuleHostAsync()
        {
            var services = m_PlayerConnection.Services;
            var warmup = services == UnityServices.Instance
                ? ModuleHostWarmup.Default
                : m_ScopeChannelWarmup ??= new ModuleHostWarmup(services);

            warmup.Arm();
            return !warmup.IsWarm && warmup.Task != null ? warmup.Task : Task.CompletedTask;
        }

        void OnPlayerConnectionChanged(string playerID)
        {
            UpdateScope();
        }

        void OnPlayerConnectionFailed(CloudBehaviourClientException error)
        {
            Disable();
            Failed?.Invoke(error);
        }

        void OnPlayerConnectionMessageReceived(IMessageReceivedEvent msg)
        {
            // Scope is null until both a player and a session resolve; those messages are not ours.
            if (Scope == null)
                return;

            MessageReceived?.Invoke(msg);
        }

        void OnScopeConnectionOpened()
        {
            UpdateScope();
        }

        void OnScopeConnectionFailed(CloudBehaviourClientException error)
        {
            Disable();
            Failed?.Invoke(error);
        }

        void OnScopeConnectionMessageReceived(IMessageReceivedEvent msg)
        {
            if (Scope == null)
                return;

            MessageReceived?.Invoke(msg);
        }

        void OnScopeConnectionStateChanged(EventConnectionState state)
        {
            if (Scope == null)
                return;

            ConnectionStateChanged?.Invoke(state);
        }

        void OnPlayerConnectionStateChanged(EventConnectionState state)
        {
            // With a scope channel configured, its state is the one that matters for the pushes this provider forwards.
            if (Scope == null || m_ScopeChannelModule != null)
                return;

            ConnectionStateChanged?.Invoke(state);
        }

        void OnSessionAdded(ISession session)
        {
            // Default configuration, ensure providers only handle one session.
            // If more than one - providers cannot deal with this ambiguity, disable the provider.
            if (!ProviderEnabledWithConfiguredSession())
            {
                if (m_MultiplayerService.Sessions.Count > 1)
                {
                    // Captured before Disable, which drops the multiplayer service reference.
                    var sessionCount = m_MultiplayerService.Sessions.Count;
                    Disable();

                    Failed?.Invoke(new CloudBehaviourClientException(
                        CloudBehaviourClientExceptionReason.AmbiguousSessions,
                        $"Found {sessionCount} active sessions."));
                    return;
                }

                m_Session = session;
                UpdateScope();
                return;
            }

            // Else we are dealing with preconfiguration situations, only apply if session matches.
            if (SessionPertainsToConfiguredSession(session))
            {
                m_Session = session;
                UpdateScope();
            }
        }

        bool SessionPertainsToConfiguredSession(ISession session)
        {
            if (session.Id == m_ConfiguredSession?.Id)
                return true;

            if (session.Id == m_ConfiguredSessionObserver?.Session?.Id)
                return true;

            if (session.Id == m_ConfiguredMultiplayerSession?.Session?.Id)
                return true;

            return false;
        }

        void OnSessionRemoved(ISession session)
        {
            if (m_Session?.Id != session.Id)
                return;

            m_Session = null;
            UpdateScope();
        }

        /// <summary>
        /// Stops tracking, withdraws <see cref="Scope"/> and releases the underlying connection. This
        /// also clears whichever session was configured through the other <c>Enable</c> overloads, so a
        /// later bare <see cref="Enable()"/> reverts to the single-session behaviour. Calling this on a
        /// provider that is not enabled does nothing.
        /// </summary>
        public void Disable()
        {
            if (!m_Enabled)
                return;

            m_ConfiguredSession = null;

            if (m_MultiplayerService != null)
            {
                m_MultiplayerService.SessionAdded -= OnSessionAdded;
                m_MultiplayerService.SessionRemoved -= OnSessionRemoved;
                m_MultiplayerService = null;
            }

            if (m_ConfiguredSessionObserver != null)
            {
                if (m_OwnsConfiguredSessionObserver)
                    m_ConfiguredSessionObserver.Dispose();

                m_ConfiguredSessionObserver = null;
                m_OwnsConfiguredSessionObserver = false;
            }

            if (m_ConfiguredMultiplayerSession != null)
            {
                m_ConfiguredMultiplayerSession.SessionLifecycle.SessionAdded.RemoveListener(OnSessionAdded);
                m_ConfiguredMultiplayerSession = null;
            }

            m_PlayerConnection.SignedIn -= OnPlayerConnectionChanged;
            m_PlayerConnection.SignedOut -= OnPlayerConnectionChanged;
            m_PlayerConnection.Failed -= OnPlayerConnectionFailed;
            m_PlayerConnection.MessageReceived -= OnPlayerConnectionMessageReceived;
            m_PlayerConnection.ConnectionStateChanged -= OnPlayerConnectionStateChanged;
            m_PlayerConnection.Services.Initialized -= OnServicesInitialized;
            m_PlayerConnection.Disable();
            m_ScopeChannelWarmup?.Reset();

            m_Session = null;
            m_Enabled = false;
            UpdateScope();
        }
    }
}
