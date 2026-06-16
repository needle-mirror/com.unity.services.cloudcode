using System;
using System.Threading.Tasks;
using Unity.Services.SecretManager.Api;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace Unity.Services.CloudCode.Apis
{
    public class SecretClient : ISecretClient
    {
        private static ISecretManagerApi s_client { get; set; }

        public SecretClient(IApiClient apiClient)
        {
            s_client = new SecretManagerApi(apiClient);
        }

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
