#if UNITY_6000_5_OR_NEWER
using System;
using System.IO;
using Unity.Services.CloudCode.Authoring.Editor.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.UI;
using Unity.Services.CloudCode.Editor.Shared.DependencyInversion;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(CloudCodeModule))]
    class CloudCodeModuleInspector : UnityEditor.Editor
    {
        [SerializeField]
        VisualTreeAsset m_VisualTreeAsset;

        VisualElement m_LastDeploymentRoot;

        static readonly string k_UxmlPath =
            Path.Combine(CloudCodePackage.EditorPath, UxmlConstants.UxmlAssetPath);

        public override VisualElement CreateInspectorGUI()
        {
            var uxmlAsset = m_VisualTreeAsset;
            if (uxmlAsset == null)
            {
                uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UxmlPath);
                if (uxmlAsset == null)
                {
                    return DisplayMissingUxml();
                }
            }

            var root = new VisualElement();
            uxmlAsset.CloneTree(root);

            root.Bind(serializedObject);

            DeploymentFooterBinder.Bind(root, target, DeploymentDashboard.Module);

            SetupLastDeployment(root);

            return root;
        }

        void SetupLastDeployment(VisualElement root)
        {
            var module = (CloudCodeModule)target;
            m_LastDeploymentRoot = root;

            RefreshLastDeployment(root, module);

            // Docking or re-parenting the inspector detaches and re-attaches
            // the same tree without rebuilding it, so subscribe on attach and unsubscribe on detach.
            root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                var store = TryGetDeploymentStore();
                if (store != null)
                {
                    store.Changed -= OnDeploymentStoreChanged;
                    store.Changed += OnDeploymentStoreChanged;
                }

                RefreshLastDeployment(root, module);
            });
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                var store = TryGetDeploymentStore();
                if (store != null)
                    store.Changed -= OnDeploymentStoreChanged;
            });
        }

        void OnDeploymentStoreChanged()
        {
            RefreshLastDeployment(m_LastDeploymentRoot, (CloudCodeModule)target);
        }

        static void RefreshLastDeployment(VisualElement root, CloudCodeModule module)
        {
            var record = TryGetDeploymentStore()?.FreshestRecord(module);
            LastDeploymentSection.Refresh(root, record);
        }

        static ILastSuccessfulDeploymentStore TryGetDeploymentStore()
        {
            try
            {
                return CloudCodeAuthoringServices.Instance.GetService<ILastSuccessfulDeploymentStore>();
            }
            catch (Exception e) when (e is DependencyNotFoundException or NullReferenceException)
            {
                return null;
            }
        }

        static VisualElement DisplayMissingUxml()
        {
            var uxmlAssetName = Path.GetFileName(k_UxmlPath);
            var errorMessage = $"Failed to load \"{uxmlAssetName}\". Please ensure the asset exists at: \"{k_UxmlPath}\".";
            Debug.LogError(errorMessage);
            var errorRoot = new VisualElement();
            errorRoot.Add(new HelpBox(errorMessage, HelpBoxMessageType.Error));
            return errorRoot;
        }

        static class UxmlConstants
        {
            public const string UxmlAssetPath = "Authoring/Modules/UI/Assets/CloudCodeModuleUi.uxml";
        }
    }
}
#endif
