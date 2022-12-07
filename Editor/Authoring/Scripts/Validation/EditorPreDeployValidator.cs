using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Parameters;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Scripts.Validation
{
    class EditorPreDeployValidator : PreDeployValidator
    {
        readonly IInScriptParameters m_InScriptParameters;

        public EditorPreDeployValidator(ILogger logger, IInScriptParameters inScriptParameters)
            : base(logger)
        {
            m_InScriptParameters = inScriptParameters;
            DuplicateNameConsoleError = L10n.Tr(DuplicateNameConsoleError + " See Deployment Window for details.");
        }

        public override async Task<ValidationInfo> Validate(
            IReadOnlyList<IScript> scripts)
        {
            var validationInfo = await base.Validate(scripts);
            var invalidScripts = new Dictionary<IScript, Exception>(validationInfo.InvalidScripts);
            foreach (var script in validationInfo.ValidScripts)
            {
                try
                {
                    await m_InScriptParameters.GetParametersFromPath(script.Path);
                }
                catch (InvalidOperationException e)
                {
                    invalidScripts.Add(script, e);
                    var concreteScript = (Script)script;
                    concreteScript.Status = DeploymentStatus.FailedToDeploy;
                    var state = new AssetState(e.Message, string.Empty, level: SeverityLevel.Error);
                    concreteScript.States.Add(state);
                }
            }

            foreach (var(invalidScript, exception) in validationInfo.InvalidScripts)
            {
                var concreteScript = (Script)invalidScript;
                concreteScript.Status = DeploymentStatus.FailedToDeploy;
                concreteScript.SetStatusDetail(exception.Message);
            }

            return new ValidationInfo(
                validationInfo.ValidScripts.Except(invalidScripts.Keys).ToList(),
                invalidScripts);
        }

        protected override void NotifyDuplicateScriptError(IReadOnlyList<IScript> scripts, IReadOnlyList<IScript> duplicateScripts)
        {
            base.NotifyDuplicateScriptError(scripts, duplicateScripts);

            DuplicateNameValidator.DetectDuplicateNames(scripts.Cast<IDeploymentItem>().ToList());
        }
    }
}
