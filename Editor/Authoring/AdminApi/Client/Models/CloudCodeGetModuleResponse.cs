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
    /// CloudCodeGetModuleResponse model
    /// </summary>
    [Preserve]
    [DataContract(Name = "cloud-code.get-module-response")]
    internal class CloudCodeGetModuleResponse
    {
        /// <summary>
        /// Creates an instance of CloudCodeGetModuleResponse.
        /// </summary>
        /// <param name="name">Name of a Cloud Code module.</param>
        /// <param name="language">The language of a Cloud Code module.</param>
        /// <param name="tags">A set of user-defined tags in the form of string key-value pairs.</param>
        /// <param name="signedDownloadURL">Signed URL for time-limited access to the binary contents of the module.</param>
        /// <param name="dateCreated">Date time in ISO 8601 format. Null if there is no associated value.</param>
        /// <param name="dateModified">Date time in ISO 8601 format. Null if there is no associated value.</param>
        [Preserve]
        public CloudCodeGetModuleResponse(string name = default, string language = "CS", Dictionary<string, string> tags = default, string signedDownloadURL = default, DateTime? dateCreated = default, DateTime? dateModified = default)
        {
            Name = name;
            Language = language;
            Tags = tags;
            SignedDownloadURL = signedDownloadURL;
            DateCreated = dateCreated;
            DateModified = dateModified;
        }

        /// <summary>
        /// Name of a Cloud Code module.
        /// </summary>
        [Preserve]
        [DataMember(Name = "name", EmitDefaultValue = false)]
        public string Name{ get; }
        
        /// <summary>
        /// The language of a Cloud Code module.
        /// </summary>
        [Preserve]
        [DataMember(Name = "language", EmitDefaultValue = false)]
        public string Language{ get; }
        
        /// <summary>
        /// A set of user-defined tags in the form of string key-value pairs.
        /// </summary>
        [Preserve]
        [DataMember(Name = "tags", EmitDefaultValue = false)]
        public Dictionary<string, string> Tags{ get; }
        
        /// <summary>
        /// Signed URL for time-limited access to the binary contents of the module.
        /// </summary>
        [Preserve]
        [DataMember(Name = "signedDownloadURL", EmitDefaultValue = false)]
        public string SignedDownloadURL{ get; }
        
        /// <summary>
        /// Date time in ISO 8601 format. Null if there is no associated value.
        /// </summary>
        [Preserve]
        [DataMember(Name = "dateCreated", EmitDefaultValue = false)]
        public DateTime? DateCreated{ get; }
        
        /// <summary>
        /// Date time in ISO 8601 format. Null if there is no associated value.
        /// </summary>
        [Preserve]
        [DataMember(Name = "dateModified", EmitDefaultValue = false)]
        public DateTime? DateModified{ get; }
    
        /// <summary>
        /// Formats a CloudCodeGetModuleResponse into a string of key-value pairs for use as a path parameter.
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
            if (SignedDownloadURL != null)
            {
                serializedModel += "signedDownloadURL," + SignedDownloadURL + ",";
            }
            if (DateCreated != null)
            {
                serializedModel += "dateCreated," + DateCreated.ToString() + ",";
            }
            if (DateModified != null)
            {
                serializedModel += "dateModified," + DateModified.ToString();
            }
            return serializedModel;
        }

        /// <summary>
        /// Returns a CloudCodeGetModuleResponse as a dictionary of key-value pairs for use as a query parameter.
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
            
            if (SignedDownloadURL != null)
            {
                var signedDownloadURLStringValue = SignedDownloadURL.ToString();
                dictionary.Add("signedDownloadURL", signedDownloadURLStringValue);
            }
            
            if (DateCreated != null)
            {
                var dateCreatedStringValue = DateCreated.ToString();
                dictionary.Add("dateCreated", dateCreatedStringValue);
            }
            
            if (DateModified != null)
            {
                var dateModifiedStringValue = DateModified.ToString();
                dictionary.Add("dateModified", dateModifiedStringValue);
            }
            
            return dictionary;
        }
    }
}
