using System.Runtime.Serialization;
using Unity.Services.CloudCode.Internal.Models;

namespace Unity.Services.CloudCode.Models
{
    /// <summary>
    /// A class representing the scope being used when executing a Cloud Code module function.
    /// </summary>
    public class CloudCodeModuleScope
    {
        /// <summary>
        /// The scope type for the Cloud Code module.
        /// </summary>
        public ScopeType Type { get; }

        /// <summary>
        /// The scope ID for the Cloud Code module.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Initializes a new <see cref="CloudCodeModuleScope"/> with the specified scope type and ID.
        /// </summary>
        /// <param name="type">The scope type for the module.</param>
        /// <param name="id">The scope identifier.</param>
        public CloudCodeModuleScope(ScopeType type, string id)
        {
            Type = type;
            Id = id;
        }

        /// <summary>
        /// Converts the public ScopeType to the internal RunModuleArgumentsScope.TypeOptions.
        /// </summary>
        /// <returns>The equivalent RunModuleArgumentsScope.TypeOptions value.</returns>
        internal RunModuleArgumentsScope.TypeOptions ToInternalType()
        {
            return (RunModuleArgumentsScope.TypeOptions)Type;
        }
    }

    /// <summary>
    /// The level of scope to use when executing a Cloud Code module function. Must match the StateScope class attribute.
    /// </summary>
    public enum ScopeType
    {
        /// <summary>
        /// Session scope.
        /// </summary>
        MultiplayerSession = RunModuleArgumentsScope.TypeOptions.MultiplayerSession,

        /// <summary>
        /// Player scope.
        /// </summary>
        Player = RunModuleArgumentsScope.TypeOptions.Player
    }
}
