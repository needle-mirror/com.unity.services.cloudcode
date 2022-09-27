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
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Client.ErrorMitigation;
using UnityEngine.Networking;

namespace Unity.Services.CloudCode.Authoring.Client.Http
{
    /// <summary>
    /// HTTP wrapper interface.
    /// </summary>
    internal interface IHttpClient
    {
        /// <summary>Performs an asynchronous HTTP request.</summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="url">The HTTP request URL.</param>
        /// <param name="body">Byte array representing the request body.</param>
        /// <param name="headers">Dictionary of headers for the request.</param>
        /// <param name="requestTimeout">Request timeout value.</param>
        /// <param name="retryPolicyConfig">The policy for backoff and retry.</param>
        /// <param name="statusCodesToRetry">The policy for status code retries.</param>
        /// <returns> </returns>
        Task<HttpClientResponse> MakeRequestAsync(string method, string url, byte[] body, Dictionary<string, string> headers, int requestTimeout, RetryPolicyConfig retryPolicyConfig, StatusCodePolicyConfig statusCodesToRetry);

        /// <summary>Performs an asynchronous Http request for multipart uploads</summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="url">The HTTP request URL.</param>
        /// <param name="body">Byte array representing the request body.</param>
        /// <param name="headers">Dictionary of headers for the request.</param>
        /// <param name="requestTimeout">Request timeout value.</param>
        /// <param name="retryPolicyConfig">The policy for backoff and retry.</param>
        /// <param name="statusCodesToRetry">The policy for status code retries.</param>
        /// <param name="boundary">The string delimiter for each multipart section.</param>
        /// <returns> </returns>
        Task<HttpClientResponse> MakeRequestAsync(string method, string url, List<IMultipartFormSection> body, Dictionary<string, string> headers, int requestTimeout, RetryPolicyConfig retryPolicyConfig = null, StatusCodePolicyConfig statusCodesToRetry = null, string boundary = null);
    }
}
