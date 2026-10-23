using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    static class LogValue
    {
        internal static JToken LoadWithoutDateCoercion(JsonReader reader)
        {
            var previous = reader.DateParseHandling;
            reader.DateParseHandling = DateParseHandling.None;

            try
            {
                return JToken.Load(reader);
            }
            finally
            {
                reader.DateParseHandling = previous;
            }
        }

        internal static string FormatDate(object value)
        {
            if (value is DateTime dateTime)
            {
                return dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffK", CultureInfo.InvariantCulture);
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                return dateTimeOffset.ToString("yyyy-MM-ddTHH:mm:ss.fffK", CultureInfo.InvariantCulture);
            }

            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        internal static string Stringify(JToken token)
        {
            if (token == null
                || token.Type == JTokenType.Null
                || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            if (!(token is JValue value))
            {
                return token.ToString(Formatting.None);
            }

            if (value.Value == null)
            {
                return null;
            }

            if (value.Value is bool boolean)
            {
                return boolean ? "true" : "false";
            }

            return Convert.ToString(value.Value, CultureInfo.InvariantCulture);
        }
    }

    class LogBodyConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(string);
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.String)
            {
                return (string)reader.Value;
            }

            if (reader.TokenType == JsonToken.Date)
            {
                return LogValue.FormatDate(reader.Value);
            }

            return LogValue.Stringify(LogValue.LoadWithoutDateCoercion(reader));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }

    class LogAttributesConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Dictionary<string, string>);
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer)
        {
            var token = LogValue.LoadWithoutDateCoercion(reader);

            if (token == null || token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return null;
            }

            var attributes = new Dictionary<string, string>();

            if (token is JObject map)
            {
                ReadProperties(map, attributes);
                return attributes;
            }

            if (token is JArray entries)
            {
                foreach (var entry in entries)
                {
                    ReadEntry(entry as JObject, attributes);
                }
            }

            return attributes;
        }

        static void ReadEntry(JObject entry, Dictionary<string, string> attributes)
        {
            if (entry == null)
            {
                return;
            }

            var key = entry["key"];

            if (key != null && entry["value"] != null)
            {
                attributes[LogValue.Stringify(key) ?? string.Empty] =
                    LogValue.Stringify(entry["value"]);
                return;
            }

            ReadProperties(entry, attributes);
        }

        static void ReadProperties(JObject source, Dictionary<string, string> attributes)
        {
            foreach (var property in source.Properties())
            {
                attributes[property.Name] = LogValue.Stringify(property.Value);
            }
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
}
