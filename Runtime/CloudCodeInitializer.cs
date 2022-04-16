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
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            CoreRegistry.Instance.RegisterPackage(new CloudCodeInitializer())
                .DependsOn<IPlayerId>()
                .DependsOn<IAccessToken>()
                .DependsOn<IInstallationId>()
                .DependsOn<IProjectConfiguration>();
        }

        public Task Initialize(CoreRegistry registry)
        {
            var accessToken = registry.GetServiceComponent<IAccessToken>();
            var playerId = registry.GetServiceComponent<IPlayerId>();

            ICloudCodeApiClient cloudCodeApiClient = new CloudCodeApiClient(
                new HttpClient(),
                accessToken,
                new Configuration(null, null, null, GetServiceHeaders(registry)));

            CloudCodeService.Instance = new CloudCodeInternal(Application.cloudProjectId, cloudCodeApiClient, playerId, accessToken);

            return Task.CompletedTask;
        }

        static Dictionary<string, string> GetServiceHeaders(CoreRegistry registry)
        {
            var headers = new Dictionary<string, string>();

            var installationId = registry.GetServiceComponent<IInstallationId>().GetOrCreateIdentifier();
            var analyticsUserId = registry.GetServiceComponent<IProjectConfiguration>().GetString("com.unity.services.core.analytics-user-id");

            headers.Add("unity-installation-id", installationId);

            if (!String.IsNullOrEmpty(analyticsUserId))
            {
                headers.Add("analytics-user-id", analyticsUserId);
            }

            return headers;
        }
    }
}
