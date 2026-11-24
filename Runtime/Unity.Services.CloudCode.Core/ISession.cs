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
        ///     The player ID of the session host, read when the call starts. The session moves the host when
        ///     the host leaves, so a copy kept in a behaviour's field goes stale; for host-only functions use
        ///     <see cref="Access.SessionHost"/>, which checks the current host on every call.
        /// </summary>
        string HostId { get; }

        /// <summary>
        ///     The player IDs of all current session members, in no particular order. The order is not join
        ///     order, so use <see cref="HostId"/> to find the host.
        /// </summary>
        IReadOnlyList<string> PlayerIds { get; }
    }
}
