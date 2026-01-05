using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.AdminApi;
using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.Collections;
using Unity.Services.DeploymentApi.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;

using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules
{
    class CloudCodeModuleDeployCommand : Command<CloudCodeModuleReference>
    {
        public override string Name => L10n.Tr("Deploy");

        readonly IModuleBuilder m_ModuleBuilder;

        readonly CloudCodeDeploymentHandler m_CloudCodeDeploymentHandler;
        readonly bool m_Reconcile;
        readonly bool m_DryRun;

        public CloudCodeModuleDeployCommand(
            IModuleBuilder moduleBuilder,
            ICloudCodeModulesClient modulesClient,
            IDeploymentAnalytics analytics,
            ILogger logger,
            IPreDeployValidator validator)
        {
            m_ModuleBuilder = moduleBuilder;

            m_CloudCodeDeploymentHandler =
                new CloudCodeDeploymentHandler(modulesClient, analytics, logger, validator);
            m_Reconcile = false;
            m_DryRun = false;
        }

        public override async Task ExecuteAsync(IEnumerable<CloudCodeModuleReference> items, CancellationToken cancellationToken = new CancellationToken())
        {
            var cloudCodeModuleReferences = items.ToList();
            OnDeploy(cloudCodeModuleReferences);
            var compiled = await Compile(cloudCodeModuleReferences, cancellationToken);
            await m_CloudCodeDeploymentHandler.DeployAsync(compiled, m_Reconcile, m_DryRun);
        }

        static void OnDeploy(IEnumerable<CloudCodeModuleReference> items)
        {
            items.ForEach(i =>
            {
                i.Progress = 0f;
                i.ClearLogStatus();
                i.States.Clear();
            });
        }

        internal async Task<List<IScript>> Compile(IEnumerable<CloudCodeModuleReference> items, CancellationToken cancellationToken = default)
        {
            var generationList = new List<IScript>();
            foreach (var ccmr in items)
            {
                try
                {
                    await m_ModuleBuilder.CreateCloudCodeModuleFromSolution(ccmr, cancellationToken);
                    if (ccmr.Status.MessageSeverity == SeverityLevel.Error)
                    {
                        continue;
                    }
                    generationList.Add(GenerateModule(ccmr));
                }
                catch (Exception e)
                {
                    ccmr.UpdateLogStatus(new DeploymentStatus("Failed to compile", e.Message, SeverityLevel.Error));
                }
            }

            return generationList;
        }

        static Module GenerateModule(CloudCodeModuleReference moduleReference)
        {
            var name = new ScriptName(moduleReference.ModuleName);
            var module = new Module(moduleReference.CcmPath, moduleReference)
            {
                Name = name,
                Body = string.Empty,
                Parameters = new List<CloudCodeParameter>(),
                Language = Language.CS
            };

            return module;
        }
    }
}
