#nullable enable
namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     The execution context for a request
    /// </summary>
    public interface IExecutionContext
    {
        /// <summary>
        ///     The project ID of the script
        /// </summary>
        string ProjectId { get; }

        /// <summary>
        ///     The player ID of the user that is executing the script, not available when the script is called by a service
        ///     account
        /// </summary>
        string? PlayerId { get; }

        /// <summary>
        ///     The environment ID of the script
        /// </summary>
        string EnvironmentId { get; }

        /// <summary>
        ///     The environment name of the script
        /// </summary>
        string EnvironmentName { get; }

        /// <summary>
        ///     The JWT credential used by the player to authenticate to Cloud Code
        /// </summary>
        string AccessToken { get; }

        /// <summary>
        ///     The user ID of the service account, not available when the script is called by a player
        /// </summary>
        string? UserId { get; }

        /// <summary>
        ///     The issuer or the service account token, not available when the script is called by a player
        /// </summary>
        string? Issuer { get; }

        /// <summary>
        ///     The Cloud Code service account JWT credential
        /// </summary>
        string ServiceToken { get; }

        /// <summary>
        ///     The Analytics User ID of the player, not available when the script is called by a service account
        /// </summary>
        string? AnalyticsUserId { get; }

        /// <summary>
        ///     The Unity device installation ID of the player, not available when the script is called by a service account
        /// </summary>
        string? UnityInstallationId { get; }

        /// <summary>
        ///     The correlation ID of this request
        /// </summary>
        string CorrelationId { get; }

        /// <summary>
        ///     The ID provided to identify the scope of this execution.
        /// </summary>
        string? ScopeId { get; }

        /// <summary>
        ///     The current call depth for nested RunModule/RunScript (X-Call-Depth). Set by the handler; modules must not override it.
        ///     The client sends depth+1 when making nested RunModule calls.
        /// </summary>
        int CallDepth { get; }

        /// <summary>
        ///     The session data for the current multiplayer-session-scoped execution.
        ///     Null when the execution is not session-scoped.
        /// </summary>
        ISession? Session { get; }
    }
}
