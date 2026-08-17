using System;
using System.Runtime.Serialization;
using Unity.Services.CloudCode.Internal.Models;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// A class representing the scope being used when executing a Cloud Code function.
    /// </summary>
    public class CloudCodeScope : IEquatable<CloudCodeScope>
    {
        /// <summary>
        /// The scope type for the Cloud Code execution.
        /// </summary>
        public ScopeType Type { get; }

        /// <summary>
        /// The scope ID for the Cloud Code execution.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Initializes a new <see cref="CloudCodeScope"/> with the specified scope type and ID.
        /// </summary>
        /// <param name="type">The scope type.</param>
        /// <param name="id">The scope identifier.</param>
        public CloudCodeScope(ScopeType type, string id)
        {
            Type = type;
            Id = id;
        }

        /// <summary>
        /// Determines whether another scope targets the same type and identifier as this one.
        /// </summary>
        /// <param name="other">The scope to compare against.</param>
        /// <returns><see langword="true"/> when both scopes target the same type and identifier.</returns>
        public bool Equals(CloudCodeScope other)
        {
            if (other is null)
                return false;

            if (ReferenceEquals(this, other))
                return true;

            if (other.GetType() != GetType())
                return false;

            return Type == other.Type && string.Equals(Id, other.Id, StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as CloudCodeScope);

        /// <inheritdoc/>
        public override int GetHashCode() => HashCode.Combine((int)Type, Id);

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
    /// The level of scope to use when executing a Cloud Code function. Must match the StateScope class attribute.
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
