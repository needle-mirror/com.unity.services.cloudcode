using System;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Produces the <see cref="CloudCodeScope"/> that binds cloud behaviour calls to a specific player.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A player-scoped cloud behaviour holds one server-side state instance per player, and every call
    /// must name which instance it targets — generated bindings do this by passing a
    /// <c>CloudCodeScope(ScopeType.Player, playerId)</c> with each request. This provider derives that
    /// scope from the signed-in player and keeps it current, so game code can bind to
    /// <see cref="Scope"/> and <see cref="ScopeChanged"/> rather than tracking player ids and sign-in
    /// state itself.
    /// </para>
    /// <para>
    /// <see cref="Scope"/> is <c>null</c> whenever the player this provider tracks is not signed in.
    /// The provider wraps its own <see cref="PlayerConnection"/>, so a scope appears only once that
    /// player's message subscription is open, and the same connection feeds
    /// <see cref="MessageReceived"/>. Nothing happens until one of the <c>Enable</c> overloads is
    /// called.
    /// </para>
    /// </remarks>
    public class PlayerScopeProvider : ICloudCodeScopeProvider
    {
        /// <summary>
        /// Raised whenever <see cref="Scope"/> changes, including when it is withdrawn. Not raised when
        /// an update resolves to the scope that is already published.
        /// </summary>
        public event Action ScopeChanged;

        /// <summary>
        /// Raised after the provider has disabled itself in response to a connection failure, so it must
        /// be re-enabled to recover.
        /// </summary>
        /// <remarks>
        /// Re-enabling picks up whatever the current sign-in state is: it retries the subscription
        /// immediately if a player is signed in, and otherwise waits for the next sign-in. Pass the same
        /// argument you originally enabled with — a bare <see cref="Enable()"/> resumes tracking any
        /// player rather than the one you configured.
        /// </remarks>
        public event Action<CloudBehaviourException> Failed;

        /// <summary>
        /// Raised for each message received on the player's subscription. Suppressed while
        /// <see cref="Scope"/> is <c>null</c>, so messages belonging to a player this provider is not
        /// tracking are not forwarded.
        /// </summary>
        public event Action<IMessageReceivedEvent> MessageReceived;

        /// <summary>
        /// The scope targeting the tracked player, or <c>null</c> while no such player is signed in.
        /// </summary>
        public CloudCodeScope Scope { get; private set; }

        /// <inheritdoc/>
        public ICloudCodeService CloudCode => m_PlayerConnection.Services.GetCloudCodeService();

        readonly PlayerConnection m_PlayerConnection;
        string m_ConfiguredPlayerId;
        bool m_Enabled;

        /// <summary>
        /// Initializes a new provider against the given services registry.
        /// </summary>
        /// <param name="services">
        /// The registry to resolve authentication and Cloud Code from, or <c>null</c> to use
        /// <see cref="UnityServices.Instance"/>.
        /// </param>
        public PlayerScopeProvider(IUnityServices services = null)
        {
            m_PlayerConnection = new PlayerConnection(services);
        }

        /// <summary>
        /// Starts tracking only the given player: a scope is published while that player is signed in,
        /// and any other player signing in publishes none. Calling this on an already-enabled provider
        /// validates <paramref name="playerId"/> and then does nothing.
        /// </summary>
        /// <param name="playerId">The id of the player to track.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="playerId"/> is <c>null</c>. Use <see cref="Enable()"/> to follow whichever
        /// player is signed in.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="playerId"/> is empty or whitespace. No player can carry such an id, so no scope
        /// would ever be published. Use <see cref="Enable()"/> to follow whichever player is signed in.
        /// </exception>
        /// <example>
        /// <para>
        /// Pin the provider to the player whose server-side state you mean to reach, so a later account
        /// switch publishes no scope rather than silently retargeting calls at a different player:
        /// </para>
        /// <code>
        /// await AuthenticationService.Instance.SignInAnonymouslyAsync();
        /// provider.Enable(AuthenticationService.Instance.PlayerId);
        /// </code>
        /// </example>
        public void Enable(string playerId)
        {
            if (playerId == null)
                throw new ArgumentNullException(nameof(playerId));

            if (string.IsNullOrWhiteSpace(playerId))
                throw new ArgumentException(
                    "A player id is required. Use Enable() to follow whichever player is signed in.",
                    nameof(playerId));

            if (m_Enabled)
                return;

            m_ConfiguredPlayerId = playerId;
            Enable();
        }

        /// <summary>
        /// Starts tracking whichever player is signed in, publishing a scope for each in turn. Calling
        /// this on an already-enabled provider does nothing.
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
            m_PlayerConnection.Enable();

            // If player connection synchronously fails - this binding is disabled. avoid continuing.
            if (!m_Enabled)
                return;

            OnPlayerConnectionChanged(m_PlayerConnection.PlayerId);
        }

        void OnPlayerConnectionChanged(string playerId)
        {
            // Default behavior - provider doesn't care who signed in or out, trigger scopes.
            if (m_ConfiguredPlayerId == null)
            {
                UpdateScope();
                return;
            }

            // Else only trigger the scope calls for the targeted configured player.
            if (playerId == m_ConfiguredPlayerId)
            {
                UpdateScope();
            }
        }

        void UpdateScope()
        {
            var lastScope = Scope;
            if (m_PlayerConnection.IsSignedIn)
            {
                Scope = new CloudCodeScope(ScopeType.Player,  m_PlayerConnection.PlayerId);
            }
            else
            {
                Scope = null;
            }

            // Ensure that we do not trigger ScopeChanged when it doesn't actually change
            if (!Equals(Scope, lastScope))
            {
                ScopeChanged?.Invoke();
            }
        }

        /// <summary>
        /// Stops tracking, withdraws <see cref="Scope"/> and releases the underlying connection. This
        /// also clears the player configured by <see cref="Enable(string)"/>, so a later bare
        /// <see cref="Enable()"/> tracks any player rather than the one originally requested. Calling
        /// this on a provider that is not enabled does nothing.
        /// </summary>
        public void Disable()
        {
            if (!m_Enabled)
                return;

            m_PlayerConnection.SignedIn -= OnPlayerConnectionChanged;
            m_PlayerConnection.SignedOut -= OnPlayerConnectionChanged;
            m_PlayerConnection.Failed -= OnPlayerConnectionFailed;
            m_PlayerConnection.MessageReceived -= OnPlayerConnectionMessageReceived;
            m_PlayerConnection.Disable();

            m_ConfiguredPlayerId = null;
            m_Enabled = false;
            UpdateScope();
        }

        void OnPlayerConnectionFailed(CloudBehaviourException error)
        {
            Disable();
            Failed?.Invoke(error);
        }

        void OnPlayerConnectionMessageReceived(IMessageReceivedEvent msg)
        {
            // Scope is null while a non-configured player is signed in; those messages are not ours.
            if (Scope == null)
                return;

            MessageReceived?.Invoke(msg);
        }
    }
}
