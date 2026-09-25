#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.Core.Editor.Environments;
using UnityEditor;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;
#if DEPLOYMENT_API_AVAILABLE_V1_1
using IProjectID = Unity.Services.DeploymentApi.Editor.IProjectIdentifierProvider;
#else
using IProjectID = Unity.Services.CloudCode.Authoring.Editor.Deployment.IProjectIdentifierProvider;
#endif

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    enum LastSuccessfulDeploymentKind
    {
        Module,
        ModuleReference
    }

    interface ILastSuccessfulDeploymentStore
    {
        void Record(string assetPath, LastSuccessfulDeploymentKind kind, LastSuccessfulDeploymentInfo info, DateTime sourceLastWriteUtc = default);
        LastSuccessfulDeploymentInfo GetRecord(string assetPath, LastSuccessfulDeploymentKind kind, DeploymentTarget target);
        DateTime GetSourceLastWriteUtc(string assetPath, LastSuccessfulDeploymentKind kind, DeploymentTarget target);

        bool CanResolveKey(string assetPath);

        // Raised after a deployment is recorded.
        event Action Changed;
    }

    static class LastSuccessfulDeploymentStoreExtensions
    {
        // Typed entry points derive the path and kind from the asset itself, so a
        // caller cannot pair a path with the wrong kind.
        public static void Record(
            this ILastSuccessfulDeploymentStore store, CloudCodeModuleReference moduleReference,
            LastSuccessfulDeploymentInfo info, DateTime sourceLastWriteUtc = default)
        {
            store.Record(moduleReference.Path, LastSuccessfulDeploymentKind.ModuleReference, info, sourceLastWriteUtc);
        }

        public static LastSuccessfulDeploymentInfo GetRecord(
            this ILastSuccessfulDeploymentStore store, CloudCodeModuleReference moduleReference, DeploymentTarget target)
        {
            return store.GetRecord(moduleReference.Path, LastSuccessfulDeploymentKind.ModuleReference, target);
        }

        public static DateTime GetSourceLastWriteUtc(
            this ILastSuccessfulDeploymentStore store, CloudCodeModuleReference moduleReference, DeploymentTarget target)
        {
            return store.GetSourceLastWriteUtc(moduleReference.Path, LastSuccessfulDeploymentKind.ModuleReference, target);
        }

        public static bool CanResolveKey(this ILastSuccessfulDeploymentStore store, CloudCodeModuleReference moduleReference)
        {
            return store.CanResolveKey(moduleReference.Path);
        }

        public static LastSuccessfulDeploymentInfo FreshestRecord(
            this ILastSuccessfulDeploymentStore store, CloudCodeModuleReference moduleReference)
        {
            return store.FreshestRecord(moduleReference.Path, LastSuccessfulDeploymentKind.ModuleReference);
        }

#if UNITY_6000_5_OR_NEWER
        public static void Record(
            this ILastSuccessfulDeploymentStore store, CloudCodeModule module, LastSuccessfulDeploymentInfo info)
        {
            store.Record(module.Path, LastSuccessfulDeploymentKind.Module, info);
        }

        public static LastSuccessfulDeploymentInfo GetRecord(
            this ILastSuccessfulDeploymentStore store, CloudCodeModule module, DeploymentTarget target)
        {
            return store.GetRecord(module.Path, LastSuccessfulDeploymentKind.Module, target);
        }

        public static LastSuccessfulDeploymentInfo FreshestRecord(
            this ILastSuccessfulDeploymentStore store, CloudCodeModule module)
        {
            return store.FreshestRecord(module.Path, LastSuccessfulDeploymentKind.Module);
        }

        public static bool CanResolveKey(this ILastSuccessfulDeploymentStore store, CloudCodeModule module)
        {
            return store.CanResolveKey(module.Path);
        }

#endif
        public static LastSuccessfulDeploymentInfo FreshestRecord(
            this ILastSuccessfulDeploymentStore store, string assetPath, LastSuccessfulDeploymentKind kind)
        {
            return Freshest(
                store.GetRecord(assetPath, kind, DeploymentTarget.Remote),
                store.GetRecord(assetPath, kind, DeploymentTarget.Local));
        }

        internal static LastSuccessfulDeploymentInfo Freshest(
            LastSuccessfulDeploymentInfo first, LastSuccessfulDeploymentInfo second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;

            return first.DeployedAtUtc >= second.DeployedAtUtc ? first : second;
        }
    }

    /// <summary>
    /// The single source of truth for each module's last successful deployment,
    /// for the current editor session. Backed by <see cref="SessionState"/> rather than plain memory
    /// because entering Play mode is a domain reload. Entries are scoped by asset GUID, project and environment.
    /// </summary>
    class LastSuccessfulDeploymentStore : ILastSuccessfulDeploymentStore
    {
        [Serializable]
        class StoreData
        {
            public List<Entry> Entries = new List<Entry>();
        }

        [Serializable]
        class Entry
        {
            public string AssetGuid;
            public string ProjectId;
            public string EnvironmentId;
            public string Kind;
            public string Target;
            public DateTime DeployedAtUtc;

            // Only meaningful for ModuleReference entries, whose staleness is judged by file timestamps.
            public DateTime SourceLastWriteUtc;
            public string LastDeployedContentHash;
        }

        const string k_SessionStateKey = "Unity.Services.CloudCode.LastSuccessfulDeployments";

        readonly Func<string> m_LoadState;
        readonly Action<string> m_SaveState;
        readonly Func<string> m_ProjectId;
        readonly Func<Guid?> m_EnvironmentId;
        readonly Func<string, string> m_AssetGuidResolver;
        readonly ILogger m_Logger;

        StoreData m_Data;

        public LastSuccessfulDeploymentStore(IEnvironmentsApi environmentsApi, IProjectID projectId, ILogger logger)
            : this(
                () => SessionState.GetString(k_SessionStateKey, string.Empty),
                state => SessionState.SetString(k_SessionStateKey, state),
                () => projectId.ProjectId,
                () => environmentsApi.ActiveEnvironmentId,
                AssetDatabase.AssetPathToGUID,
                logger)
        {
        }

        internal LastSuccessfulDeploymentStore(
            Func<string> loadState,
            Action<string> saveState,
            Func<string> projectId,
            Func<Guid?> environmentId,
            Func<string, string> assetGuidResolver,
            ILogger logger)
        {
            m_LoadState = loadState;
            m_SaveState = saveState;
            m_ProjectId = projectId;
            m_EnvironmentId = environmentId;
            m_AssetGuidResolver = assetGuidResolver;
            m_Logger = logger;
        }

        public event Action Changed;

        public void Record(string assetPath, LastSuccessfulDeploymentKind kind, LastSuccessfulDeploymentInfo info, DateTime sourceLastWriteUtc = default)
        {
            if (info == null || !TryResolveEntryKey(assetPath, out var assetGuid, out var projectId, out var environmentId))
                return;

            var data = GetData();
            var entry = FindIn(data, assetGuid, projectId, environmentId, kind, info.Target);
            if (entry == null)
            {
                entry = new Entry
                {
                    AssetGuid = assetGuid,
                    ProjectId = projectId,
                    EnvironmentId = environmentId,
                    Kind = kind.ToString(),
                    Target = info.Target.ToString()
                };
                data.Entries.Add(entry);
            }

            entry.DeployedAtUtc = info.DeployedAtUtc;
            entry.SourceLastWriteUtc = sourceLastWriteUtc;
            entry.LastDeployedContentHash = info.LastDeployedContentHash;
            Save();
            Changed?.Invoke();
        }

        public DateTime GetSourceLastWriteUtc(string assetPath, LastSuccessfulDeploymentKind kind, DeploymentTarget target)
        {
            if (!TryResolveEntryKey(assetPath, out var assetGuid, out var projectId, out var environmentId))
                return default;

            return Find(assetGuid, projectId, environmentId, kind, target)?.SourceLastWriteUtc ?? default;
        }

        public LastSuccessfulDeploymentInfo GetRecord(string assetPath, LastSuccessfulDeploymentKind kind, DeploymentTarget target)
        {
            if (!TryResolveEntryKey(assetPath, out var assetGuid, out var projectId, out var environmentId))
                return null;

            var entry = Find(assetGuid, projectId, environmentId, kind, target);
            if (entry == null)
                return null;

            return new LastSuccessfulDeploymentInfo
            {
                Target = target,
                DeployedAtUtc = entry.DeployedAtUtc,
                LastDeployedContentHash = entry.LastDeployedContentHash
            };
        }

        public bool CanResolveKey(string assetPath)
        {
            return TryResolveEntryKey(assetPath, out _, out _, out _);
        }

        bool TryResolveEntryKey(string assetPath, out string assetGuid, out string projectId, out string environmentId)
        {
            assetGuid = string.IsNullOrEmpty(assetPath) ? null : m_AssetGuidResolver(assetPath);
            projectId = m_ProjectId();
            environmentId = m_EnvironmentId()?.ToString();

            return !string.IsNullOrEmpty(assetGuid)
                && !string.IsNullOrEmpty(projectId)
                && !string.IsNullOrEmpty(environmentId);
        }

        Entry Find(string assetGuid, string projectId, string environmentId, LastSuccessfulDeploymentKind kind, DeploymentTarget target)
        {
            return FindIn(GetData(), assetGuid, projectId, environmentId, kind, target);
        }

        static Entry FindIn(StoreData data, string assetGuid, string projectId, string environmentId, LastSuccessfulDeploymentKind kind, DeploymentTarget target)
        {
            var kindName = kind.ToString();
            var targetName = target.ToString();

            return data.Entries.FirstOrDefault(entry =>
                entry.AssetGuid == assetGuid
                && entry.ProjectId == projectId
                && entry.EnvironmentId == environmentId
                && entry.Kind == kindName
                && entry.Target == targetName);
        }

        StoreData GetData()
        {
            if (m_Data != null)
                return m_Data;

            m_Data = Load();
            return m_Data;
        }

        StoreData Load()
        {
            var text = m_LoadState();
            if (string.IsNullOrEmpty(text))
                return new StoreData();

            try
            {
                var parsed = JsonConvert.DeserializeObject<StoreData>(text);
                if (parsed == null)
                    return new StoreData();

                parsed.Entries = parsed.Entries?
                    .Where(entry => entry != null)
                    .ToList() ?? new List<Entry>();

                foreach (var entry in parsed.Entries)
                {
                    entry.DeployedAtUtc = NormalizeUtc(entry.DeployedAtUtc);
                    entry.SourceLastWriteUtc = NormalizeUtc(entry.SourceLastWriteUtc);
                }

                return parsed;
            }
            catch (Exception e)
            {
                m_Logger.LogVerbose($"Could not parse the Cloud Code last successful deployment records: {e.Message}");
                return new StoreData();
            }
        }

        static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        void Save()
        {
            m_SaveState(JsonConvert.SerializeObject(m_Data));
        }
    }
}
#endif
