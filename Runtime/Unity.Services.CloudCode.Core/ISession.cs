using System.Collections.Generic;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     <para>
    ///     Read-only view of the session (lobby) data associated with a multiplayer-session-scoped execution.
    ///     </para>
    ///     <para>
    ///     Fetched once per invocation for every class declaring <c>[StateScope(Scope.MultiplayerSession)]</c>.
    ///     <see cref="IExecutionContext.Session"/> is <see langword="null"/> for a <see cref="Scope.Player"/>-scoped
    ///     or non-scoped class, which have no associated session.
    ///     </para>
    /// </summary>
    public interface ISession
    {
        /// <summary>
        ///     The lobby/session identifier.
        /// </summary>
        string Id { get; }

        /// <summary>
        ///     The player ID of the session host.
        /// </summary>
        string HostId { get; }

        /// <summary>
        ///     The player IDs of all current session members.
        /// </summary>
        IReadOnlyList<string> PlayerIds { get; }
    }
}
