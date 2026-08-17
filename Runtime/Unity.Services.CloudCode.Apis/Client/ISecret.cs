using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Apis
{
    /// <summary>
    /// JSON converter that masks secret values during serialization.
    /// </summary>
    public class MaskedSecretConverter : JsonConverter<Secret>
    {
        /// <summary>
        /// Writes the secret value as masked asterisks.
        /// </summary>
        /// <param name="writer">The JSON writer.</param>
        /// <param name="value">The secret value to serialize.</param>
        /// <param name="serializer">The JSON serializer.</param>
        public override void WriteJson(JsonWriter writer, Secret value, JsonSerializer serializer)
        {
            writer.WriteValue("****");
        }

        /// <summary>
        /// Reads a secret value from JSON.
        /// </summary>
        /// <param name="reader">The JSON reader.</param>
        /// <param name="objectType">The target object type.</param>
        /// <param name="existingValue">The existing secret value.</param>
        /// <param name="hasExistingValue">Whether an existing value is present.</param>
        /// <param name="serializer">The JSON serializer.</param>
        /// <returns>The deserialized secret value.</returns>
        public override Secret ReadJson(JsonReader reader, Type objectType, Secret existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return (Secret)reader.Value;
        }
    }

    /// <summary>
    /// Represents a secret value retrieved from the Secret Manager service.
    /// </summary>
    [JsonConverter(typeof(MaskedSecretConverter))]
    public class Secret
    {
        private readonly string _value;

        /// <summary>
        /// Gets the secret value.
        /// </summary>
        public string Value => _value;

        /// <summary>
        /// Initializes a new instance of <see cref="Secret"/> with the specified value.
        /// </summary>
        /// <param name="value">The secret value.</param>
        public Secret(string value)
        {
            _value = value;
        }

        /// <summary>
        /// Returns a masked string representation of the secret.
        /// </summary>
        /// <returns>A masked string (****) to prevent accidental secret exposure in logs.</returns>
        public override string ToString()
        {
            return "****";
        }
    }

    /// <summary>
    /// Client for retrieving secrets from the Secret Manager service.
    /// </summary>
    public interface ISecretClient
    {
        /// <summary>
        /// Retrieves a secret value by key from the execution environment.
        /// </summary>
        /// <param name="executionContext">The execution context for the request.</param>
        /// <param name="secretKey">The key of the secret to retrieve.</param>
        /// <returns>A task that returns the secret value.</returns>
        public Task<Secret> GetSecret(IExecutionContext executionContext, string secretKey);
    }
}
