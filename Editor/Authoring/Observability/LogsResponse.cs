using System.Collections.Generic;
using System.Runtime.Serialization;
using UnityEngine.Scripting;

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    [DataContract(Name = "logs-response")]
    [Preserve]
    class LogsResponse
    {
        [DataMember(Name = "offset", EmitDefaultValue = false)]
        [Preserve]
        public int Offset { get; set; }

        [DataMember(Name = "limit", EmitDefaultValue = false)]
        [Preserve]
        public int Limit { get; set; }

        [DataMember(Name = "total", EmitDefaultValue = false)]
        [Preserve]
        public int Total { get; set; }

        [DataMember(Name = "results", EmitDefaultValue = false)]
        [Preserve]
        public List<LogRecord> Results { get; set; }
    }
}
