using System;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using UnityEngine.UIElements;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    /// <summary>
    /// Renders the "Last Successful Deployment" inspector section shared by the
    /// Cloud Code Module (.ccmu) and Module Reference (.ccmr) inspectors, so both display the same
    /// target/time summary from the same UXML element names.
    /// </summary>
    static class LastDeploymentSection
    {
        public static void Refresh(VisualElement root, LastSuccessfulDeploymentInfo deployment)
        {
            var targetLabel = root.Q<Label>("last-deploy-target");
            var timeLabel = root.Q<Label>("last-deploy-time");
            if (targetLabel == null || timeLabel == null)
                return;

            if (deployment != null)
            {
                targetLabel.text = TargetDisplayName(deployment.Target);
                var deployedLocalTime = deployment.DeployedAtUtc.ToLocalTime();
                timeLabel.text = $"Editor deployed on [{deployedLocalTime:yyyy-MM-dd HH:mm:ss}]";
            }
            else
            {
                targetLabel.text = "No status";
                timeLabel.text = "No status";
            }
        }

        static string TargetDisplayName(DeploymentTarget target)
        {
            return target == DeploymentTarget.Remote
                ? "Remote Cloud Code Server"
                : "Local Server";
        }
    }
}
