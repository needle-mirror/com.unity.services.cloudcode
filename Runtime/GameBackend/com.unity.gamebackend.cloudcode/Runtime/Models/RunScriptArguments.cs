using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Unity.GameBackend.CloudCode.Http;



namespace Unity.GameBackend.CloudCode.Models
{
    /// <summary>
    /// RunScriptArguments model
    /// <param name="params">Object containing Key-Value pairs that map on to the parameter definitions for the script. Parameters are required according to the definition.</param>
    /// </summary>

    [Preserve]
    [DataContract(Name = "run_script_arguments")]
    public class RunScriptArguments
    {
        /// <summary>
        /// Creates an instance of RunScriptArguments.
        /// </summary>
        /// <param name="params">Object containing Key-Value pairs that map on to the parameter definitions for the script. Parameters are required according to the definition.</param>
        [Preserve]
        public RunScriptArguments(Dictionary<string, object> _params = default)
        {
            Params = new JsonObject(_params);
        }

    
        /// <summary>
        /// Object containing Key-Value pairs that map on to the parameter definitions for the script. Parameters are required according to the definition.
        /// </summary>
        [Preserve]
        [JsonConverter(typeof(JsonObjectConverter))]
        [DataMember(Name = "params", EmitDefaultValue = false)]
        public JsonObject Params{ get; }
    
    }
}

