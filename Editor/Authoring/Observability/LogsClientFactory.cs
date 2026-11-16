using Unity.Services.CloudCode.Authoring.Client.Http;
using Unity.Services.Core.Editor;
using Unity.Services.Core.Editor.Environments;
#if DEPLOYMENT_API_AVAILABLE_V1_1
using IProjectID = Unity.Services.DeploymentApi.Editor.IProjectIdentifierProvider;
#else
using IProjectID = Unity.Services.CloudCode.Authoring.Editor.Deployment.IProjectIdentifierProvider;
#endif

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    static class LogsClientFactory
    {
        internal static ILogsClient CreateClient()
        {
            var services = CloudCodeAuthoringServices.Instance;
            return new LogsClient(
                services.GetService<IAccessTokens>(),
                services.GetService<IProjectID>(),
                services.GetService<IEnvironmentsApi>(),
                services.GetService<IHttpClient>());
        }
    }
}
