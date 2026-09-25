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

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Services.CloudCode
{
    class CloudCodeInitializer : IInitializablePackageV2
    {
        const string k_CloudEnvironmentKey = "com.unity.services.core.cloud-environment";
        const string k_StagingEnvironment = "staging";
        internal const ushort k_DefaultLocalCloudCodeServerPort = 14750;
        const string k_LocalCloudCodePidPrefs = "LOCAL_CLOUD_CODE_PID";
        const string k_LocalCloudCodePortPrefs = "CLOUD_CODE_DEBUG_PORT";
        const string k_LocalCloudCodeDebuggerArg = "--cloud-code-local-debugger";
        const int k_ConfigurationReqTimeoutSec = 30;
        const string k_PackageName = "com.unity.services.cloudcode";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeOnLoad()
        {
            // Ensure Instance is reset to account for Fast Enter Play Mode
            CloudCodeService.Instance = null;
            var initializer = new CloudCodeInitializer();
            initializer.Register(CorePackageRegistry.Instance);
        }

        public void Register(CorePackageRegistry registry)
        {
            registry.Register(this)
                .DependsOn<ICloudProjectId>()
                .DependsOn<IPlayerId>()
                .DependsOn<IAccessToken>()
                .DependsOn<IInstallationId>()
                .DependsOn<IProjectConfiguration>()
                .DependsOn<IExternalUserId>()
                .OptionallyDependsOn<IWire>()
                .OptionallyDependsOn<IWireFactory>();
        }

        public Task Initialize(CoreRegistry registry)
        {
            CloudCodeService.Instance = InitializeService(registry);
            return Task.CompletedTask;
        }

        public Task InitializeInstanceAsync(CoreRegistry registry)
        {
            _ = InitializeService(registry);
            return Task.CompletedTask;
        }

        static ICloudCodeService InitializeService(CoreRegistry registry)
        {
            var cloudProjectId = registry.GetServiceComponent<ICloudProjectId>();
            var accessToken = registry.GetServiceComponent<IAccessToken>();
            var playerId = registry.GetServiceComponent<IPlayerId>();
            var installationId = registry.GetServiceComponent<IInstallationId>();
            var projectConfiguration = registry.GetServiceComponent<IProjectConfiguration>();
            var externalUserId = registry.GetServiceComponent<IExternalUserId>();
            var localDebugPortOrNull = TryGetLocalDebugPort(out var localDebugPort) ? localDebugPort : (int?)null;
            var wire = GetWire(registry, localDebugPortOrNull);

            var configuration = new Configuration(GetHost(projectConfiguration, localDebugPortOrNull), GetTimeout(localDebugPortOrNull), null, GetServiceHeaders(installationId, externalUserId));
            var packageVersion = projectConfiguration.GetString($"{k_PackageName}.version", "unknown");
            configuration.Headers["User-Agent"] = BuildUserAgent(k_PackageName, packageVersion);
            externalUserId.UserIdChanged += id => UpdateExternalUserId(configuration, id);

            ICloudCodeApiClient cloudCodeApiClient = new CloudCodeApiClient(
                new HttpClient(),
                accessToken,
                configuration);

            var service = new CloudCodeInternal(wire, cloudProjectId, cloudCodeApiClient, playerId, accessToken);
            registry.RegisterService<ICloudCodeService>(service);
            registry.RegisterService<ICloudCodeWarmup>(new CloudCodeWarmup(cloudProjectId, cloudCodeApiClient, playerId, accessToken));
            return service;
        }

        static Dictionary<string, string> GetServiceHeaders(IInstallationId installationIdProvider, IExternalUserId externalUserId)
        {
            var headers = new Dictionary<string, string>();

            var installationId = installationIdProvider.GetOrCreateIdentifier();
            var analyticsUserId = externalUserId.UserId;

            headers.Add("unity-installation-id", installationId);

            if (!string.IsNullOrEmpty(analyticsUserId))
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

        static IWire GetWire(CoreRegistry registry, int? localDebugPort)
        {
            var wire = registry.GetServiceComponent<IWire>();
            if (localDebugPort == null)
            {
                return wire;
            }

            var wireFactory = registry.GetServiceComponent<IWireFactory>();
            if (wireFactory == null)
            {
                Debug.LogError(
                    "Cloud Code is in local debugging mode but no IWireFactory is registered, so push messages"
                    + " will be subscribed against the cloud, which rejects the local server's channel tokens."
                    + " Upgrade com.unity.services.wire to 1.6.0 or newer.");
                return wire;
            }

            return wireFactory.Create($"ws://localhost:{localDebugPort}/v2/ws");
        }

        static int? GetTimeout(int? localDebugPort)
        {
            if (localDebugPort != null)
            {
                // For local Cloud Code debugging, override and ensure UnityWebRequest do not time out.
                return 0;
            }

            // Provide overrides for remote Cloud Code
            return k_ConfigurationReqTimeoutSec;
        }

        static string GetHost(IProjectConfiguration projectConfiguration, int? localDebugPort)
        {
            if (localDebugPort != null)
            {
                return "http://localhost:" + localDebugPort;
            }

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

        internal static bool TryGetLocalDebugPort(out int port)
        {
            port = k_DefaultLocalCloudCodeServerPort;

#if UNITY_EDITOR
            if (EditorPrefs.GetInt(k_LocalCloudCodePidPrefs, -1) != -1)
            {
                port = EditorPrefs.GetInt(k_LocalCloudCodePortPrefs, k_DefaultLocalCloudCodeServerPort);
                return true;
            }
#else
            // A release build must never be redirectable to a local server by a command line argument.
            if (!Debug.isDebugBuild)
            {
                return false;
            }
#endif

            return TryGetLocalDebugPortFromCommandLine(Environment.GetCommandLineArgs(), ref port);
        }

        internal static bool TryGetLocalDebugPortFromCommandLine(string[] commandLineArgs, ref int port)
        {
            if (commandLineArgs == null)
            {
                return false;
            }

            for (var i = 0; i < commandLineArgs.Length; i++)
            {
                var arg = commandLineArgs[i];
                string value;

                if (arg == k_LocalCloudCodeDebuggerArg)
                {
                    value = i + 1 < commandLineArgs.Length ? commandLineArgs[i + 1] : null;
                }
                else if (arg != null && arg.StartsWith(k_LocalCloudCodeDebuggerArg + "="))
                {
                    value = arg.Substring(k_LocalCloudCodeDebuggerArg.Length + 1);
                }
                else
                {
                    continue;
                }

                if (int.TryParse(value, out var parsedPort) && parsedPort > 0 && parsedPort <= 65535)
                {
                    port = parsedPort;
                }

                return true;
            }

            return false;
        }
    }
}
