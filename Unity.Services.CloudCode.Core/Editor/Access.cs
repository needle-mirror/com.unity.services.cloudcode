namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     Enum specifying the access control level for a Cloud Code function.
    ///     Values are ordered from most restrictive to most permissive so that
    ///     ordinal comparison (e.g. <c>granted &gt;= required</c>) is meaningful.
    ///     Service accounts are always permitted regardless of the access level.
    ///
    ///     Hierarchy (most permissive → most restrictive):
    ///       Global → Members → Host → ServiceAccount
    ///
    ///     For player-scoped functions, both Host and Members allow the owning player to invoke the function.
    ///     For non-scoped functions, only ServiceAccount vs Global is meaningful;
    ///     Members and Host behave like Global since there is no scope to check against.
    /// </summary>
    public enum Access
    {
        /// <summary>
        ///     Only service account callers (no players) can invoke the function.
        ///     This is the most restrictive access level.
        /// </summary>
        ServiceAccount = 0,

        /// <summary>
        ///     The host of the session (or the owning player for player-scoped functions)
        ///     and service accounts may invoke the function.
        /// </summary>
        Host = 1,

        /// <summary>
        ///     Any member of the session (including the host), the owning player
        ///     for player-scoped functions, and service accounts may invoke the function.
        /// </summary>
        Members = 2,

        /// <summary>
        ///     Anyone may invoke the function, regardless of session membership.
        ///     This is the default access level.
        /// </summary>
        Global = 3,
    }
}
