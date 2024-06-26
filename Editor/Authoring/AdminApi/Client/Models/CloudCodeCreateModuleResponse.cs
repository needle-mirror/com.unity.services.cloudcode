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
    /// CloudCodeCreateModuleResponse model
    /// </summary>
    [Preserve]
    [DataContract(Name = "cloud-code.create-module-response")]
    internal class CloudCodeCreateModuleResponse
    {
        /// <summary>
        /// Creates an instance of CloudCodeCreateModuleResponse.
        /// </summary>
        /// <param name="dateCreated">Date time in ISO 8601 format. Null if there is no associated value.</param>
        [Preserve]
        public CloudCodeCreateModuleResponse(DateTime? dateCreated = default)
        {
            DateCreated = dateCreated;
        }

        /// <summary>
        /// Date time in ISO 8601 format. Null if there is no associated value.
        /// </summary>
        [Preserve]
        [DataMember(Name = "dateCreated", EmitDefaultValue = false)]
        public DateTime? DateCreated{ get; }
    
        /// <summary>
        /// Formats a CloudCodeCreateModuleResponse into a string of key-value pairs for use as a path parameter.
        /// </summary>
        /// <returns>Returns a string representation of the key-value pairs.</returns>
        internal string SerializeAsPathParam()
        {
            var serializedModel = "";

            if (DateCreated != null)
            {
                serializedModel += "dateCreated," + DateCreated.ToString();
            }
            return serializedModel;
        }

        /// <summary>
        /// Returns a CloudCodeCreateModuleResponse as a dictionary of key-value pairs for use as a query parameter.
        /// </summary>
        /// <returns>Returns a dictionary of string key-value pairs.</returns>
        internal Dictionary<string, string> GetAsQueryParam()
        {
            var dictionary = new Dictionary<string, string>();

            if (DateCreated != null)
            {
                var dateCreatedStringValue = DateCreated.ToString();
                dictionary.Add("dateCreated", dateCreatedStringValue);
            }
            
            return dictionary;
        }
    }
}
