using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
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

            // Sanity check, only able to deploy if the local cloud code has started.
            var server = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
            if (server.GetCurrentServerStatus() != ICloudCodeLocalServer.LocalCloudCodeServerStatus.Started)
            {
                const string kFailureMessage = "Local Server Offline";
                m_DeployHandler.UpdateDeployStatus(ccmrs, kFailureMessage, severity: SeverityLevel.Error);
                throw new Exception(kFailureMessage);
            }

            // Else continue deployment
            await CompileAndDeployAsync(ccmrs, cancellationToken);
        }

        internal async Task<string> CompileAndDeployAsync(List<CloudCodeModuleReference> ccmrs,
            CancellationToken cancellationToken = new CancellationToken())
        {
            m_DeployHandler.ClearDeploymentStatus(ccmrs);
            m_DeployHandler.UpdateDeployStatus(ccmrs, "Compiling...", severity: SeverityLevel.Info, shouldLog: false);

            // First compile and zip the Modules in preprataion for deploy
            var runtimeIdentifier = GetRuntimeIdentifier(ccmrs);
            // Throws on cancellation internally
            var compiled = await CompileForDebug(ccmrs, runtimeIdentifier, cancellationToken);

            // Deploy to the local server's path referencing all modules
            try
            {
                m_DeployHandler.UpdateDeployStatus(ccmrs, "Deploying...", severity: SeverityLevel.Info);
                // Throws on cancellation internally
                var deployedLocation = await m_DeployHandler.DeployAsync(compiled, cancellationToken);
                m_DeployHandler.UpdateDeployStatus(ccmrs, "Deployed Successfully", severity: SeverityLevel.Info);
                m_DeployHandler.UpdateDeployStatus(ccmrs, "Up to date", severity: SeverityLevel.Success);
                m_DeployHandler.SetDeployStatusWithState(ccmrs, "Deployed to Local Server", severity: SeverityLevel.Info);

                return deployedLocation;
            }
            catch (Exception e)
            {
                m_DeployHandler.UpdateDeployStatus(ccmrs, "Deployment Failed ", detail: e.Message,
                    severity: SeverityLevel.Error);
                throw;
            }
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

            m_DeployHandler.UpdateDeployStatus(ccmrs, "Failed to compile ", "Unsupported platform.", SeverityLevel.Error);
            throw new Exception("Unsupported platform.");
        }

        async Task<List<IScript>> CompileForDebug(List<CloudCodeModuleReference> items, string operatingSystem,
            CancellationToken cancellationToken = default)
        {
            var generationList = new List<IScript>();
            foreach (var ccmr in items)
            {
                try
                {
                    await m_ModuleBuilder.CreateCloudCodeModuleFromSolution(ccmr, cancellationToken, operatingSystem, "Debug");
                    if (ccmr.Status.MessageSeverity == SeverityLevel.Error)
                    {
                        continue;
                    }

                    generationList.Add(GenerateModule(ccmr.ModuleName, ccmr.CcmPath));

                    // Do not continue if a cancellation was requested
                    cancellationToken.ThrowIfCancellationRequested();
                }
                catch (Exception e)
                {
                    ccmr.Status = new DeploymentStatus("Failed to compile", e.Message, SeverityLevel.Error);
                    throw;
                }
            }

            return generationList;
        }

        static IScript GenerateModule(string moduleName, string filePath)
        {
            var name = new ScriptName(moduleName);
            var script = new Script(filePath)
            {
                Name = name,
                Body = string.Empty,
                Parameters = new List<CloudCodeParameter>(),
                Language = Language.CS
            };

            return script;
        }
    }
}
