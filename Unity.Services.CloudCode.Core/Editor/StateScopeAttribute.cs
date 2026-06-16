using System;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     An enum containing the possible levels of scope for the class state.
    /// </summary>
    public enum Scope {
        /// <summary>
        ///     Indicates that the class has state scoped to a multiplayer session.
        /// </summary>
        MultiplayerSession,
        /// <summary>
        ///     Indicates that the class has state scoped to a player
        /// </summary>
        Player,
    }

    /// <summary>
    ///     The StateScopeAttribute is used to indicate that a given class is stateful, and class variables will retain
    ///     their state across multiple function invocations within the defined scope.
    /// </summary>
    /// <example>
    ///     The following snippet will allow you to increment and decrement a value that is retained across multiple function calls.
    ///     <code>
    /// [StateScope(Scope.MultiplayerSession)]
    /// public class Example
    /// {
    ///      public int Value = 0;
    ///
    ///     [CloudCodeFunction("Increment")]
    ///     public int Increment()
    ///     {
    ///         Value++;
    ///         return Value;
    ///     }
    ///
    ///     [CloudCodeFunction("Decrement")]
    ///     public int Decrement()
    ///     {
    ///         Value--;
    ///         return Value;
    ///     }
    ///  }
    ///  </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Class)]
    public class StateScopeAttribute : Attribute
    {
        /// <summary>
        ///     The level of scope to apply to this class.
        /// </summary>
        public Scope StateScope { get; private set; }

        /// <summary>
        ///     Indicate that a class is to have state at a given scope.
        /// </summary>
        /// <param name="scope">The level of scope to apply to this class.</param>
        public StateScopeAttribute(Scope scope)
        {
            StateScope = scope;
        }
    }
}