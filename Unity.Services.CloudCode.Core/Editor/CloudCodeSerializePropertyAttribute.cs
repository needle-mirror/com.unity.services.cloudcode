using System;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     Instructs the default Cloud Code serializer to serialize the field or property value.
    ///     Use this attribute to include data in state persistence.
    /// </summary>
    /// <example>
    ///     The following snippet demonstrates include fields in state persistence
    ///     <code>
    /// [StateScope(Scope.Player)]
    /// public class PlayerState
    /// {
    ///     [CloudCodeSerializeProperty]
    ///     private int Score { get; set; }
    ///
    ///     [CloudCodeSerializeProperty]
    ///     private string _name;
    /// }
    ///  </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = false)]
    public sealed class CloudCodeSerializePropertyAttribute : Attribute
    {
        public CloudCodeSerializePropertyAttribute() {}
    }
}