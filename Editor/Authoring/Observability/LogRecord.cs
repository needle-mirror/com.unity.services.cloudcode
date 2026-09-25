using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    [DataContract(Name = "log-record")]
    [Preserve]
    class LogRecord
    {
        [DataMember(Name = "timestamp", EmitDefaultValue = false)]
        [Preserve]
        public string Timestamp { get; set; }

        [DataMember(Name = "severityText", EmitDefaultValue = false)]
        [Preserve]
        public string SeverityText { get; set; }

        [DataMember(Name = "severityNumber", EmitDefaultValue = false)]
        [Preserve]
        public int? SeverityNumber { get; set; }

        [DataMember(Name = "body", EmitDefaultValue = false)]
        [JsonConverter(typeof(LogBodyConverter))]
        [Preserve]
        public string Body { get; set; }

        [DataMember(Name = "resourceAttributes", EmitDefaultValue = false)]
        [JsonConverter(typeof(LogAttributesConverter))]
        [Preserve]
        public Dictionary<string, string> ResourceAttributes { get; set; }

        [DataMember(Name = "logAttributes", EmitDefaultValue = false)]
        [JsonConverter(typeof(LogAttributesConverter))]
        [Preserve]
        public Dictionary<string, string> LogAttributes { get; set; }
    }
}
