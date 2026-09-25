using System;
using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    ///     <para>
    ///     Restricts which callers may invoke a Cloud Behaviour endpoint..
    ///     </para>
    ///
    ///     <para>
    ///     When the attribute is omitted the endpoint's access is resolved from the class
    ///     <see cref="Scope"/> when the module loads: non-scoped resolves to
    ///     <see cref="Access.Global"/>, <see cref="Scope.Player"/> resolves to
    ///     <see cref="Access.Player"/>, and <see cref="Scope.MultiplayerSession"/> resolves to
    ///     <see cref="Access.SessionMember"/>.
    ///     </para>
    /// </summary>
    /// <seealso cref="Unity.Services.CloudCode.Core.Access"/>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class CloudCodeAccessAttribute : Attribute
    {
        /// <summary>
        /// The access control level for this endpoint.
        /// </summary>
        public Access Access { get; }

        /// <summary>
        /// Restricts the endpoint to the given access level.
        /// </summary>
        /// <param name="access">
        /// The access control level. Defaults to <see cref="Access.Unspecified"/>, whose effective
        /// access is resolved from the class scope at module load.
        /// </param>
        public CloudCodeAccessAttribute(Access access = Access.Unspecified)
        {
            Access = access;
        }
    }
}
