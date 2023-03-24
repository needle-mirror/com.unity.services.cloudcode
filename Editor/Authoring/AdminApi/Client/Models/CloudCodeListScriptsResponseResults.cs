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
    /// CloudCodeListScriptsResponseResults model
    /// </summary>
    [Preserve]
    [DataContract(Name = "cloud_code_list_scripts_response_results")]
    internal class CloudCodeListScriptsResponseResults
    {
        /// <summary>
        /// Creates an instance of CloudCodeListScriptsResponseResults.
        /// </summary>
        /// <param name="type">The type of the Script</param>
        /// <param name="name">The name of the Script</param>
        /// <param name="language">The language of the Script</param>
        /// <param name="published">A flag indicating if the Script is published</param>
        /// <param name="lastPublishedDate">Date time in ISO 8601 format. Null if there is no associated value.</param>
        /// <param name="lastPublishedVersion">The version id of the last published version of the Script</param>
        [Preserve]
        public CloudCodeListScriptsResponseResults(TypeOptions type = default, string name = default, LanguageOptions language = LanguageOptions.JS, bool published = default, DateTime? lastPublishedDate = default, int? lastPublishedVersion = default)
        {
            Type = type;
            Name = name;
            Language = language;
            Published = published;
            LastPublishedDate = lastPublishedDate;
            LastPublishedVersion = lastPublishedVersion;
        }

        /// <summary>
        /// The type of the Script
        /// </summary>
        [Preserve]
        [JsonConverter(typeof(StringEnumConverter))]
        [DataMember(Name = "type", EmitDefaultValue = false)]
        public TypeOptions Type{ get; }
        
        /// <summary>
        /// The name of the Script
        /// </summary>
        [Preserve]
        [DataMember(Name = "name", EmitDefaultValue = false)]
        public string Name{ get; }
        
        /// <summary>
        /// The language of the Script
        /// </summary>
        [Preserve]
        [JsonConverter(typeof(StringEnumConverter))]
        [DataMember(Name = "language", EmitDefaultValue = false)]
        public LanguageOptions Language{ get; }
        
        /// <summary>
        /// A flag indicating if the Script is published
        /// </summary>
        [Preserve]
        [DataMember(Name = "published", EmitDefaultValue = true)]
        public bool Published{ get; }
        
        /// <summary>
        /// Date time in ISO 8601 format. Null if there is no associated value.
        /// </summary>
        [Preserve]
        [DataMember(Name = "lastPublishedDate", EmitDefaultValue = false)]
        public DateTime? LastPublishedDate{ get; }
        
        /// <summary>
        /// The version id of the last published version of the Script
        /// </summary>
        [Preserve]
        [DataMember(Name = "lastPublishedVersion", EmitDefaultValue = false)]
        public int? LastPublishedVersion{ get; }
    
        /// <summary>
        /// The type of the Script
        /// </summary>
        /// <value>The type of the Script</value>
        [Preserve]
        [JsonConverter(typeof(StringEnumConverter))]
        public enum TypeOptions
        {
            /// <summary>
            /// Enum API for value: API
            /// </summary>
            [EnumMember(Value = "API")]
            API = 1,
            /// <summary>
            /// Enum MODULE for value: MODULE
            /// </summary>
            [EnumMember(Value = "MODULE")]
            MODULE = 2
        }

        /// <summary>
        /// The language of the Script
        /// </summary>
        /// <value>The language of the Script</value>
        [Preserve]
        [JsonConverter(typeof(StringEnumConverter))]
        public enum LanguageOptions
        {
            /// <summary>
            /// Enum JS for value: JS
            /// </summary>
            [EnumMember(Value = "JS")]
            JS = 1
        }

        /// <summary>
        /// Formats a CloudCodeListScriptsResponseResults into a string of key-value pairs for use as a path parameter.
        /// </summary>
        /// <returns>Returns a string representation of the key-value pairs.</returns>
        internal string SerializeAsPathParam()
        {
            var serializedModel = "";

            serializedModel += "type," + Type + ",";
            if (Name != null)
            {
                serializedModel += "name," + Name + ",";
            }
            serializedModel += "language," + Language + ",";
            serializedModel += "published," + Published.ToString() + ",";
            if (LastPublishedDate != null)
            {
                serializedModel += "lastPublishedDate," + LastPublishedDate.ToString() + ",";
            }
            if (LastPublishedVersion != null)
            {
                serializedModel += "lastPublishedVersion," + LastPublishedVersion.ToString();
            }
            return serializedModel;
        }

        /// <summary>
        /// Returns a CloudCodeListScriptsResponseResults as a dictionary of key-value pairs for use as a query parameter.
        /// </summary>
        /// <returns>Returns a dictionary of string key-value pairs.</returns>
        internal Dictionary<string, string> GetAsQueryParam()
        {
            var dictionary = new Dictionary<string, string>();

            var typeStringValue = Type.ToString();
            dictionary.Add("type", typeStringValue);
            
            if (Name != null)
            {
                var nameStringValue = Name.ToString();
                dictionary.Add("name", nameStringValue);
            }
            
            var languageStringValue = Language.ToString();
            dictionary.Add("language", languageStringValue);
            
            var publishedStringValue = Published.ToString();
            dictionary.Add("published", publishedStringValue);
            
            if (LastPublishedDate != null)
            {
                var lastPublishedDateStringValue = LastPublishedDate.ToString();
                dictionary.Add("lastPublishedDate", lastPublishedDateStringValue);
            }
            
            if (LastPublishedVersion != null)
            {
                var lastPublishedVersionStringValue = LastPublishedVersion.ToString();
                dictionary.Add("lastPublishedVersion", lastPublishedVersionStringValue);
            }
            
            return dictionary;
        }
    }
}
