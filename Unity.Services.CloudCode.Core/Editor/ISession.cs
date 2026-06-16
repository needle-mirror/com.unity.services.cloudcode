using System.Collections.Generic;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     Read-only view of the session (lobby) data associated with a multiplayer-session-scoped execution.
    ///     Populated lazily when the function's access attribute requires lobby data for authorization.
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
