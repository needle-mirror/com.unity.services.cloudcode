#if UNITY_SERVICES_CLOUDCODE_EXPERIMENTAL
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment
{
    class CloudCodeLocalModuleDeployCommand : Command<CloudCodeModuleReference>
    {
        public override string Name => L10n.Tr("Deploy Local");
        readonly EditorCloudCodeLocalModuleDeploymentHandler m_DeployHandler;
        readonly IModuleBuilder m_ModuleBuilder;

        internal CloudCodeLocalModuleDeployCommand(
            IModuleBuilder moduleBuilder,
            EditorCloudCodeLocalModuleDeploymentHandler deployHandler)
        {
            m_ModuleBuilder = moduleBuilder;
            m_DeployHandler = deployHandler;
        }

        internal bool ShouldDeployToLocal()
        {
            var server = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
            return server.GetCurrentServerStatus() == ICloudCodeLocalServer.LocalCloudCodeServerStatus.Started;
        }

        public override async Task ExecuteAsync(IEnumerable<CloudCodeModuleReference> items,
            CancellationToken cancellationToken = new CancellationToken())
        {
            var ccmrs = items.ToList();
            m_DeployHandler.ClearDeploymentStatuses(ccmrs);

            // Sanity check, only able to deploy if the local cloud code has started.
            var server = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
            if (server.GetCurrentServerStatus() != ICloudCodeLocalServer.LocalCloudCodeServerStatus.Started)
            {
                const string kFailureMessage = "Local Server Offline";
                m_DeployHandler.UpdateDeployStatuses(ccmrs, kFailureMessage, severity: SeverityLevel.Error);
                throw new Exception(kFailureMessage);
            }

            // Else continue deployment
            await CompileAndDeployAsync(ccmrs, cancellationToken);
        }

        internal async Task<string> CompileAndDeployAsync(List<CloudCodeModuleReference> ccmrs,
            CancellationToken cancellationToken = new CancellationToken())
        {
            // First compile and zip the Modules in preparation for deploy
            var runtimeIdentifier = GetRuntimeIdentifier(ccmrs);
            var compiled = await CompileForDebug(ccmrs, runtimeIdentifier, cancellationToken);

            // Deploy to the local server's path referencing all modules
            return await m_DeployHandler.DeployAsync(compiled, cancellationToken);
        }

        string GetRuntimeIdentifier(List<CloudCodeModuleReference> ccmrs)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "win-x64";
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "osx-arm64" : "osx-x64";
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64";
            }

            m_DeployHandler.UpdateDeployStatuses(ccmrs, "Failed to compile ", "Unsupported platform.", SeverityLevel.Error);
            throw new Exception("Unsupported platform.");
        }

        async Task<Dictionary<IModuleItem, IScript>> CompileForDebug(List<CloudCodeModuleReference> items, string operatingSystem,
            CancellationToken cancellationToken = default)
        {
            var allReferencedModulesToDeploy = new Dictionary<IModuleItem, IScript>();
            foreach (var ccmr in items)
            {
                try
                {
                    m_DeployHandler.UpdateDeployStatus(ccmr, "Compiling...", severity: SeverityLevel.Info, shouldLog: false);
                    await m_ModuleBuilder.CreateCloudCodeModuleFromSolution(ccmr, cancellationToken, operatingSystem, "Debug");
                    if (ccmr.Status.MessageSeverity == SeverityLevel.Error)
                    {
                        continue;
                    }

                    var moduleToDeploy = CloudCodeModuleDeployCommand.GenerateModule(ccmr);
                    allReferencedModulesToDeploy.Add(ccmr, moduleToDeploy);

                    // Do not continue if a cancellation was requested
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (OperationCanceledException e)
                {
                    m_DeployHandler.UpdateDeployStatuses(items, "Cancelled", e.Message, severity: SeverityLevel.Warning);
                    throw;
                }
                catch (Exception e)
                {
                    m_DeployHandler.UpdateDeployStatus(ccmr, "Failed to compile", e.Message, severity: SeverityLevel.Error);
                }
            }

            return allReferencedModulesToDeploy;
        }
    }
}
#endif
