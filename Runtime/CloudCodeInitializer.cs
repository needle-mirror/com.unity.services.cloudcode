using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication.Internal;
using Unity.Services.CloudCode.Internal;
using Unity.Services.CloudCode.Internal.Apis.CloudCode;
using Unity.Services.CloudCode.Internal.Http;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Core.Device.Internal;
using Unity.Services.Core.Internal;
using UnityEngine;

namespace Unity.Services.CloudCode
{
    class CloudCodeInitializer : IInitializablePackage
    {
        const string k_CloudEnvironmentKey = "com.unity.services.core.cloud-environment";
        const string k_StagingEnvironment = "staging";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            CoreRegistry.Instance.RegisterPackage(new CloudCodeInitializer())
                .DependsOn<ICloudProjectId>()
                .DependsOn<IPlayerId>()
                .DependsOn<IAccessToken>()
                .DependsOn<IInstallationId>()
                .DependsOn<IProjectConfiguration>();
        }

        public Task Initialize(CoreRegistry registry)
        {
            var cloudProjectId = registry.GetServiceComponent<ICloudProjectId>();
            var accessToken = registry.GetServiceComponent<IAccessToken>();
            var playerId = registry.GetServiceComponent<IPlayerId>();
            var installationId = registry.GetServiceComponent<IInstallationId>();
            var projectConfiguration = registry.GetServiceComponent<IProjectConfiguration>();

            ICloudCodeApiClient cloudCodeApiClient = new CloudCodeApiClient(
                new HttpClient(),
                accessToken,
                new Configuration(GetHost(projectConfiguration), null, null, GetServiceHeaders(installationId, projectConfiguration)));

            CloudCodeService.Instance = new CloudCodeInternal(cloudProjectId, cloudCodeApiClient, playerId, accessToken);

            return Task.CompletedTask;
        }

        static Dictionary<string, string> GetServiceHeaders(IInstallationId installationIdProvider, IProjectConfiguration projectConfiguration)
        {
            var headers = new Dictionary<string, string>();

            var installationId = installationIdProvider.GetOrCreateIdentifier();
            var analyticsUserId = projectConfiguration.GetString("com.unity.services.core.analytics-user-id");

            headers.Add("unity-installation-id", installationId);

            if (!String.IsNullOrEmpty(analyticsUserId))
            {
                headers.Add("analytics-user-id", analyticsUserId);
            }

            return headers;
        }

        static string GetHost(IProjectConfiguration projectConfiguration)
        {
            var cloudEnvironment = projectConfiguration?.GetString(k_CloudEnvironmentKey);

            switch (cloudEnvironment)
            {
                case k_StagingEnvironment:
                    return "https://cloud-code-stg.services.api.unity.com";
                default:
                    return "https://cloud-code.services.api.unity.com";
            }
        }
    }
}
