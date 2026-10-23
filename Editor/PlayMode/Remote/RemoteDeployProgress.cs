using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.PlayMode.Editor;
using Unity.PlayMode.Editor;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    // The window builds the Cloud Code title bar with no runtime, so the progress is read from the active scenario.
    static class RemoteDeployProgress
    {
        internal static bool TryGet(out float progress)
        {
            var runtime = (PlayModeScenarioManager.ActiveScenario as OrchestratedScenario)?.Scenario?
                .GetAllScenarioControllers()
                .FirstOrDefault(controller => controller.Controller is CloudCodeController);

            var nodes = runtime?.GetExecutionGraph()?.GetNodes(ExecutionStage.Prepare);
            return TryGet(nodes, out progress);
        }

        internal static bool TryGet(IEnumerable<ExecutionNode> prepareNodes, out float progress)
        {
            var deploy = prepareNodes?.OfType<DeployRemoteCloudCodeNode>()
                .FirstOrDefault(node => node.State == ExecutionState.Running);
            progress = deploy?.Progress ?? 0f;
            return deploy != null;
        }

        internal static string Label(float progress) => $"Deploying... {progress:P0}";
    }
}
