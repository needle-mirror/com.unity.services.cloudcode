#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Deployment;
using Unity.Services.CloudCode.Editor.Shared.EditorUtils;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    class ModuleReferenceModifiedTracker : IDisposable
    {
        readonly IEnumerable<CloudCodeModuleReference> m_ModuleReferences;
        readonly ILastSuccessfulDeploymentStore m_DeploymentStore;
        readonly IEnvironmentsApi m_EnvironmentsApi;

#if UNITY_6000_4_OR_NEWER
        readonly Dictionary<UnityEngine.EntityId, int> m_Generations = new Dictionary<UnityEngine.EntityId, int>();
#else
        readonly Dictionary<int, int> m_Generations = new Dictionary<int, int>();
#endif
        bool m_Disposed;

        public ModuleReferenceModifiedTracker(
            CloudCodeModuleReferenceCollection moduleReferences, ILastSuccessfulDeploymentStore deploymentStore,
            IEnvironmentsApi environmentsApi)
            : this((IEnumerable<CloudCodeModuleReference>)moduleReferences, deploymentStore, environmentsApi)
        {
            EditorApplication.focusChanged += OnFocusChanged;
            ScheduleReconcileAll();
        }

        internal ModuleReferenceModifiedTracker(
            IEnumerable<CloudCodeModuleReference> moduleReferences, ILastSuccessfulDeploymentStore deploymentStore,
            IEnvironmentsApi environmentsApi)
        {
            m_ModuleReferences = moduleReferences;
            m_DeploymentStore = deploymentStore;
            m_EnvironmentsApi = environmentsApi;

            m_EnvironmentsApi.PropertyChanged += OnEnvironmentsChanged;

            if (m_ModuleReferences is INotifyCollectionChanged observableModuleReferences)
                observableModuleReferences.CollectionChanged += OnModuleReferencesChanged;
        }

        void OnEnvironmentsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IEnvironmentsApi.ActiveEnvironmentId))
                ReconcileAll();
        }

        void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                ScheduleReconcileAll();
        }

        void ScheduleReconcileAll()
        {
            EditorApplication.delayCall += ReconcileAll;
        }

        void ReconcileAll()
        {
            if (m_Disposed)
                return;

            foreach (var moduleReference in m_ModuleReferences.ToList())
                ReconcileFireAndForget(moduleReference);
        }

        void OnModuleReferencesChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                m_Generations.Clear();
                ReconcileAll();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (CloudCodeModuleReference moduleReference in e.OldItems)
                {
                    if (moduleReference != null)
                        m_Generations.Remove(GenerationKey(moduleReference));
                }
            }

            if (e.NewItems == null)
                return;

            foreach (CloudCodeModuleReference moduleReference in e.NewItems)
            {
                if (moduleReference != null)
                    ReconcileFireAndForget(moduleReference);
            }
        }

        public void ReconcileFireAndForget(CloudCodeModuleReference moduleReference)
        {
            _ = Sync.SafeAsync(() => ReconcileAsync(moduleReference));
        }

        public async Task ReconcileAsync(CloudCodeModuleReference moduleReference)
        {
            if (!DeploymentStatusOwnership.IsReconcilable(moduleReference.Status))
                return;

            var freshest = m_DeploymentStore.FreshestRecord(moduleReference);
            if (freshest == null)
            {
                if (m_DeploymentStore.CanResolveKey(moduleReference)
                    && DeploymentStatusOwnership.IsContentDerived(moduleReference.Status))
                {
                    moduleReference.Status = DeploymentStatus.Empty;
                }
                return;
            }

            var sourceSnapshotUtc = m_DeploymentStore.GetSourceLastWriteUtc(moduleReference, freshest.Target);
            var floor = freshest.DeployedAtUtc > sourceSnapshotUtc ? freshest.DeployedAtUtc : sourceSnapshotUtc;
            var sourceDir = LocalAutoDeployer.GetSourceDir(moduleReference);
            var generation = NextGeneration(moduleReference);

            var stale = await Task.Run(() => ComputeStaleness(sourceDir, floor));

            if (m_Disposed || moduleReference == null || !IsCurrentGeneration(moduleReference, generation)
                || !DeploymentStatusOwnership.IsReconcilable(moduleReference.Status))
                return;

            if (stale == null)
                return;

            var next = stale.Value ? DeploymentStatus.ModifiedLocally : DeploymentStatus.UpToDate;

            if (DeploymentStatusOwnership.Matches(moduleReference.Status, next))
                return;

            moduleReference.Status = next;
        }

        int NextGeneration(CloudCodeModuleReference moduleReference)
        {
            var key = GenerationKey(moduleReference);
            var next = m_Generations.TryGetValue(key, out var current) ? current + 1 : 1;
            m_Generations[key] = next;
            return next;
        }

        bool IsCurrentGeneration(CloudCodeModuleReference moduleReference, int generation)
        {
            return m_Generations.TryGetValue(GenerationKey(moduleReference), out var current) && current == generation;
        }

#if UNITY_6000_4_OR_NEWER
        static UnityEngine.EntityId GenerationKey(CloudCodeModuleReference moduleReference)
        {
            return moduleReference.GetEntityId();
        }

#else
        static int GenerationKey(CloudCodeModuleReference moduleReference)
        {
            return moduleReference.GetInstanceID();
        }

#endif

        static bool? ComputeStaleness(string sourceDir, DateTime floor)
        {
            try
            {
                if (string.IsNullOrEmpty(sourceDir) || !System.IO.Directory.Exists(sourceDir))
                    return null;

                return LocalAutoDeployer.HasSourceNewerThan(sourceDir, floor);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void Dispose()
        {
            m_Disposed = true;
            m_Generations.Clear();
            EditorApplication.focusChanged -= OnFocusChanged;
            m_EnvironmentsApi.PropertyChanged -= OnEnvironmentsChanged;

            if (m_ModuleReferences is INotifyCollectionChanged observableModuleReferences)
                observableModuleReferences.CollectionChanged -= OnModuleReferencesChanged;
        }
    }
}
#endif
