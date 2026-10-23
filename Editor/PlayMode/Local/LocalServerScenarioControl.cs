using Unity.Multiplayer.PlayMode.Editor;
using Unity.PlayMode.Editor;
using UnityEditor;
using ScenarioControlInfo = Unity.Services.CloudCode.Authoring.Editor.Modules.UI.CloudCodeLocalServerScenarioControl.Info;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    // Tells the toolbar when the active scenario deploys Cloud Code, so it stops offering local server controls.
    static class LocalServerScenarioControl
    {
        internal static readonly string k_LocalMessage =
            L10n.Tr("Cloud Code server is currently controlled by Play Mode Scenario.");
        internal static readonly string k_RemoteMessage =
            L10n.Tr("Cloud Code server is currently controlled by Play Mode Scenario. It deploys to the remote Cloud Code service, so the local server is not used.");

        internal static ScenarioControlInfo GetActiveScenarioControl() => GetControl(PlayModeScenarioManager.ActiveScenario);

        internal static ScenarioControlInfo GetControl(PlayModeScenario activeScenario)
        {
            if (activeScenario is not OrchestratedScenario scenario)
                return null;

            var item = scenario.Settings.FindScenarioItem(typeof(CloudCodeController));
            return item == null ? null : GetControl(item.GetSettings<CloudCodeSettings>());
        }

        internal static ScenarioControlInfo GetControl(CloudCodeSettings settings)
        {
            if (!settings.DeployCloudCode)
                return null;

            return settings.DeployTarget == CloudCodeDeployTarget.Remote
                ? new ScenarioControlInfo(k_RemoteMessage, usesLocalServer: false)
                : new ScenarioControlInfo(k_LocalMessage, usesLocalServer: true);
        }
    }
}
