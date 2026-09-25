using Unity.Multiplayer.PlayMode.Editor;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using UnityEditor;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    class CloudCodeController : PlayModeController<CloudCodeSettings>
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            PlayModeControllerRegistry.RegisterScenarioController<CloudCodeController>();
        }

        protected internal override void SetupExecutionGraph(ExecutionGraphBuilder graph)
        {
            var settings = Settings;

            var validate = graph.AddNode<ValidateCloudCodeSettingsNode>(ExecutionStage.Validate);
            graph.ConnectConstant(validate.Settings, settings);

            if (!settings.DeployCloudCode)
            {
                return;
            }

            var publish = graph.AddNode<PublishCloudCodeEndpointNode>(ExecutionStage.Prepare);
            var reset = graph.AddNode<ResetCloudCodeEndpointNode>(ExecutionStage.Cleanup);

            switch (settings.DeployTarget)
            {
                case CloudCodeDeployTarget.Local:
                {
                    // The server outlives the run: nothing stops it in Cleanup. It is stopped when the
                    // developer switches scenario (TearDown below), switches to Remote, or quits.
                    var start = graph.AddNode<StartLocalCloudCodeServerNode>(ExecutionStage.Prepare);

                    // This edge is what runs the server start before the endpoint is published.
                    graph.Connect(start.Endpoint, publish.Endpoint);
                    graph.Connect(start.Endpoint, reset.Endpoint);
                    graph.Connect(start.StartedByScenario, reset.StartedByScenario);
                    break;
                }
                case CloudCodeDeployTarget.Remote:
                {
                    var deploy = graph.AddNode<DeployRemoteCloudCodeNode>(ExecutionStage.Prepare);

                    graph.Connect(deploy.Endpoint, publish.Endpoint);
                    graph.Connect(deploy.Endpoint, reset.Endpoint);
                    graph.ConnectConstant(reset.StartedByScenario, false);
                    break;
                }
            }
        }

        internal override bool NeedsTearDown(ControllerRuntime runtime, out string reason)
        {
            if (ShouldStopLocalServer(out _))
            {
                reason = $"Local Cloud Code server";
                return true;
            }

            reason = null;
            return false;
        }

        internal override void TearDown(ControllerRuntime runtime)
        {
            var stop = ShouldStopLocalServer(out var server);
            LocalServerOwnership.Release();
            if (stop)
                _ = server.StopCompilationAndService();
        }

        // Stops a server this scenario started, whatever its settings say now, and a server it uses
        // because it is set to deploy Cloud Code locally.
        bool ShouldStopLocalServer(out ICloudCodeLocalServer server)
        {
            server = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
            var isRunning = server.GetCurrentServerStatus() is LocalCloudCodeServerStatus.Started
                or LocalCloudCodeServerStatus.Starting
                or LocalCloudCodeServerStatus.Preparing;
            if (!isRunning)
                return false;

            if (LocalServerOwnership.IsOwned(server))
                return true;

            var settings = Settings;
            return settings.DeployCloudCode && settings.DeployTarget == CloudCodeDeployTarget.Local;
        }
    }
}
