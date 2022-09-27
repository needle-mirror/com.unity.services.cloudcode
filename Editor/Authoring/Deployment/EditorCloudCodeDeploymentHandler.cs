using System.Collections.Generic;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Authoring.Editor.Scripts.Validation;
using Unity.Services.DeploymentApi.Editor;
using DeploymentSeverity = Unity.Services.DeploymentApi.Editor.SeverityLevel;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    class EditorCloudCodeDeploymentHandler : CloudCodeDeploymentHandler
    {
        public EditorCloudCodeDeploymentHandler(
            ICloudCodeClient client,
            IDeploymentAnalytics deploymentAnalytics,
            IScriptCache scriptCache,
            ILogger logger) :
            base(client, deploymentAnalytics, scriptCache, logger)
        {
        }

        protected override void UpdateScriptProgress(IScript script, float progress)
        {
            ((Script)script).Progress = progress;
        }

        protected override void UpdateScriptStatus(IScript script, string message, string detail, StatusSeverityLevel level = StatusSeverityLevel.Error)
        {
            ((Script)script).Status = new DeploymentStatus(
                message,
                detail,
                ToDeploymentSeverityLevel(level));
        }

        protected override bool OnPreDeploy(IReadOnlyList<IScript> scriptsEnumerated)
        {
            var scriptNames = scriptsEnumerated.Select(di => di.Name).ToList();

            if (scriptNames.Count != scriptNames.Distinct().Count())
            {
                DuplicateNameValidator.DetectDuplicateNames(scriptsEnumerated.Cast<Script>().ToList());
                m_Logger.LogError(DuplicateNamesError);
                return false;
            }

            return true;
        }

        static DeploymentSeverity ToDeploymentSeverityLevel(StatusSeverityLevel level)
        {
            switch (level)
            {
                case StatusSeverityLevel.Info:
                    return DeploymentSeverity.Info;
                case StatusSeverityLevel.Warning:
                    return DeploymentSeverity.Warning;
                case StatusSeverityLevel.Error:
                default:
                    return DeploymentSeverity.Error;
            }
        }
    }
}
