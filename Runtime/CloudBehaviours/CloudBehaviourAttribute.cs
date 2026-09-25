using System;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Marks a partial class to generate the cloud behaviour client endpoints
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class CloudBehaviourAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of <see cref="CloudBehaviourAttribute"/>.
        /// </summary>
        /// <param name="type">The cloud behaviour type to associate with this class.</param>
        public CloudBehaviourAttribute(Type type)
        {
        }
    }
}
