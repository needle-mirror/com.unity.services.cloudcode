//-----------------------------------------------------------------------------
// <auto-generated>
//     This file was generated by the C# SDK Code Generator.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//-----------------------------------------------------------------------------


using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Unity.Services.CloudCode.Internal.Http;



namespace Unity.Services.CloudCode.Internal.Models
{
    /// <summary>
    /// RunScriptResponse model
    /// </summary>
    [Preserve]
    [DataContract(Name = "RunScriptResponse")]
    internal class RunScriptResponse
    {
        /// <summary>
        /// Creates an instance of RunScriptResponse.
        /// </summary>
        /// <param name="output">output param</param>
        [Preserve]
        public RunScriptResponse(object output = default)
        {
            Output = output;
        }

        /// <summary>
        /// 
        /// </summary>
        [Preserve]
        [DataMember(Name = "output", EmitDefaultValue = false)]
        public object Output{ get; }
    
    }
}
