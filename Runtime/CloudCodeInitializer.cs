#if UNITY_EDITOR && UNITY_6000_7_OR_NEWER
#define CLOUD_CODE_ENDPOINT_FROM_EDITOR_PREFS
#endif

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

#if CLOUD_CODE_ENDPOINT_FROM_EDITOR_PREFS
using UnityEditor;
#elif UNITY_EDITOR
using System.IO;
#endif

namespace Unity.Services.CloudCode
{
    class CloudCodeInitializer : IInitializablePackageV2
    {
        const string k_CloudEnvironmentKey = "com.unity.services.core.cloud-environment";
        const string k_StagingEnvironment = "staging";
        internal const ushort k_DefaultLocalCloudCodeServerPort = 14750;
#if CLOUD_CODE_ENDPOINT_FROM_EDITOR_PREFS
        const string k_LocalCloudCodePidPrefs = "LOCAL_CLOUD_CODE_PID";
        const string k_LocalCloudCodePortPrefs = "CLOUD_CODE_DEBUG_PORT";
#endif
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

        public async Task Initialize(CoreRegistry registry)
        {
            CloudCodeService.Instance = await InitializeServiceAsync(registry);
        }

        public async Task InitializeInstanceAsync(CoreRegistry registry)
        {
            await InitializeServiceAsync(registry);
        }

        static async Task<ICloudCodeService> InitializeServiceAsync(CoreRegistry registry)
        {
            var cloudProjectId = registry.GetServiceComponent<ICloudProjectId>();
            var accessToken = registry.GetServiceComponent<IAccessToken>();
            var playerId = registry.GetServiceComponent<IPlayerId>();
            var installationId = registry.GetServiceComponent<IInstallationId>();
            var projectConfiguration = registry.GetServiceComponent<IProjectConfiguration>();
            var externalUserId = registry.GetServiceComponent<IExternalUserId>();
            var localDebugPortOrNull = await GetLocalDebugPortAsync();
            var wire = GetWire(registry, localDebugPortOrNull);

            var configuration = new Configuration(GetHost(projectConfiguration, localDebugPortOrNull), GetTimeout(localDebugPortOrNull), null, GetServiceHeaders(installationId, externalUserId));
            var packageVersion = projectConfiguration.GetString($"{k_PackageName}.version", "unknown");
            configuration.Headers["User-Agent"] = BuildUserAgent(k_PackageName, packageVersion);
            externalUserId.UserIdChanged += id => UpdateExternalUserId(configuration, id);

            ICloudCodeApiClient cloudCodeApiClient = new CloudCodeApiClient(
                new HttpClient(),
                accessToken,
                configuration);

            var service = new CloudCodeInternal(wire, cloudProjectId, cloudCodeApiClient, playerId, accessToken, localDebugPortOrNull != null);
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

#if CLOUD_CODE_ENDPOINT_FROM_EDITOR_PREFS
        static Task<int?> GetLocalDebugPortAsync()
        {
            return Task.FromResult(GetLocalDebugPortFromEditorPrefs() ?? GetLocalDebugPortFromCommandLine());
        }

        internal static int? GetLocalDebugPortFromEditorPrefs()
        {
            if (EditorPrefs.GetInt(k_LocalCloudCodePidPrefs, -1) == -1)
            {
                return null;
            }

            return EditorPrefs.GetInt(k_LocalCloudCodePortPrefs, k_DefaultLocalCloudCodeServerPort);
        }
#elif UNITY_EDITOR
        static async Task<int?> GetLocalDebugPortAsync()
        {
            var serverPort = LocalServerProbe.ReadPort(
                Path.Combine(Path.GetDirectoryName(Application.dataPath), LocalServerProbe.k_SettingsAssetPath),
                k_DefaultLocalCloudCodeServerPort);
            var (result, detail) = await LocalServerProbe.ProbeAsync(serverPort);
            return LogLocalServerProbe(result, serverPort, detail) ? serverPort : GetLocalDebugPortFromCommandLine();
        }
#else
        static Task<int?> GetLocalDebugPortAsync()
        {
            // A release build must never be redirectable to a local server by a command line argument.
            return Task.FromResult(Debug.isDebugBuild ? GetLocalDebugPortFromCommandLine() : null);
        }
#endif

        static int? GetLocalDebugPortFromCommandLine()
        {
            var port = (int)k_DefaultLocalCloudCodeServerPort;
            return TryGetLocalDebugPortFromCommandLine(Environment.GetCommandLineArgs(), ref port) ? port : (int?)null;
        }

#if UNITY_EDITOR
        // Returns whether to route to the local server. A server that isn't running
        // stays silent, since most projects never use it; the other failures warn because the developer
        // likely expected local routing.
        internal static bool LogLocalServerProbe(LocalServerProbe.Result result, int port, string detail)
        {
            switch (result)
            {
                case LocalServerProbe.Result.Healthy:
                    Debug.Log($"Cloud Code calls go to the local server on port {port}.");
                    return true;
                case LocalServerProbe.Result.TimedOut:
                    Debug.LogWarning(
                        $"The Cloud Code local server on port {port} accepted a connection but didn't answer within"
                        + $" {LocalServerProbe.k_Timeout.TotalMilliseconds} ms. Cloud Code calls still go to it;"
                        + " it may be paused at a breakpoint.");
                    return true;
                case LocalServerProbe.Result.Unexpected:
                    Debug.LogWarning(
                        $"Port {port} didn't respond like a running Cloud Code local server ({detail}),"
                        + " so Cloud Code calls go to the deployed environment.");
                    return false;
                default:
                    return false;
            }
        }
#endif

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
