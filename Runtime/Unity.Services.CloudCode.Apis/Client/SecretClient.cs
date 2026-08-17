using System;
using System.Threading.Tasks;
using Unity.Services.SecretManager.Api;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace Unity.Services.CloudCode.Apis
{
    /// <summary>
    /// Client for managing secrets in the Cloud Code service.
    /// </summary>
    public class SecretClient : ISecretClient
    {
        private static ISecretManagerApi s_client { get; set; }

        /// <summary>
        /// Initializes a new instance of <see cref="SecretClient"/>.
        /// </summary>
        /// <param name="apiClient">The API client to use for requests.</param>
        public SecretClient(IApiClient apiClient)
        {
            s_client = new SecretManagerApi(apiClient);
        }

        /// <summary>
        /// Retrieves a secret value by key from the execution environment.
        /// </summary>
        /// <param name="executionContext">The execution context for the request.</param>
        /// <param name="secretKey">The key of the secret to retrieve.</param>
        /// <returns>A task that returns the secret value.</returns>
        public async Task<Secret> GetSecret(IExecutionContext executionContext, string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey))
            {
                throw new ApiException(ApiExceptionType.InvalidParameters, "Missing required parameter 'secretKey' when calling GetSecret");
            }

            var response = await s_client.GetEnvironmentSecretAsync(executionContext, executionContext.ServiceToken, Guid.Parse(executionContext.ProjectId), Guid.Parse(executionContext.EnvironmentId), secretKey);
            return new Secret(response.Data.Value);
        }
    }
}
