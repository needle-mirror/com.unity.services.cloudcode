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
using Unity.Services.CloudCode.Editor.Shared.DependencyInversion;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.Collections;
using Unity.Services.DeploymentApi.Editor;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;

using UnityEditor;
using UnityEngine;
#if UNITY_6000_3_OR_NEWER
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment;
#endif
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules
{
    class CloudCodeModuleReferenceDeployCommand : Command<CloudCodeModuleReference>
    {
        public override string Name => L10n.Tr("Deploy");
        readonly IModuleBuilder m_ModuleBuilder;

        readonly CloudCodeDeploymentHandler m_CloudCodeDeploymentHandler;
        readonly IDeploymentAnalytics m_Analytics;
        readonly IDashboardUrlResolver m_DashboardUrlResolver;
        readonly bool m_Reconcile;
        readonly bool m_DryRun;
#if UNITY_6000_3_OR_NEWER
        readonly CloudCodeModuleReferenceLocalDeployCommand m_CloudCodeModuleReferenceLocalDeployCommand;
        readonly ILastSuccessfulDeploymentStore m_DeploymentStore;
#endif

#if UNITY_6000_3_OR_NEWER
        public CloudCodeModuleReferenceDeployCommand(
            IModuleBuilder moduleBuilder,
            ICloudCodeModulesClient modulesClient,
            IDeploymentAnalytics analytics,
            ILogger logger,
            IPreDeployValidator validator,
            CloudCodeModuleReferenceLocalDeployCommand cloudCodeLocalModuleDeployCommand,
            IDashboardUrlResolver dashboardUrlResolver,
            ILastSuccessfulDeploymentStore deploymentStore
            )
        {
            m_ModuleBuilder = moduleBuilder;
            m_CloudCodeDeploymentHandler =
                new CloudCodeDeploymentHandler(modulesClient, analytics, logger, validator, DeploymentAssetKind.ModuleReference);
            m_Analytics = analytics;
            m_CloudCodeModuleReferenceLocalDeployCommand = cloudCodeLocalModuleDeployCommand;
            m_DashboardUrlResolver = dashboardUrlResolver;
            m_DeploymentStore = deploymentStore;
            m_Reconcile = false;
            m_DryRun = false;
        }

#else
        public CloudCodeModuleReferenceDeployCommand(
            IModuleBuilder moduleBuilder,
            ICloudCodeModulesClient modulesClient,
            IDeploymentAnalytics analytics,
            ILogger logger,
            IPreDeployValidator validator,
            IDashboardUrlResolver dashboardUrlResolver
            )
        {
            m_ModuleBuilder = moduleBuilder;

            m_CloudCodeDeploymentHandler =
                new CloudCodeDeploymentHandler(modulesClient, analytics, logger, validator, DeploymentAssetKind.ModuleReference);
            m_Analytics = analytics;
            m_DashboardUrlResolver = dashboardUrlResolver;
            m_Reconcile = false;
            m_DryRun = false;
        }

#endif

        public override async Task ExecuteAsync(IEnumerable<CloudCodeModuleReference> items, CancellationToken cancellationToken = new CancellationToken())
        {
#if UNITY_6000_3_OR_NEWER
            // If the User is using a Local Cloud Code server, direct all deployments to it.
            if (m_CloudCodeModuleReferenceLocalDeployCommand.ShouldDeployToLocal())
            {
                await m_CloudCodeModuleReferenceLocalDeployCommand.ExecuteAsync(items, cancellationToken);
                return;
            }
#endif

            // Else, deploy to Remote Cloud Code as usual.
            var cloudCodeModuleReferences = items.ToList();
            OnDeploy(cloudCodeModuleReferences);
            var deployStartedAtUtc = DateTime.UtcNow;
            var sourceAtDeployStart = SnapshotSource(cloudCodeModuleReferences);
            var compiled = await Compile(cloudCodeModuleReferences, cancellationToken);
            ReportCompileFailures(m_Analytics, cloudCodeModuleReferences, DeploymentTarget.Remote);
            cancellationToken.ThrowIfCancellationRequested();
            DeployResult result = null;
            try
            {
                result = await m_CloudCodeDeploymentHandler.DeployAsync(compiled.Values.ToList(), m_Reconcile, m_DryRun);
            }
            catch (DeploymentException e)
            {
                // A mixed batch throws AFTER some scripts already reached the backend. Record those
                // successes before rethrowing, otherwise the modules that did deploy keep no baseline.
                await CompleteDeployment(compiled, e.Result, deployStartedAtUtc, sourceAtDeployStart);
                throw;
            }

            if (result != null)
                await CompleteDeployment(compiled, result, deployStartedAtUtc, sourceAtDeployStart);
            var dashboardUrl = await m_DashboardUrlResolver.CloudCodeModules();
            Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "[Cloud Code] Cloud Code Modules are deployed to the remote server successfully. <a href=\"{0}\">View on Dashboard</a>", dashboardUrl);
        }

        internal readonly struct SourceSnapshot
        {
            public readonly DateTime LastWriteUtc;
            public readonly string Fingerprint;

            public SourceSnapshot(DateTime lastWriteUtc, string fingerprint)
            {
                LastWriteUtc = lastWriteUtc;
                Fingerprint = fingerprint;
            }
        }

        /// <summary>
        /// Records the last successful remote deployment of each module reference, with a fingerprint of its
        /// source taken when the deploy started. Success is what the handler reports
        /// in <see cref="DeployResult.Deployed"/>, never inferred from status severity.
        /// </summary>
        internal void RecordSuccessfulDeployments(
            IReadOnlyDictionary<CloudCodeModuleReference, IScript> compiled, DeployResult result, DateTime deployStartedAtUtc,
            IReadOnlyDictionary<CloudCodeModuleReference, SourceSnapshot> sourceAtDeployStart)
        {
#if UNITY_6000_3_OR_NEWER
            var uploadCompletedAtUtc = DateTime.UtcNow;
            foreach (var entry in compiled)
            {
                if (!result.Deployed.Contains(entry.Value))
                    continue;

                sourceAtDeployStart.TryGetValue(entry.Key, out var source);
                var deploymentInfo = new LastSuccessfulDeploymentInfo
                {
                    Target = LastSuccessfulDeploymentInfo.DeploymentTarget.Remote,
                    DeployedAtUtc = deployStartedAtUtc,
                    UploadCompletedAtUtc = uploadCompletedAtUtc,
                    LastDeployedContentHash = source.Fingerprint
                };
                m_DeploymentStore.Record(entry.Key, deploymentInfo, source.LastWriteUtc);
                entry.Key.UpdateLogStatus(DeploymentStatusOwnership.Deployed);
            }
#endif
        }

        async Task CompleteDeployment(
            IReadOnlyDictionary<CloudCodeModuleReference, IScript> compiled, DeployResult result, DateTime deployStartedAtUtc,
            IReadOnlyDictionary<CloudCodeModuleReference, SourceSnapshot> sourceAtDeployStart)
        {
            RecordSuccessfulDeployments(compiled, result, deployStartedAtUtc, sourceAtDeployStart);
            await ReconcileAfterDeployAsync(compiled, result);
        }

        static Dictionary<CloudCodeModuleReference, SourceSnapshot> SnapshotSource(List<CloudCodeModuleReference> items)
        {
            var snapshot = new Dictionary<CloudCodeModuleReference, SourceSnapshot>();
            foreach (var ccmr in items)
            {
#if UNITY_6000_3_OR_NEWER
                snapshot[ccmr] = new SourceSnapshot(
                    LocalAutoDeployer.SourceLastWriteUtcFor(ccmr), LocalAutoDeployer.SourceFingerprintFor(ccmr));
#else
                snapshot[ccmr] = default;
#endif
            }

            return snapshot;
        }

        static async Task ReconcileAfterDeployAsync(
            IReadOnlyDictionary<CloudCodeModuleReference, IScript> compiled, DeployResult result)
        {
#if UNITY_6000_3_OR_NEWER
            ModuleReferenceModifiedTracker tracker;
            try
            {
                tracker = CloudCodeAuthoringServices.Instance.GetService<ModuleReferenceModifiedTracker>();
            }
            catch (Exception e) when (e is DependencyNotFoundException or NullReferenceException)
            {
                return;
            }

            foreach (var entry in compiled)
            {
                if (result.Deployed.Contains(entry.Value))
                    await tracker.ReconcileAsync(entry.Key);
            }
#else
            await Task.CompletedTask;
#endif
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

        internal async Task<Dictionary<CloudCodeModuleReference, IScript>> Compile(
            IEnumerable<CloudCodeModuleReference> items, CancellationToken cancellationToken = default)
        {
            var generationList = new Dictionary<CloudCodeModuleReference, IScript>();
            foreach (var ccmr in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await m_ModuleBuilder.CreateCloudCodeModuleFromSolution(ccmr, cancellationToken);

                    // Cancelled after publishing, the builder returns with CcmPath still on an earlier zip.
                    cancellationToken.ThrowIfCancellationRequested();
                    if (ccmr.Status.MessageSeverity == SeverityLevel.Error)
                    {
                        continue;
                    }
                    generationList.Add(ccmr, GenerateModule(ccmr));
                }
                catch (Exception e)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ccmr.UpdateLogStatus(new DeploymentStatus("Failed to compile", e.Message, SeverityLevel.Error));
                }
            }

            return generationList;
        }

        // A reference that fails to compile never reaches the deployment handler, which is what
        // reports. Matched on the status both compile loops use to omit it.
        internal static void ReportCompileFailures(
            IDeploymentAnalytics analytics,
            IEnumerable<CloudCodeModuleReference> ccmrs,
            DeploymentTarget target)
        {
            var failed = ccmrs
                .Where(ccmr => ccmr.Status.MessageSeverity == SeverityLevel.Error)
                .Select(ccmr => ccmr.ModuleName)
                .ToList();

            if (failed.Count == 0)
                return;

            analytics.SendFailureDeploymentEvent(
                ModuleBuilderStatuses.FailedToCompile,
                AnalyticsErrorCode.k_CompilationFailed,
                null,
                DeploymentAssetKind.ModuleReference,
                target,
                failed);
        }

        // Deployed under the solution's main entry project name. Falls back to the asset name when the
        // solution cannot be read.
        internal static string GetDeployModuleName(CloudCodeModuleReference ccmr, IModuleProjectRetriever retriever)
        {
            try
            {
                var name = string.IsNullOrEmpty(ccmr.ModuleName)
                    ? retriever.GetMainEntryProjectName(ccmr.SolutionPath)
                    : ccmr.ModuleName;
                return System.IO.Path.GetFileNameWithoutExtension(name);
            }
            catch (Exception)
            {
                return System.IO.Path.GetFileNameWithoutExtension(ccmr.Name);
            }
        }

        internal static Module GenerateModule(CloudCodeModuleReference moduleReference)
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
