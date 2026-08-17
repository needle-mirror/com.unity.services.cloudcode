using System;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     <para>
    ///     Specifies which callers may invoke a Cloud Code function.
    ///     Note that while there is some level of hierarchy between the access levels,
    ///     the values are not ordinal and must not be compared numerically.
    ///     </para>
    ///
    ///     <para>
    ///     When no access level is supplied the attribute defaults to
    ///     <see cref="Unspecified"/>, which the runtime resolves from the class
    ///     scope at module load: non-scoped resolves to <see cref="Global"/>,
    ///     <see cref="Scope.Player"/> resolves to <see cref="Player"/>, and
    ///     <see cref="Scope.MultiplayerSession"/> resolves to
    ///     <see cref="SessionMember"/>.
    ///     </para>
    /// </summary>
    public enum Access
    {
        /// <summary>
        ///     Only service-account callers may invoke the function.
        ///     Valid for any scope.
        /// </summary>
        Service = 0,

        /// <summary>
        ///     Deprecated alias of <see cref="Service"/> (value <c>0</c>, identical
        ///     behaviour). Retained so existing module source keeps compiling.
        /// </summary>
        [Obsolete("Renamed to Access.Service. The value (0) and behaviour are unchanged.")]
        ServiceAccount = 0,

        /// <summary>
        ///     Only the host of the session (and service accounts) may invoke the
        ///     function. Requires <see cref="Scope.MultiplayerSession"/>.
        /// </summary>
        SessionHost = 1,

        /// <summary>
        ///     Deprecated alias of <see cref="SessionHost"/> (value <c>1</c>). The
        ///     behaviour is now strict: declaring it on a <see cref="Scope.Player"/>
        ///     or non-scoped class is rejected when the module is deployed. For
        ///     player-owned access use <see cref="Player"/>.
        /// </summary>
        [Obsolete("Renamed to Access.SessionHost (requires Scope.MultiplayerSession). For player-owned access use Access.Player.")]
        Host = 1,

        /// <summary>
        ///     Any member of the session, including the host (and service accounts),
        ///     may invoke the function. Requires <see cref="Scope.MultiplayerSession"/>.
        /// </summary>
        SessionMember = 2,

        /// <summary>
        ///     Deprecated alias of <see cref="SessionMember"/> (value <c>2</c>). The
        ///     behaviour is now strict: declaring it on a <see cref="Scope.Player"/>
        ///     or non-scoped class is rejected when the module is deployed. For
        ///     player-owned access use <see cref="Player"/>.
        /// </summary>
        [Obsolete("Renamed to Access.SessionMember (requires Scope.MultiplayerSession). For player-owned access use Access.Player.")]
        Members = 2,

        /// <summary>
        ///     Anyone may invoke the function, regardless of session membership or
        ///     ownership. Valid for any scope.
        /// </summary>
        Global = 3,

        /// <summary>
        ///     Only the owning player (and service accounts) may invoke the function.
        ///     Requires <see cref="Scope.Player"/>.
        /// </summary>
        Player = 4,

        /// <summary>
        ///     Sentinel default meaning "no access level was specified". The
        ///     effective access is resolved from the class <see cref="Scope"/> at
        ///     module load: non-scoped resolves to <see cref="Global"/>,
        ///     <see cref="Scope.Player"/> resolves to <see cref="Player"/>, and
        ///     <see cref="Scope.MultiplayerSession"/> resolves to
        ///     <see cref="SessionMember"/>.
        /// </summary>
        Unspecified = 5,
    }
}
