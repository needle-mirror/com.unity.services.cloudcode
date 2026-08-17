#if UNITY_6000_5_OR_NEWER
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using Unity.Services.CloudCode.Editor.Shared.Assets;
using Unity.Services.CloudCode.Editor.Shared.DependencyInversion;
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    [HelpURL("https://docs.unity3d.com/Packages/com.unity.services.cloudcode@2.8/manual/Authoring/cloud_code_modules.html"),
     Icon("Packages/com.unity.services.cloudcode/Editor/Authoring/Modules/UI/Assets/icon.png")]
    class CloudCodeModule : ScriptableObject, ICloudCodeModuleItem, IPath, ITrackableItem
    {
        static readonly JsonSerializerSettings k_JsonSerializerSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ContractResolver = new CamelCasePropertyNamesContractResolver()
        };

        [SerializeField]
        AssemblyDefinitionAsset m_CloudAssemblyDefinition;
        public AssemblyDefinitionAsset CloudAssemblyDefinition
        {
            get => m_CloudAssemblyDefinition;
            set => m_CloudAssemblyDefinition = value;
        }

        [SerializeField]
        [HideInInspector]
        AssemblyDefinitionAsset m_ClientAssemblyDefinition;
        internal AssemblyDefinitionAsset ClientAssemblyDefinition
        {
            get => m_ClientAssemblyDefinition;
            set => m_ClientAssemblyDefinition = value;
        }

        public CloudCodeModule()
        {
            Progress = 0;
            Status = DeploymentStatus.Empty;
            m_DeployedServerStatus = new SerializableObservableCollection<AssetState>();
            m_DeploymentStatusLog = new List<(DateTime, DeploymentStatus)>();
        }

        public string AssemblyPath => GetAssemblyPath();

        string m_CcmPath;
        public string CcmPath
        {
            get => m_CcmPath;
            set => SetField(ref m_CcmPath, value);
        }

        string GetAssemblyPath()
        {
            if (m_CloudAssemblyDefinition == null)
                return string.Empty;

            var asmdef = AsmdefJsonData.ParseAssemblyDefinitionAsset(m_CloudAssemblyDefinition);
            return System.IO.Path.GetFullPath(System.IO.Path.Combine("Library", "ScriptAssemblies", asmdef.name + ".dll"));
        }

        /// <summary>Directory containing the cloud assembly definition. Null when the asmdef is unresolved.</summary>
        internal string GetCloudAssemblyDirectory() => GetAssemblyDirectory(m_CloudAssemblyDefinition);

        /// <summary>Directory containing the client assembly definition. Null when the asmdef is unresolved.</summary>
        internal string GetClientAssemblyDirectory() => GetAssemblyDirectory(m_ClientAssemblyDefinition);

        static string GetAssemblyDirectory(AssemblyDefinitionAsset assemblyDefinition)
        {
            if (assemblyDefinition == null)
                return null;

            var asmdefPath = AssetDatabase.GetAssetPath(assemblyDefinition);
            return string.IsNullOrEmpty(asmdefPath)
                ? null
                : System.IO.Path.GetDirectoryName(asmdefPath)?.Replace('\\', '/');
        }

        /// <summary>
        /// ITrackableItem: the Deployment window calls this when it (re)tracks the asset. Reconciles
        /// status from deployed content instead of falling back to the generic file-timestamp heuristic.
        /// </summary>
        public void TrackOrUpdate()
        {
            CloudCodeModuleModifiedTracker tracker;
            try
            {
                // Instance is lazily created (never null), but its service provider may not be built yet
                // during early lifecycle (the deployment window can track items before authoring services
                // initialize on load), and the dependency is absent when required defines are off.
                tracker = CloudCodeAuthoringServices.Instance.GetService<CloudCodeModuleModifiedTracker>();
            }
            catch (Exception e) when (e is DependencyNotFoundException or NullReferenceException)
            {
                return;
            }

            tracker.ReconcileFireAndForget(this);
        }

        #region IModuleItem

        float m_DeploymentProgress;
        private string m_DeploymentItemName;
        DeploymentStatus m_DeploymentStatus;
        SerializableObservableCollection<AssetState> m_DeployedServerStatus;
        List<(DateTime, DeploymentStatus)> m_DeploymentStatusLog;

        // Required by Deployment window to notify property changes
        public event PropertyChangedEventHandler PropertyChanged;

        // Type of the deployment item as displayed in the Deployment Window
        public string Type { get; } = "Cloud Code Module";

        // Syncs both the name of the asset, and Deployment Name as required by IDeploymentItem.
        public string Name
        {
            get
            {
                if (string.IsNullOrEmpty(m_DeploymentItemName))
                    Name = name;

                return m_DeploymentItemName;
            }
            private set
            {
                name = value;
                SetField(ref m_DeploymentItemName, value);
            }
        }

        // Tracks progression of this item if deployed.
        public float Progress
        {
            get => m_DeploymentProgress;
            set => SetField(ref m_DeploymentProgress, value);
        }

        // Tracks the current status of this deployment item.
        public DeploymentStatus Status
        {
            get => m_DeploymentStatus;
            set => SetField(ref m_DeploymentStatus, value);
        }

        /// <summary>
        /// Tracks a log history of all deployment status events. Lazily initialized because a domain
        /// reload restores the asset without running the constructor, leaving the backing field null.
        /// </summary>
        public List<(DateTime, DeploymentStatus)> StatusLog =>
            m_DeploymentStatusLog ??= new List<(DateTime, DeploymentStatus)>();

        /// <summary>Tracks the current local server status, if available. Lazily initialized for the same reason.</summary>
        public ObservableCollection<AssetState> States =>
            m_DeployedServerStatus ??= new SerializableObservableCollection<AssetState>();

        [SerializeReference]
        LastSuccessfulDeploymentInfo m_LastSuccessfulDeployment;

        // Last successful deployment for this editor session.
        public LastSuccessfulDeploymentInfo LastSuccessfulDeployment
        {
            get => m_LastSuccessfulDeployment;
            set => SetField(ref m_LastSuccessfulDeployment, value);
        }

        [SerializeField]
        string m_CurrentContentHash;

        /// <summary>
        /// Hash of the module's current source as last computed by the modified tracker. Serialized so a
        /// domain reload can restore status by comparing it to the deployed baseline without re-hashing.
        /// </summary>
        internal string CurrentContentHash
        {
            get => m_CurrentContentHash;
            set => m_CurrentContentHash = value;
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Path { get; set; }
        #endregion

        #region ObservableAssets

        string IPath.Path
        {
            get => Path;
            set
            {
                Path = value;

                // Name changes are detected on path changes.
                // Ensure we trigger property name change updates for the Deployment window.
                Name = System.IO.Path.GetFileName(value);
            }
        }

        #endregion

        public static string ToJson(AssemblyDefinitionAsset cloudAssemblyDefinition, AssemblyDefinitionAsset clientAssemblyDefinition)
        {
            return JsonConvert.SerializeObject(
                new
                {
                    cloudAssemblyDefinitionGuid = GetAssetGuid(cloudAssemblyDefinition),
                    clientAssemblyDefinitionGuid = GetAssetGuid(clientAssemblyDefinition),
                },
                k_JsonSerializerSettings);
        }

        public static bool FromJson(string json, out AssemblyDefinitionAsset cloudAssemblyDefinition,
            out AssemblyDefinitionAsset clientAssemblyDefinition)
        {
            cloudAssemblyDefinition = null;
            clientAssemblyDefinition = null;

            JsonShape parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<JsonShape>(json, k_JsonSerializerSettings);
            }
            catch (JsonException e)
            {
                Debug.LogError($"Failed to parse Cloud Code Module: {e.Message}");
                return false;
            }

            if (parsed == null)
                return false;

            return TryLoadAsmdefByGuid(parsed.CloudAssemblyDefinitionGuid, out cloudAssemblyDefinition)
                && TryLoadAsmdefByGuid(parsed.ClientAssemblyDefinitionGuid, out clientAssemblyDefinition);
        }

        static string GetAssetGuid(AssemblyDefinitionAsset asset)
        {
            var path = asset == null ? null : AssetDatabase.GetAssetPath(asset);
            var guid = string.IsNullOrEmpty(path) ? string.Empty : AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                throw new InvalidOperationException("Could not resolve a GUID for the module's assembly definition.");
            return guid;
        }

        static bool TryLoadAsmdefByGuid(string guid, out AssemblyDefinitionAsset asset)
        {
            asset = null;
            if (!GUID.TryParse(guid, out var parsedGuid))
            {
                Debug.LogError($"Invalid or missing assembly definition GUID '{guid}' in Cloud Code Module.");
                return false;
            }

            asset = AssetDatabase.LoadAssetByGUID<AssemblyDefinitionAsset>(parsedGuid);
            if (asset == null)
            {
                Debug.LogError($"Could not resolve assembly definition for GUID '{guid}' in Cloud Code Module.");
                return false;
            }

            return true;
        }

        class JsonShape
        {
            [JsonRequired]
            public string CloudAssemblyDefinitionGuid { get; set; }

            [JsonRequired]
            public string ClientAssemblyDefinitionGuid { get; set; }
        }

        #region Serialization Wrappers

        // Required as ObservableCollection fails Unity serialization of its items across Domain Reloads
        [Serializable]
        class SerializableObservableCollection<T> : ObservableCollection<T>, ISerializationCallbackReceiver
        {
            [SerializeField]
            List<T> m_PersistedList;

            internal SerializableObservableCollection()
            {
                m_PersistedList = new List<T>();
            }

            public void OnBeforeSerialize()
            {
                m_PersistedList.Clear();
                m_PersistedList.AddRange(Items);
            }

            public void OnAfterDeserialize()
            {
                Items.Clear();
                foreach (var state in m_PersistedList)
                {
                    Items.Add(state);
                }
            }
        }

        #endregion
    }
}
#endif
