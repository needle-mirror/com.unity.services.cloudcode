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
    /// CloudCodeCreateModuleRequest model
    /// </summary>
    [Preserve]
    [DataContract(Name = "cloud-code.create-module-request")]
    internal class CloudCodeCreateModuleRequest
    {
        /// <summary>
        /// Creates an instance of CloudCodeCreateModuleRequest.
        /// </summary>
        /// <param name="name">Name of a Cloud Code module.</param>
        /// <param name="language">The language of a Cloud Code module.</param>
        /// <param name="file">Archive file containing the module assemblies.</param>
        /// <param name="tags">A set of user-defined tags in the form of string key-value pairs.</param>
        [Preserve]
        public CloudCodeCreateModuleRequest(string name, string language, System.IO.Stream file, Dictionary<string, string> tags = default)
        {
            Name = name;
            Language = language;
            Tags = tags;
            File = file;
        }

        /// <summary>
        /// Name of a Cloud Code module.
        /// </summary>
        [Preserve]
        [DataMember(Name = "name", IsRequired = true, EmitDefaultValue = true)]
        public string Name{ get; }
        
        /// <summary>
        /// The language of a Cloud Code module.
        /// </summary>
        [Preserve]
        [DataMember(Name = "language", IsRequired = true, EmitDefaultValue = true)]
        public string Language{ get; }
        
        /// <summary>
        /// A set of user-defined tags in the form of string key-value pairs.
        /// </summary>
        [Preserve]
        [DataMember(Name = "tags", EmitDefaultValue = false)]
        public Dictionary<string, string> Tags{ get; }
        
        /// <summary>
        /// Archive file containing the module assemblies.
        /// </summary>
        [Preserve]
        [DataMember(Name = "file", IsRequired = true, EmitDefaultValue = true)]
        public System.IO.Stream File{ get; }
    
        /// <summary>
        /// Formats a CloudCodeCreateModuleRequest into a string of key-value pairs for use as a path parameter.
        /// </summary>
        /// <returns>Returns a string representation of the key-value pairs.</returns>
        internal string SerializeAsPathParam()
        {
            var serializedModel = "";

            if (Name != null)
            {
                serializedModel += "name," + Name + ",";
            }
            if (Language != null)
            {
                serializedModel += "language," + Language + ",";
            }
            if (Tags != null)
            {
                serializedModel += "tags," + Tags.ToString() + ",";
            }
            if (File != null)
            {
                serializedModel += "file," + File.ToString();
            }
            return serializedModel;
        }

        /// <summary>
        /// Returns a CloudCodeCreateModuleRequest as a dictionary of key-value pairs for use as a query parameter.
        /// </summary>
        /// <returns>Returns a dictionary of string key-value pairs.</returns>
        internal Dictionary<string, string> GetAsQueryParam()
        {
            var dictionary = new Dictionary<string, string>();

            if (Name != null)
            {
                var nameStringValue = Name.ToString();
                dictionary.Add("name", nameStringValue);
            }
            
            if (Language != null)
            {
                var languageStringValue = Language.ToString();
                dictionary.Add("language", languageStringValue);
            }
            
            if (Tags != null)
            {
                var tagsStringValue = Tags.ToString();
                dictionary.Add("tags", tagsStringValue);
            }
            
            if (File != null)
            {
                var fileStringValue = File.ToString();
                dictionary.Add("file", fileStringValue);
            }
            
            return dictionary;
        }
    }
}
