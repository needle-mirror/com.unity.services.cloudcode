#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration;
using Unity.Services.CloudCode.Authoring.Editor.Core.Dotnet;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Editor.Shared.DependencyInversion;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment
{
    class CloudCodeModuleReferenceLocalDeployCommand : Command<CloudCodeModuleReference>
    {
        public override string Name => L10n.Tr("Deploy Local");
        readonly EditorCloudCodeLocalModuleDeploymentHandler m_DeployHandler;
        readonly IModuleBuilder m_ModuleBuilder;
        readonly IDeploymentAnalytics m_Analytics;
        readonly ILastSuccessfulDeploymentStore m_DeploymentStore;

        internal CloudCodeModuleReferenceLocalDeployCommand(
            IModuleBuilder moduleBuilder,
            EditorCloudCodeLocalModuleDeploymentHandler deployHandler,
            IDeploymentAnalytics analytics,
            ILastSuccessfulDeploymentStore deploymentStore)
        {
            m_ModuleBuilder = moduleBuilder;
            m_DeployHandler = deployHandler;
            m_Analytics = analytics;
            m_DeploymentStore = deploymentStore;
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
            await CompileAndDeployAsync(ccmrs, DeploymentOrigin.Manual, cancellationToken);
        }

        internal async Task<string> CompileAndDeployAsync(List<CloudCodeModuleReference> ccmrs,
            DeploymentOrigin origin,
            CancellationToken cancellationToken = new CancellationToken())
        {
            // A module's status is the highest severity in its log, and CompileForDebug skips a module in
            // Error, so a failure left from an earlier attempt would keep a fixed module from deploying.
            m_DeployHandler.ClearDeploymentStatuses(ccmrs);

            var deployStartedAtUtc = DateTime.UtcNow;
            var sourceLastWriteUtcAtDeployStart = ccmrs.ToDictionary(ccmr => ccmr, LocalAutoDeployer.SourceLastWriteUtcFor);

            // First compile and zip the Modules in preparation for deploy
            var runtimeIdentifier = GetRuntimeIdentifier(ccmrs);
            var compiled = await CompileForDebug(ccmrs, runtimeIdentifier, cancellationToken);

            if (origin == DeploymentOrigin.Manual)
            {
                CloudCodeModuleReferenceDeployCommand.ReportCompileFailures(
                    m_Analytics, ccmrs, DeploymentTarget.Local);
            }

            // Deploy to the local server's path referencing all modules
            var moduleDestinationDir = await m_DeployHandler.DeployAsync(
                compiled, DeploymentAssetKind.ModuleReference, origin, cancellationToken);

            RecordSuccessfulDeployments(compiled.Keys, deployStartedAtUtc, sourceLastWriteUtcAtDeployStart);
            await ReconcileAfterDeployAsync(compiled.Keys.OfType<CloudCodeModuleReference>());
            return moduleDestinationDir;
        }

        static async Task ReconcileAfterDeployAsync(IEnumerable<CloudCodeModuleReference> ccmrs)
        {
            ModuleReferenceModifiedTracker tracker;
            try
            {
                tracker = CloudCodeAuthoringServices.Instance.GetService<ModuleReferenceModifiedTracker>();
            }
            catch (Exception e) when (e is DependencyNotFoundException or NullReferenceException)
            {
                return;
            }

            foreach (var ccmr in ccmrs)
                await tracker.ReconcileAsync(ccmr);
        }

        // Success is membership in the compiled set that was handed to the deploy, never inferred from status severity.
        void RecordSuccessfulDeployments(
            IEnumerable<IModuleItem> deployedItems, DateTime deployStartedAtUtc,
            IReadOnlyDictionary<CloudCodeModuleReference, DateTime> sourceLastWriteUtcAtDeployStart)
        {
            foreach (var ccmr in deployedItems.OfType<CloudCodeModuleReference>())
            {
                var deploymentInfo = new LastSuccessfulDeploymentInfo
                {
                    Target = LastSuccessfulDeploymentInfo.DeploymentTarget.Local,
                    DeployedAtUtc = deployStartedAtUtc,
                    LastDeployedContentHash = null
                };
                m_DeploymentStore.Record(ccmr, deploymentInfo,
                    sourceLastWriteUtcAtDeployStart.TryGetValue(ccmr, out var sourceLastWriteUtc) ? sourceLastWriteUtc : default);
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

                    var moduleToDeploy = CloudCodeModuleReferenceDeployCommand.GenerateModule(ccmr);
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
                    var reportedByDotnet = e is DotnetCommandFailedException dotnetFailure
                        && dotnetFailure.DiagnosticsReported;

                    m_DeployHandler.UpdateDeployStatus(
                        ccmr, ModuleBuilderStatuses.FailedToCompile, e.Message,
                        severity: SeverityLevel.Error, logToConsole: !reportedByDotnet);
                }
            }

            return allReferencedModulesToDeploy;
        }
    }
}
#endif
