using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules
{
    class NativeModuleGenerateBindingsCommand : Command<NativeModuleReference>
    {
        // TODO: Remove the "NOT IMPLEMENTED" note once this command is implemented.
        public override string Name => L10n.Tr("Generate Code Bindings (NOT IMPLEMENTED)");
        public override Task ExecuteAsync(IEnumerable<NativeModuleReference> items, CancellationToken cancellationToken = default)
        {
            // TODO: Implement native binding generation. This is blocked by [MTT-14382](https://jira.unity3d.com/browse/MTT-14382).
            throw new NotImplementedException(
                "Implement native binding generation. This is blocked by [MTT-14382](https://jira.unity3d.com/browse/MTT-14382).");
        }
    }
}
