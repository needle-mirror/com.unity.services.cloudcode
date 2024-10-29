using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace Unity.Services.CloudCode.Internal.Http
{
    static class IsolatedJsonConvert
    {
        [DebuggerStepThrough]
        public static string SerializeObject(object value) => SerializeObject(value, null, null);

        [DebuggerStepThrough]
        public static string SerializeObject(object value, JsonSerializerSettings settings) => SerializeObject(value, null, settings);

        [DebuggerStepThrough]
        public static string SerializeObject(object value, Type type, JsonSerializerSettings settings)
        {
            JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
            return SerializeObjectInternal(value, type, jsonSerializer);
        }

        static string SerializeObjectInternal(
            object value,
            Type type,
            JsonSerializer jsonSerializer)
        {
            StringWriter stringWriter = new StringWriter(new StringBuilder(256), CultureInfo.InvariantCulture);
            using (JsonTextWriter jsonTextWriter = new JsonTextWriter(stringWriter))
            {
                jsonTextWriter.Formatting = jsonSerializer.Formatting;
                jsonSerializer.Serialize(jsonTextWriter, value, type);
            }

            return stringWriter.ToString();
        }

        [DebuggerStepThrough]
        public static object DeserializeObject(string value, Type type) => DeserializeObject(value, type, null);

        [DebuggerStepThrough]
        public static T DeserializeObject<T>(string value, JsonSerializerSettings settings) => (T)DeserializeObject(value, typeof(T), settings);

        public static object DeserializeObject(
            string value,
            Type type,
            JsonSerializerSettings settings)
        {
            JsonSerializer jsonSerializer = JsonSerializer.Create(settings);
            using (JsonTextReader reader = new JsonTextReader(new StringReader(value)))
                return jsonSerializer.Deserialize(reader, type);
        }
    }
}
