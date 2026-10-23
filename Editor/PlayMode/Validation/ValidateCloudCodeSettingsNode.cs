using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Multiplayer.PlayMode.Editor;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.Core.Editor.Environments;
using UnityEditor;
using UnityEngine;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [Serializable]
    class ValidateCloudCodeSettingsNode : ExecutionNode
    {
        internal const string k_ProjectNotLinkedMessage =
            "Cloud Code: Project not linked. Link it in Edit > Project Settings > Services.";
        internal const string k_EnvironmentNotSetMessage =
            "Cloud Code: The environment has not been set. Please use the Environment Selector, found under Edit > Project Settings > Services > Environments, to set it.";

        [SerializeReference] public NodeInput<CloudCodeSettings> Settings;

        public ValidateCloudCodeSettingsNode()
        {
            Settings = new(this);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var settings = GetInput(Settings);
            if (!settings.DeployCloudCode)
                return;

            ValidateDeployTarget(settings.DeployTarget);

            var services = CloudCodeAuthoringServices.Instance;
            var error = GetProjectConfigurationError(
                CloudProjectSettings.projectId, services.GetService<IEnvironmentsApi>().ActiveEnvironmentId);
            if (error == null && settings.DeployTarget == CloudCodeDeployTarget.Local)
                error = await GetLocalServerError(services.GetService<ICloudCodeLocalServer>(), cancellationToken);

            if (error != null)
                throw new InvalidOperationException(error);
        }

        static void ValidateDeployTarget(CloudCodeDeployTarget target)
        {
            if (!Enum.IsDefined(typeof(CloudCodeDeployTarget), target))
            {
                throw new InvalidOperationException(
                    $"Cloud Code: The deploy target '{(int)target}' is not supported. Set Deploy Target to Local or Remote in the scenario's Cloud Code settings.");
            }
        }

        internal static string GetProjectConfigurationError(string projectId, Guid? environmentId)
        {
            if (string.IsNullOrEmpty(projectId))
                return k_ProjectNotLinkedMessage;

            if (!EnvironmentIds.IsSet(environmentId))
                return k_EnvironmentNotSetMessage;

            return null;
        }

        static async Task<string> GetLocalServerError(ICloudCodeLocalServer server, CancellationToken cancellationToken)
        {
            var configuredPort = GetConfiguredPort();
            switch (server.GetCurrentServerStatus())
            {
                case LocalCloudCodeServerStatus.Started:
                    var boundPort = CloudCodeLocalArgument.ToEffectivePort(
                        EditorPrefs.GetInt(CloudCodeEndpointMessage.k_ServerPortPref, configuredPort));
                    return GetRunningServerError(configuredPort, boundPort);
                case LocalCloudCodeServerStatus.Idle:
                    return await LocalServerPort.IsLoopbackListeningAsync(configuredPort, cancellationToken)
                        ? GetPortInUseError(configuredPort)
                        : null;
                default:
                    return null;
            }
        }

        static int GetConfiguredPort()
        {
            var settings = CloudCodeLocalServerSettings.LoadExisting();
            return CloudCodeLocalArgument.ToEffectivePort(settings != null ? settings.Port : CloudCodeLocalArgument.k_DefaultPort);
        }

        internal static string GetRunningServerError(int configuredPort, int boundPort)
        {
            if (configuredPort == boundPort)
                return null;

            return $"Cloud Code: The local server runs on port {boundPort}, but the Cloud Code Local Server Settings asset is set to port {configuredPort}. Restart the local server.";
        }

        internal static string GetPortInUseError(int port)
        {
            return $"Cloud Code: {LocalServerPort.InUseMessage(port)}";
        }
    }
}
