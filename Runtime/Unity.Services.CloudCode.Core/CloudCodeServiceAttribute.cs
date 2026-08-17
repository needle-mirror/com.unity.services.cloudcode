using System;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     The CloudCodeServiceAttribute is used to identify a function parameter as an injected service, as opposed to a
    ///     request parameter. This allows tooling and source generators to distinguish between services registered via
    ///     <see cref="ICloudCodeSetup"/> and parameters provided by the caller at invocation time.
    /// </summary>
    /// <example>
    ///     The following snippet marks <c>myService</c> as an injected service and <c>name</c> as a caller-provided parameter.
    /// <c>
    /// public class MyModule
    /// {
    ///     [CloudCodeFunction("SayHello")]
    ///     public string SayHello([CloudCodeService] IMyService myService, string name)
    ///     {
    ///         return myService.Greet(name);
    ///     }
    /// }
    /// </c>
    /// </example>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    public sealed class CloudCodeServiceAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CloudCodeServiceAttribute"/>.
        /// </summary>
        public CloudCodeServiceAttribute() {}
    }
}
