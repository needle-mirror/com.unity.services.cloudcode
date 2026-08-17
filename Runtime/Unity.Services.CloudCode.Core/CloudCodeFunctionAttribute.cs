using System;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    /// The <c>CloudCodeFunctionAttribute</c> is used to identify
    /// an entrypoint within a Cloud Code module. When used on a
    /// method, it makes that method callable through an API request.
    /// </summary>
    /// <example>
    /// <para>
    /// The following snippet makes the <c>MyCloudModule.SayHello</c>
    /// method callable via the Cloud Code API as "SayHello".
    /// <c>
    /// public class MyCloudModule
    /// {
    ///     [CloudCodeFunction("SayHello")]
    ///     public string SayHello(string name)
    ///     {
    ///         return $"Hello, {name}!";
    ///     }
    /// }
    /// </c>
    /// </para>
    /// <para>
    /// The name used in the <c>CloudCodeFunctionAttribute</c>
    /// does not need to match the method name. The following
    /// snippet makes the same method callable as "Greet".
    /// <c>
    /// public class MyCloudModule
    /// {
    ///     [CloudCodeFunction("Greet")]
    ///     public string SayHello(string name)
    ///     {
    ///         return $"Hello, {name}!";
    ///     }
    /// }
    /// </c>
    /// </para>
    /// <para>
    /// When no function name is supplied, the method name is used.
    /// The following snippet makes the method callable as "SayHello".
    /// <c>
    /// public class MyCloudModule
    /// {
    ///     [CloudCodeFunction]
    ///     public string SayHello(string name)
    ///     {
    ///         return $"Hello, {name}!";
    ///     }
    /// }
    /// </c>
    /// </para>
    /// <para>
    /// An <see cref="Access"/> level can be supplied on its own to
    /// restrict who may invoke the function while still inferring the
    /// function name from the method. The following snippet makes the
    /// method callable as "SayHello", limited to the session host and
    /// service accounts. <see cref="Access.SessionHost"/> requires the
    /// class to declare <c>[StateScope(Scope.MultiplayerSession)]</c>.
    /// <c>
    /// [StateScope(Scope.MultiplayerSession)]
    /// public class MyCloudModule
    /// {
    ///     [CloudCodeFunction(Access.SessionHost)]
    ///     public string SayHello(string name)
    ///     {
    ///         return $"Hello, {name}!";
    ///     }
    /// }
    /// </c>
    /// The following snippet makes the method callable as "Greet". Because
    /// <see cref="Access"/> is omitted it defaults to
    /// <see cref="Access.Unspecified"/>, whose effective access is resolved from
    /// the class scope when the module loads. This class is non-scoped, so it
    /// resolves to <see cref="Access.Global"/> and anyone may invoke it.
    /// <c>
    /// public class MyCloudModule
    /// {
    ///     [CloudCodeFunction]
    ///     public string Greet()
    ///     {
    ///         return "Greetings!";
    ///     }
    /// }
    /// </c>
    /// </para>
    /// <para>
    /// A function name and an access level can also be combined. The
    /// following snippet makes the method callable as "Greet", limited
    /// to session members and service accounts. <see cref="Access.SessionMember"/>
    /// requires the class to declare <c>[StateScope(Scope.MultiplayerSession)]</c>.
    /// <c>
    /// [StateScope(Scope.MultiplayerSession)]
    /// public class MyCloudModule
    /// {
    ///     [CloudCodeFunction("Greet", Access.SessionMember)]
    ///     public string SayHello(string name)
    ///     {
    ///         return $"Hello, {name}!";
    ///     }
    /// }
    /// </c>
    /// </para>
    /// </example>
    /// <seealso cref="Unity.Services.CloudCode.Core.Access"/>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class CloudCodeFunctionAttribute : Attribute
    {
        /// <summary>
        /// The name of the Cloud Code callable function.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// The access control level for this function.
        /// </summary>
        public Access Access { get; private set; }

        /// <summary>
        /// Expose a method as a callable function
        /// with an explicit name and access level.
        /// </summary>
        /// <param name="functionName">
        /// <para>
        /// The name of the callable function.
        /// </para>
        /// <para>
        /// The name does not need to match the method name.
        /// </para>
        /// <para>
        /// The name must be unique across all methods
        /// declared in the same class. Two methods in the
        /// same class cannot share the same function name.
        /// </para>
        /// <para>
        /// When <see langword="null"/>, the method
        /// name is used as the function name.
        /// </para>
        /// </param>
        /// <param name="access">
        /// The access control level. Defaults to <see cref="Access.Unspecified"/>,
        /// whose effective access is resolved from the class scope at module load.
        /// </param>
        public CloudCodeFunctionAttribute(string functionName, Access access = Access.Unspecified)
        {
            Name = functionName;
            Access = access;
        }

        /// <summary>
        /// Expose a method as a callable function with an explicit name.
        /// <br/>
        /// Access level defaults to <see cref="Access.Unspecified"/>, whose
        /// effective access is resolved from the class scope at module load.
        /// </summary>
        /// <param name="functionName">
        /// <para>
        /// The name of the callable function.
        /// </para>
        /// <para>
        /// The name does not need to match the method name.
        /// </para>
        /// <para>
        /// The name must be unique across all methods
        /// declared in the same class. Two methods in the
        /// same class cannot share the same function name.
        /// </para>
        /// <para>
        /// When <see langword="null"/>, the method
        /// name is used as the function name.
        /// </para>
        /// </param>
        // ReSharper disable once RedundantOverload.Global
        public CloudCodeFunctionAttribute(string functionName) : this(functionName, Access.Unspecified)
        {}

        /// <summary>
        /// Expose a method as a callable function using
        /// the method name and specified access level.
        /// </summary>
        /// <param name="access">
        /// The access control level. Defaults to <see cref="Access.Unspecified"/>,
        /// whose effective access is resolved from the class scope at module load.
        /// </param>
        public CloudCodeFunctionAttribute(Access access = Access.Unspecified) : this(null, access)
        {}
    }
}
