#if UNITY_6000_5_OR_NEWER
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Editor.Shared.Assets;
using Unity.Services.CloudCode.Editor.Shared.EditorUtils;
using Unity.Services.DeploymentApi.Editor;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    class CloudCodeModuleModifiedTracker : IDisposable
    {
        readonly IEnumerable<CloudCodeModule> m_CloudCodeModules;
        readonly AssetPostprocessorProxy m_PostprocessorProxy;
        readonly IModuleContentHasher m_ContentHasher;
        readonly ILogger m_Logger;

        /// <summary>
        /// Per-module reconcile counter: a reconcile captures the value at start and drops its result if a
        /// newer reconcile for the same module has since bumped it (last writer wins across async overlap).
        /// Keyed by entity id rather than the module so a stale entry never keeps a destroyed asset alive.
        /// </summary>
        readonly Dictionary<UnityEngine.EntityId, int> m_Generations = new Dictionary<UnityEngine.EntityId, int>();
        bool m_Disposed;

        public CloudCodeModuleModifiedTracker(
            CloudCodeModuleCollection cloudCodeModules,
            AssetPostprocessorProxy assetPostprocessorProxy,
            IModuleContentHasher contentHasher,
            ILogger logger)
            : this((IEnumerable<CloudCodeModule>)cloudCodeModules, assetPostprocessorProxy, contentHasher, logger)
        {
        }

        internal CloudCodeModuleModifiedTracker(
            IEnumerable<CloudCodeModule> cloudCodeModules,
            AssetPostprocessorProxy assetPostprocessorProxy,
            IModuleContentHasher contentHasher,
            ILogger logger)
        {
            m_CloudCodeModules = cloudCodeModules;
            m_PostprocessorProxy = assetPostprocessorProxy;
            m_ContentHasher = contentHasher;
            m_Logger = logger;
            m_PostprocessorProxy.AllAssetsPostprocessed += OnAllAssetsPostprocessed;

            if (m_CloudCodeModules is INotifyCollectionChanged observableModules)
                observableModules.CollectionChanged += OnModulesChanged;

            // A domain reload (entering Play Mode, recompile) resets each module's non-serialized status.
            // This tracker is reconstructed afterwards with the already-repopulated collection, so restore
            // each module's status from its cached hash here - no file I/O, since a reload changes no files.
            RestoreAll();
        }

        void OnModulesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (CloudCodeModule module in e.OldItems)
                {
                    if (module != null)
                        m_Generations.Remove(module.GetEntityId());
                }
            }

            if (e.NewItems == null)
                return;

            foreach (CloudCodeModule module in e.NewItems)
                ReconcileFireAndForget(module);
        }

        void OnAllAssetsPostprocessed(object sender, PostProcessEventArgs e)
        {
            var hasTrackedChange = e.ImportedAssetPaths
                .Concat(e.DeletedAssetPaths)
                .Concat(e.MovedAssetPaths)
                .Concat(e.MovedFromAssetPaths)
                .Any(IsTrackedExtension);

            if (hasTrackedChange)
                ReconcileAll();
        }

        void ReconcileAll()
        {
            foreach (var module in m_CloudCodeModules)
                ReconcileFireAndForget(module);
        }

        void RestoreAll()
        {
            foreach (var module in m_CloudCodeModules)
                Restore(module);
        }

        /// <summary>
        /// Restores a module's status after a domain reload from its cached content hash, without reading
        /// any files. The cache is kept current by Reconcile on every tracked change, so a reload only has
        /// to compare it against the deployed baseline. Modules with no baseline or cache are left as-is.
        /// </summary>
        void Restore(CloudCodeModule module)
        {
            if (!IsReconcilable(module.Status))
                return;

            var baseline = module.LastSuccessfulDeployment?.ContentHash;
            if (string.IsNullOrEmpty(baseline) || string.IsNullOrEmpty(module.CurrentContentHash))
                return;

            module.Status = StatusFrom(module.CurrentContentHash, baseline);
        }

        /// <summary>
        /// Fire-and-forget reconcile for callers that cannot await (the void <see cref="ITrackableItem"/>
        /// entry point and the asset-pipeline events). The hash runs off the main thread; any failure is
        /// logged by <see cref="Sync.SafeAsync"/>.
        /// </summary>
        public void ReconcileFireAndForget(CloudCodeModule module)
        {
            _ = Sync.SafeAsync(() => ReconcileAsync(module));
        }

        /// <summary>
        /// Re-derives a module's status by hashing its current source and comparing it against the content
        /// captured at the last successful deploy, caching the result so a later reload can restore without
        /// re-hashing. Only states this tracker owns (empty/unknown/up-to-date/modified-locally) are
        /// touched; transient and error states are left alone. A module with no recorded deployment this
        /// session has no baseline to compare against and is left as-is.
        ///
        /// Awaiting guarantees the status has been written by the time it completes, so a caller that must
        /// be the last writer (the deploy command, right after recording the new baseline) can await it.
        /// </summary>
        public async Task ReconcileAsync(CloudCodeModule module)
        {
            if (!IsReconcilable(module.Status))
                return;

            var baseline = module.LastSuccessfulDeployment?.ContentHash;
            if (string.IsNullOrEmpty(baseline))
                return;

            var generation = NextGeneration(module);

            var current = await m_ContentHasher.ComputeHashAsync(module);

            // Back on the main thread (the await captured the editor's synchronization context). Drop the
            // result if the tracker was disposed, the module was destroyed by a domain reload, or a newer
            // reconcile for this module started while we were hashing.
            if (m_Disposed || module == null || !IsCurrentGeneration(module, generation))
                return;

            if (string.IsNullOrEmpty(current))
            {
                m_Logger.LogError(
                    $"Could not compute the content hash for Cloud Code Module '{module.name}'. Ensure it " +
                    "has a cloud and a client assembly definition and that its source files are readable.");
                return;
            }

            module.CurrentContentHash = current;
            module.Status = StatusFrom(current, baseline);
        }

        int NextGeneration(CloudCodeModule module)
        {
            var key = module.GetEntityId();
            var next = m_Generations.TryGetValue(key, out var current) ? current + 1 : 1;
            m_Generations[key] = next;
            return next;
        }

        bool IsCurrentGeneration(CloudCodeModule module, int generation)
        {
            return m_Generations.TryGetValue(module.GetEntityId(), out var current) && current == generation;
        }

        static DeploymentStatus StatusFrom(string current, string baseline)
        {
            return string.Equals(current, baseline, StringComparison.Ordinal)
                ? DeploymentStatus.UpToDate
                : DeploymentStatus.ModifiedLocally;
        }

        static bool IsTrackedExtension(string path)
        {
            return ModuleContentHasher.TrackedExtensions.Any(
                ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Reconcilable when the module carries no meaningful status yet (empty, or the default value left
        /// by a domain reload) or one of the two states derived from content.
        /// </summary>
        static bool IsReconcilable(DeploymentStatus status)
        {
            return IsEmptyOrUnknown(status)
                || Matches(status, DeploymentStatus.UpToDate)
                || Matches(status, DeploymentStatus.ModifiedLocally);
        }

        static bool IsEmptyOrUnknown(DeploymentStatus status)
        {
            return string.IsNullOrEmpty(status.Message) && status.MessageSeverity == SeverityLevel.None;
        }

        static bool Matches(DeploymentStatus status, DeploymentStatus other)
        {
            return status.Message == other.Message
                && status.MessageSeverity == other.MessageSeverity;
        }

        public void Dispose()
        {
            m_Disposed = true;
            m_Generations.Clear();
            m_PostprocessorProxy.AllAssetsPostprocessed -= OnAllAssetsPostprocessed;

            if (m_CloudCodeModules is INotifyCollectionChanged observableModules)
                observableModules.CollectionChanged -= OnModulesChanged;
        }
    }
}
#endif
