using System;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// Marks a <c>public partial</c> class as the client-side binding for a cloud module class. The Cloud Code
    /// source generator emits a matching partial declaration containing one Task returning method per
    /// <c>[CloudCodeFunction]</c> on the referenced cloud class, where each method invokes the corresponding
    /// cloud endpoint through <c>ICloudCodeService.CallModuleEndpointAsync</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The decorated class must be <c>public partial</c>. Non-partial or non-public declarations are skipped
    /// by the generator. Only one <c>[CloudModuleClient]</c> attribute is permitted per class - each
    /// binding maps to exactly one cloud class.
    /// </para>
    /// <para>
    /// All bindings within a single client assembly must reference cloud classes from the same cloud module
    /// assembly. Mixing bindings across multiple cloud assemblies is not supported..
    /// </para>
    /// <para>Example:</para>
    /// <code>
    /// // Cloud side:
    /// public class Greeter
    /// {
    ///     [CloudCodeFunction("Hello")]
    ///     public string Hello(string name) =&gt; $"Hello, {name}!";
    /// }
    ///
    /// // Client side:
    /// [CloudModuleClient(typeof(Greeter))]
    /// public partial class GreeterClient {}
    ///
    /// // Usage:
    /// var client = new GreeterClient();
    /// var greeting = await client.Hello("world");
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CloudModuleClientAttribute : Attribute
    {
        /// <summary>
        /// Creates a new <see cref="CloudModuleClientAttribute"/> targeting the given cloud module class.
        /// </summary>
        /// <param name="cloudClass">
        /// The cloud module class whose <c>[CloudCodeFunction]</c> methods should be projected as client bindings.
        /// Must be a concrete user-defined class - generics, interfaces, structs, enums, abstract classes, static
        /// classes, and BCL/framework types (e.g. <c>System.Random</c>) are rejected.
        /// </param>
        public CloudModuleClientAttribute(Type cloudClass)
        {
        }
    }
}
