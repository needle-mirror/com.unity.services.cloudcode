#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Editor.Shared.EditorUtils;
using Unity.Services.Core.Editor.Environments;
using UnityEditor;
using UnityEngine;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    /// <summary>
    /// Warns on entering Play mode when the remote environment is stale relative to
    /// local module source. Only the remote environment matters: with the local server running,
    /// Play routes there and auto-deploy keeps it current.
    /// </summary>
    class PlayModeStaleDeploymentWarner : IDisposable
    {
        internal enum StaleReason
        {
            ModifiedSinceLastDeploy,
            SourceNewerOnDisk
        }

        readonly ICloudCodeLocalServer m_LocalServer;
        readonly IEnvironmentsApi m_EnvironmentsApi;
        readonly ILastSuccessfulDeploymentStore m_DeploymentStore;
        readonly IEnumerable<CloudCodeModuleReference> m_ModuleReferences;
        readonly ILogger m_Logger;
        readonly Func<bool> m_IsBatchMode;
#if UNITY_6000_5_OR_NEWER
        readonly IEnumerable<CloudCodeModule> m_Modules;
        readonly IModuleContentHasher m_ContentHasher;
#endif

        bool m_EvaluationInFlight;

        internal Task LastEvaluation { get; private set; }

        public PlayModeStaleDeploymentWarner(
            ICloudCodeLocalServer localServer,
            IEnvironmentsApi environmentsApi,
            ILastSuccessfulDeploymentStore deploymentStore,
            CloudCodeModuleReferenceCollection moduleReferences,
#if UNITY_6000_5_OR_NEWER
            CloudCodeModuleCollection modules,
            IModuleContentHasher contentHasher,
#endif
            ILogger logger)
            : this(
                localServer,
                environmentsApi,
                deploymentStore,
                (IEnumerable<CloudCodeModuleReference>)moduleReferences,
#if UNITY_6000_5_OR_NEWER
                (IEnumerable<CloudCodeModule>)modules,
                contentHasher,
#endif
                logger,
                () => Application.isBatchMode)
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal PlayModeStaleDeploymentWarner(
            ICloudCodeLocalServer localServer,
            IEnvironmentsApi environmentsApi,
            ILastSuccessfulDeploymentStore deploymentStore,
            IEnumerable<CloudCodeModuleReference> moduleReferences,
#if UNITY_6000_5_OR_NEWER
            IEnumerable<CloudCodeModule> modules,
            IModuleContentHasher contentHasher,
#endif
            ILogger logger,
            Func<bool> isBatchMode)
        {
            m_LocalServer = localServer;
            m_EnvironmentsApi = environmentsApi;
            m_DeploymentStore = deploymentStore;
            m_ModuleReferences = moduleReferences;
#if UNITY_6000_5_OR_NEWER
            m_Modules = modules;
            m_ContentHasher = contentHasher;
#endif
            m_Logger = logger;
            m_IsBatchMode = isBatchMode;
        }

        internal void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode
                || m_EvaluationInFlight
                || m_IsBatchMode()
                || !LocalAutoDeployer.IsMainEditor()
                || m_LocalServer.GetCurrentServerStatus()
                is LocalCloudCodeServerStatus.Started or LocalCloudCodeServerStatus.Starting
                || m_EnvironmentsApi.ActiveEnvironmentId == null)
            {
                return;
            }

            m_EvaluationInFlight = true;
            LastEvaluation = Sync.SafeAsync(EvaluateAndWarnAsync);
        }

        async Task EvaluateAndWarnAsync()
        {
            try
            {
                var stale = new List<(string Name, StaleReason Reason)>();
#if UNITY_6000_5_OR_NEWER
                await CollectStaleModulesAsync(stale);
#endif
                await CollectStaleModuleReferencesAsync(stale);

                if (stale.Count == 0)
                    return;

                m_Logger.LogWarning(ComposeWarning(m_EnvironmentsApi.ActiveEnvironmentName, stale));
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // This is a best-effort check, so its own failures must not fill the Console.
                // Logging at verbose keeps an environment quirk (unreadable
                // solution folder, mid-scan deletion...) from erroring on every play entry.
                m_Logger.LogVerbose($"Play mode stale-deployment check failed: {e.Message}");
            }
            finally
            {
                m_EvaluationInFlight = false;
            }
        }

#if UNITY_6000_5_OR_NEWER
        async Task CollectStaleModulesAsync(List<(string Name, StaleReason Reason)> stale)
        {
            foreach (var module in m_Modules.ToList())
            {
                // Records are session-scoped, so a missing remote record is the
                // default state of a fresh session, stay silent.
                var remote = m_DeploymentStore.GetRecord(module, DeploymentTarget.Remote);
                if (remote == null)
                    continue;

                // A remote record without a hash means the module could not be hashed when it was deployed;
                // there is nothing to compare, but the record still proves a remote deploy happened.
                if (string.IsNullOrEmpty(remote.LastDeployedContentHash))
                    continue;

                var current = module.CurrentContentHash;
                if (string.IsNullOrEmpty(current))
                    current = await m_ContentHasher.ComputeHashAsync(module);

                if (string.IsNullOrEmpty(current))
                    continue;

                if (!string.Equals(current, remote.LastDeployedContentHash, StringComparison.Ordinal))
                    stale.Add((module.Name, StaleReason.ModifiedSinceLastDeploy));
            }
        }

#endif

        async Task CollectStaleModuleReferencesAsync(List<(string Name, StaleReason Reason)> stale)
        {
            var toScan = new List<(string Name, string SourceDir, DateTime FloorUtc)>();
            foreach (var ccmr in m_ModuleReferences.ToList())
            {
                // Records are session-scoped, so a missing remote record is the
                // default state of a fresh session, not evidence of staleness - stay silent.
                var remote = m_DeploymentStore.GetRecord(ccmr, DeploymentTarget.Remote);
                if (remote == null)
                    continue;

                var sourceDir = LocalAutoDeployer.GetSourceDir(ccmr);
                if (sourceDir == null)
                    continue;

                // The staleness floor is the LATER of the deploy's wall-clock time and the newest
                // source mtime snapshotted at that deploy. Comparing against the deploy time alone breaks on
                // a file whose mtime lies in the future (a clock-skewed archive, VM copy, or cloud-sync write).
                // Entries persisted before the snapshot existed read as the default, which falls back to the clock-only comparison.
                var sourceSnapshotUtc = m_DeploymentStore.GetSourceLastWriteUtc(ccmr, DeploymentTarget.Remote);
                var floorUtc = remote.DeployedAtUtc > sourceSnapshotUtc ? remote.DeployedAtUtc : sourceSnapshotUtc;
                toScan.Add((ccmr.Name, sourceDir, floorUtc));
            }

            if (toScan.Count == 0)
                return;

            var changed = await Task.Run(() => toScan
                .Where(candidate => LocalAutoDeployer.HasSourceNewerThanSafe(candidate.SourceDir, candidate.FloorUtc))
                .Select(candidate => candidate.Name)
                .ToList());

            stale.AddRange(changed.Select(name => (name, StaleReason.SourceNewerOnDisk)));
        }

        internal static string ComposeWarning(
            string environmentName, IReadOnlyList<(string Name, StaleReason Reason)> stale)
        {
            var lead = stale.All(item => item.Reason == StaleReason.ModifiedSinceLastDeploy)
                ? $"Cloud Code changes have not been deployed to the remote environment '{environmentName}'"
                : $"Cloud Code modules may be out of date in the remote environment '{environmentName}'";

            var items = string.Join(", ", stale.Select(item => $"{item.Name} ({ReasonText(item.Reason)})"));

            return $"{lead}: {items}. Play mode is calling the previously deployed cloud code. " +
                "Deploy from the Deployment window, or start the local Cloud Code server to run changes locally.";
        }

        static string ReasonText(StaleReason reason)
        {
            switch (reason)
            {
                case StaleReason.ModifiedSinceLastDeploy:
                    return "modified since last deploy";
                case StaleReason.SourceNewerOnDisk:
                    return "source files changed on disk since the last deploy";
                default:
                    return reason.ToString();
            }
        }

        public void Dispose()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }
    }
}
#endif
