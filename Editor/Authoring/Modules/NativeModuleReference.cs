using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using UnityEditorInternal;
using UnityEngine;
using Unity.Services.CloudCode.Editor.Shared.Assets;
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    // TODO Add Icon so that it is Cloud Code Specific
    [CreateAssetMenu(menuName = "Create NativeModuleReference", fileName = "NativeModuleReference", order = 0)]
    class NativeModuleReference : ScriptableObject, IModuleItem, IPath
    {
        [SerializeField]
        AssemblyDefinitionAsset m_AssemblyDefinition;
        public AssemblyDefinitionAsset AssemblyDefinition
        {
            get => m_AssemblyDefinition;
            set => m_AssemblyDefinition = value;
        }

        public NativeModuleReference()
        {
            Progress = 0;
            Status = DeploymentStatus.Empty;
            m_DeployedServerStatus = new SerializableObservableCollection<AssetState>();
            m_DeploymentStatusLog = new List<(DateTime, DeploymentStatus)>();
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
        public string Type { get; } = "Native Module";

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

        // Tracks a log history of all deployment status events
        public List<(DateTime, DeploymentStatus)> StatusLog => m_DeploymentStatusLog;

        // Tracks the current local server status, if available.
        public ObservableCollection<AssetState> States => m_DeployedServerStatus;

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
                Name = name;
            }
        }

#endregion

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
