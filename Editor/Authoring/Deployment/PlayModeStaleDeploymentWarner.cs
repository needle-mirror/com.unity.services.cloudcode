#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Editor.Shared.EditorUtils;
using Unity.Services.Core.Editor.Environments;
using UnityEditor;
using UnityEngine;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;
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
            SourceNewerOnDisk,
            NeverDeployed
        }

        readonly ICloudCodeLocalServer m_LocalServer;
        readonly IEnvironmentsApi m_EnvironmentsApi;
        readonly RemoteModuleStaleness m_Staleness;
        readonly IEnumerable<CloudCodeModuleReference> m_ModuleReferences;
        readonly ILogger m_Logger;
        readonly Func<bool> m_IsBatchMode;
#if UNITY_6000_5_OR_NEWER
        readonly IEnumerable<CloudCodeModule> m_Modules;
#endif

        bool m_EvaluationInFlight;

        internal Task LastEvaluation { get; private set; }

        public PlayModeStaleDeploymentWarner(
            ICloudCodeLocalServer localServer,
            IEnvironmentsApi environmentsApi,
            RemoteModuleStaleness staleness,
            CloudCodeModuleReferenceCollection moduleReferences,
#if UNITY_6000_5_OR_NEWER
            CloudCodeModuleCollection modules,
#endif
            ILogger logger)
            : this(
                localServer,
                environmentsApi,
                staleness,
                (IEnumerable<CloudCodeModuleReference>)moduleReferences,
#if UNITY_6000_5_OR_NEWER
                (IEnumerable<CloudCodeModule>)modules,
#endif
                logger,
                () => Application.isBatchMode)
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        internal PlayModeStaleDeploymentWarner(
            ICloudCodeLocalServer localServer,
            IEnvironmentsApi environmentsApi,
            RemoteModuleStaleness staleness,
            IEnumerable<CloudCodeModuleReference> moduleReferences,
#if UNITY_6000_5_OR_NEWER
            IEnumerable<CloudCodeModule> modules,
#endif
            ILogger logger,
            Func<bool> isBatchMode)
        {
            m_LocalServer = localServer;
            m_EnvironmentsApi = environmentsApi;
            m_Staleness = staleness;
            m_ModuleReferences = moduleReferences;
#if UNITY_6000_5_OR_NEWER
            m_Modules = modules;
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
                || !EnvironmentIds.IsSet(m_EnvironmentsApi.ActiveEnvironmentId))
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
                var stale = await CollectStaleAsync();
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

        /// <summary>
        /// The modules whose source changed since their last deploy to the remote environment. A module
        /// never deployed remotely in this session has nothing to compare against: it is left out, unless
        /// <paramref name="includeNeverDeployed"/> asks for it, for a caller that knows a deploy happened.
        /// </summary>
        internal async Task<List<(string Name, StaleReason Reason)>> CollectStaleAsync(bool includeNeverDeployed = false)
        {
            var stale = new List<(string Name, StaleReason Reason)>();
#if UNITY_6000_5_OR_NEWER
            await CollectStaleModulesAsync(stale, includeNeverDeployed);
#endif
            await CollectStaleModuleReferencesAsync(stale, includeNeverDeployed);
            return stale;
        }

#if UNITY_6000_5_OR_NEWER
        async Task CollectStaleModulesAsync(List<(string Name, StaleReason Reason)> stale, bool includeNeverDeployed)
        {
            foreach (var module in m_Modules.ToList())
            {
                var verdict = await m_Staleness.EvaluateAsync(module);
                if (TryGetReason(verdict, includeNeverDeployed, out var reason))
                    stale.Add((module.Name, reason));
            }
        }

#endif

        async Task CollectStaleModuleReferencesAsync(List<(string Name, StaleReason Reason)> stale, bool includeNeverDeployed)
        {
            foreach (var ccmr in m_ModuleReferences.ToList())
            {
                var verdict = await m_Staleness.EvaluateAsync(ccmr);
                if (TryGetReason(verdict, includeNeverDeployed, out var reason))
                    stale.Add((ccmr.Name, reason));
            }
        }

        // A missing record is the normal state of a fresh session, not evidence of staleness.
        static bool TryGetReason(StalenessVerdict verdict, bool includeNeverDeployed, out StaleReason reason)
        {
            switch (verdict.Reason)
            {
                case StalenessReason.ModifiedSinceLastDeploy:
                    reason = StaleReason.ModifiedSinceLastDeploy;
                    return true;
                case StalenessReason.SourceChangedOnDisk:
                    reason = StaleReason.SourceNewerOnDisk;
                    return true;
                case StalenessReason.NoRemoteRecord:
                    reason = StaleReason.NeverDeployed;
                    return includeNeverDeployed;
                default:
                    reason = default;
                    return false;
            }
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
                case StaleReason.NeverDeployed:
                    return "not deployed yet";
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
