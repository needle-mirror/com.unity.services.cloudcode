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
using System.Linq;
using UnityEngine.Scripting;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Unity.Services.CloudCode.Authoring.Client.Http;



namespace Unity.Services.CloudCode.Authoring.Client.Models
{
    /// <summary>
    /// CloudCodePublishScriptResponse model
    /// </summary>
    [Preserve]
    [DataContract(Name = "cloud-code.publish-script-response")]
    internal class CloudCodePublishScriptResponse
    {
        /// <summary>
        /// Creates an instance of CloudCodePublishScriptResponse.
        /// </summary>
        /// <param name="version">The version id of the newly published Script</param>
        [Preserve]
        public CloudCodePublishScriptResponse(int version)
        {
            Version = version;
        }

        /// <summary>
        /// The version id of the newly published Script
        /// </summary>
        [Preserve]
        [DataMember(Name = "version", IsRequired = true, EmitDefaultValue = true)]
        public int Version{ get; }
    
        /// <summary>
        /// Formats a CloudCodePublishScriptResponse into a string of key-value pairs for use as a path parameter.
        /// </summary>
        /// <returns>Returns a string representation of the key-value pairs.</returns>
        internal string SerializeAsPathParam()
        {
            var serializedModel = "";

            serializedModel += "version," + Version.ToString();
            return serializedModel;
        }

        /// <summary>
        /// Returns a CloudCodePublishScriptResponse as a dictionary of key-value pairs for use as a query parameter.
        /// </summary>
        /// <returns>Returns a dictionary of string key-value pairs.</returns>
        internal Dictionary<string, string> GetAsQueryParam()
        {
            var dictionary = new Dictionary<string, string>();

            var versionStringValue = Version.ToString();
            dictionary.Add("version", versionStringValue);
            
            return dictionary;
        }
    }
}
