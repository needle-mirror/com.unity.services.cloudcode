using System;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     Instructs the default Cloud Code serializer not to serialize the field or property value.
    ///     Use this attribute to exclude sensitive, temporary, or computed data from state persistence.
    /// </summary>
    /// <example>
    ///  <para>
    ///     The following snippet demonstrates excluding fields from state persistence
    ///  </para>
    ///  <code>
    /// [StateScope(Scope.Player)]
    /// public class PlayerState
    /// {
    ///     // This will NOT be persisted
    ///     [CloudCodeIgnoreProperty]
    ///     public string SecretString { get; set; }
    ///
    ///     // This will NOT be persisted
    ///     [CloudCodeIgnoreProperty]
    ///     public string _anotherSecret { get; set;}
    /// }
    ///  </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    public sealed class CloudCodeIgnorePropertyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CloudCodeIgnorePropertyAttribute"/>.
        /// </summary>
        public CloudCodeIgnorePropertyAttribute() {}
    }
}
