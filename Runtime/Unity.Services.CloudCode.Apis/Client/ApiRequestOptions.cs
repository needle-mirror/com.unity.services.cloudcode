using System;
using System.Collections.Generic;
using System.IO;
using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Shared
{
    /// <summary>
    /// A container for generalized request inputs. This type allows consumers to extend the request functionality
    /// by abstracting away from the default (built-in) request framework (e.g. RestSharp).
    /// </summary>
    public class ApiRequestOptions
    {
        /// <summary>
        /// Parameters to be bound to path parts of the Request's URL
        /// </summary>
        public Dictionary<string, string> PathParameters { get; set; }

        /// <summary>
        /// Query parameters to be applied to the request.
        /// Keys may have 1 or more values associated.
        /// </summary>
        public Multimap<string, string> QueryParameters { get; set; }

        /// <summary>
        /// Header parameters to be applied to to the request.
        /// Keys may have 1 or more values associated.
        /// </summary>
        public Multimap<string, string> HeaderParameters { get; set; }

        /// <summary>
        /// Form parameters to be sent along with the request.
        /// </summary>
        public Dictionary<string, string> FormParameters { get; set; }

        /// <summary>
        /// File parameters to be sent along with the request.
        /// </summary>
        public Multimap<string, Stream> FileParameters { get; set; }

        /// <summary>
        /// Operation associated with the request path.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Any data associated with a request body.
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// Constructs a new instance of <see cref="ApiRequestOptions"/>
        /// </summary>
        /// <param name="executionContext">The execution context for the request.</param>
        /// <param name="accessToken">The access token for bearer authentication.</param>
        public ApiRequestOptions(IExecutionContext executionContext, string accessToken)
        {
            Initialize(executionContext, new BearerAuth(accessToken));
        }

        /// <summary>
        /// Constructs a new instance of <see cref="ApiRequestOptions"/>
        /// </summary>
        /// <param name="executionContext">The execution context for the request.</param>
        /// <param name="authentication">The authentication method to use for the request.</param>
        public ApiRequestOptions(IExecutionContext executionContext, IAuthType authentication)
        {
            Initialize(executionContext, authentication);
        }

        private void Initialize(IExecutionContext executionContext, IAuthType authType)
        {
            PathParameters = new Dictionary<string, string>();
            QueryParameters = new Multimap<string, string>();
            HeaderParameters = new Multimap<string, string>();
            FormParameters = new Dictionary<string, string>();
            FileParameters = new Multimap<string, Stream>();
            HeaderParameters.Add("Authorization", authType.GetHeader());
            if (!string.IsNullOrEmpty(executionContext.UnityInstallationId))
            {
                HeaderParameters.Add("Unity-Installation-Id", executionContext.UnityInstallationId);
            }
            if (!string.IsNullOrEmpty(executionContext.AnalyticsUserId))
            {
                HeaderParameters.Add("Analytics-User-Id", executionContext.AnalyticsUserId);
            }
            if (!string.IsNullOrEmpty(executionContext.CorrelationId))
            {
                HeaderParameters.Add("X-Request-Id", executionContext.CorrelationId);
            }
            HeaderParameters.Add("X-Call-Depth", (executionContext.CallDepth + 1).ToString());
        }
    }
}
