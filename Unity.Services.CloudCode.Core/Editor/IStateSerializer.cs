namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    /// The Cloud Code serialization interface for persisting and restoring module instance state.
    /// </summary>
    /// <remarks>
    /// Implementations of this interface are responsible for serializing and deserializing the state
    /// of Cloud Code module classes. Module developers should implement this interface if they want to bypass the default
    /// Cloud Code serializer and use custom behaviour.
    /// </remarks>
    public interface IStateSerializer
    {
        /// <summary>
        /// Serializes the current state of the type into a byte array.
        /// </summary>
        /// <returns>
        /// A byte array containing the serialized state of the instance. The returned array should
        /// contain all necessary data to fully reconstruct the instance state when passed to
        /// <see cref="OnDeserialize"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Implementations should ensure that all mutable state that needs to be preserved is included
        /// in the serialized output.
        /// </para>
        /// <para>
        /// The serialization format must be consistent with the implementation of <see cref="OnDeserialize"/>,
        /// as the same format will be used to restore the state later.
        /// </para>
        /// <para>
        /// If the instance has no state to persist, an empty byte array may be returned.
        /// </para>
        /// </remarks>
        byte[] OnSerialize();

        /// <summary>
        /// Deserializes the provided byte array and populates the type's fields.
        /// </summary>
        /// <param name="input">
        /// A byte array containing previously serialized state data, typically produced by
        /// <see cref="OnSerialize"/>. This array should be used to restore the instance to its
        /// previous state.
        /// </param>
        /// <remarks>
        /// <para>
        /// The implementation should parse the input byte array and update all instance fields and
        /// properties to match the state that was previously serialized.
        /// </para>
        /// <para>
        /// Implementations must handle the same serialization format that is produced by the
        /// <see cref="OnSerialize"/> method of the same implementation.
        /// </para>
        /// <para>
        /// If the input array is empty or represents no state, the implementation should leave
        /// the instance in its default state.
        /// </para>
        /// <para>
        /// Implementations should be prepared to handle deserialization of state that may have been
        /// serialized by previous versions of the code, if backward compatibility is required.
        /// </para>
        /// </remarks>
        void OnDeserialize(byte[] input);
    }
}
