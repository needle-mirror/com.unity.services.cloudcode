using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Apis
{
    public class MaskedSecretConverter : JsonConverter<Secret>
    {
        public override void WriteJson(JsonWriter writer, Secret value, JsonSerializer serializer)
        {
            writer.WriteValue("****");
        }

        public override Secret ReadJson(JsonReader reader, Type objectType, Secret existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            return (Secret)reader.Value;
        }
    }

    [JsonConverter(typeof(MaskedSecretConverter))]
    public class Secret
    {
        private readonly string _value;

        public string Value => _value;

        public Secret(string value)
        {
            _value = value;
        }

        public override string ToString()
        {
            return "****";
        }
    }

    public interface ISecretClient
    {
        public Task<Secret> GetSecret(IExecutionContext executionContext, string secretKey);
    }
}
