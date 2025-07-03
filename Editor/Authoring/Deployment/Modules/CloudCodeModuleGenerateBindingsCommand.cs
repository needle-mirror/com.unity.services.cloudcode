using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Core.Modules.Bindings;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Shared.UI;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules
{
    class CloudCodeModuleGenerateBindingsCommand : Command<CloudCodeModuleReference>
    {
        public override string Name => L10n.Tr("Generate Code Bindings");
        const string k_DialogOptOutKey = "cloudcode_generate_changed";

        ICloudCodeModuleBindingsGenerator m_EditorCloudCodeModuleGenerateBindings;
        readonly IDisplayDialog m_Dialog;

        public CloudCodeModuleGenerateBindingsCommand(
            ICloudCodeModuleBindingsGenerator editorCloudCodeModuleGenerateBindings,
            IDisplayDialog dialog)
        {
            m_EditorCloudCodeModuleGenerateBindings = editorCloudCodeModuleGenerateBindings;
            m_Dialog = dialog;
        }

        public override async Task ExecuteAsync(
            IEnumerable<CloudCodeModuleReference> ccmrs,
            CancellationToken cancellationToken = default)
        {
            if (!m_Dialog.Show(
                "Generate Bindings",
                "Binding generation has changed, it may require addressing client code.\nPlease see the changelog for details.\n\n Do you wish to continue?",
                dialogOptOutDecisionType: DialogOptOutDecisionType.ForThisMachine,
                dialogOptOutDecisionStorageKey: k_DialogOptOutKey
            ))
            {
                return;
            }

            var results =
                await m_EditorCloudCodeModuleGenerateBindings.GenerateModuleBindings(ccmrs, cancellationToken);

            var failedResults = results
                .Select(x => x.Exception)
                .Where(x => x != null).ToList();

            CloudCodeAuthoringServices.Instance.GetService<ICloudCodeModuleBindingsGenerationAnalytics>()
                .SendCodeGenerationFromCommandEvent(
                    failedResults.Any() ? new AggregateException(failedResults) : null);
        }
    }
}
