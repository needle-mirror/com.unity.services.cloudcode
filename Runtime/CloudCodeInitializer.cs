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
using Unity.Services.Wire.Internal;
using UnityEngine;

namespace Unity.Services.CloudCode
{
    class CloudCodeInitializer : IInitializablePackage
    {
        const string k_CloudEnvironmentKey = "com.unity.services.core.cloud-environment";
        const string k_StagingEnvironment = "staging";
        const int k_ConfigurationReqTimeoutSec = 30;
        const string k_PackageName = "com.unity.services.cloudcode";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            // Ensure Instance is reset to account for Fast Enter Play Mode
            CloudCodeService.Instance = null;

            CoreRegistry.Instance.RegisterPackage(new CloudCodeInitializer())
                .DependsOn<ICloudProjectId>()
                .DependsOn<IPlayerId>()
                .DependsOn<IAccessToken>()
                .DependsOn<IInstallationId>()
                .DependsOn<IProjectConfiguration>()
                .DependsOn<IExternalUserId>()
                .OptionallyDependsOn<IWire>();
        }

        public Task Initialize(CoreRegistry registry)
        {
            var cloudProjectId = registry.GetServiceComponent<ICloudProjectId>();
            var accessToken = registry.GetServiceComponent<IAccessToken>();
            var playerId = registry.GetServiceComponent<IPlayerId>();
            var installationId = registry.GetServiceComponent<IInstallationId>();
            var projectConfiguration = registry.GetServiceComponent<IProjectConfiguration>();
            var externalUserId = registry.GetServiceComponent<IExternalUserId>();
            var wire = registry.GetServiceComponent<IWire>();

            var configuration = new Configuration(GetHost(projectConfiguration), k_ConfigurationReqTimeoutSec, null, GetServiceHeaders(installationId, externalUserId));
            var packageVersion = projectConfiguration.GetString($"{k_PackageName}.version", "unknown");
            configuration.Headers["User-Agent"] = BuildUserAgent(k_PackageName, packageVersion);
            externalUserId.UserIdChanged += id => UpdateExternalUserId(configuration, id);

            ICloudCodeApiClient cloudCodeApiClient = new CloudCodeApiClient(
                new HttpClient(),
                accessToken,
                configuration);

            var service = new CloudCodeInternal(wire, cloudProjectId, cloudCodeApiClient, playerId, accessToken);
            registry.RegisterService<ICloudCodeService>(service);
            CloudCodeService.Instance = service;
            return Task.CompletedTask;
        }

        static Dictionary<string, string> GetServiceHeaders(IInstallationId installationIdProvider, IExternalUserId externalUserId)
        {
            var headers = new Dictionary<string, string>();

            var installationId = installationIdProvider.GetOrCreateIdentifier();
            var analyticsUserId = externalUserId.UserId;

            headers.Add("unity-installation-id", installationId);

            if (!String.IsNullOrEmpty(analyticsUserId))
            {
                headers.Add("analytics-user-id", analyticsUserId);
            }

            return headers;
        }

        static void UpdateExternalUserId(Configuration configuration, string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                configuration.Headers.Remove("analytics-user-id");
            }
            else
            {
                configuration.Headers["analytics-user-id"] = userId;
            }
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

        internal static string BuildUserAgent(string packageName, string packageVersion)
        {
            return $"UnityPlayer/{Application.unityVersion} ({packageName}/{packageVersion})";
        }
    }
}
