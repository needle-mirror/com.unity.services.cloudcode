#if UNITY_6000_5_OR_NEWER
using System;
using System.ComponentModel;
using System.IO;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.UI;
using UnityEditor;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;
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

            module.PropertyChanged += OnModulePropertyChanged;
            root.RegisterCallback<DetachFromPanelEvent>(_ => module.PropertyChanged -= OnModulePropertyChanged);
        }

        void OnModulePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CloudCodeModule.LastSuccessfulDeployment))
                return;

            if (sender is CloudCodeModule changedModule && changedModule == target)
                RefreshLastDeployment(m_LastDeploymentRoot, changedModule);
            else
                Debug.LogError($"Unexpected {nameof(CloudCodeModule.LastSuccessfulDeployment)} change source in {nameof(CloudCodeModuleInspector)}.");
        }

        static void RefreshLastDeployment(VisualElement root, CloudCodeModule module)
        {
            var targetLabel = root.Q<Label>("last-deploy-target");
            var timeLabel = root.Q<Label>("last-deploy-time");
            if (targetLabel == null || timeLabel == null)
                return;

            var deployment = module.LastSuccessfulDeployment;
            if (deployment != null)
            {
                targetLabel.text = TargetDisplayName(deployment.Target);
                var deployedLocalTime = new DateTime(deployment.TimeTicks, DateTimeKind.Utc).ToLocalTime();
                timeLabel.text = $"Editor deployed on [{deployedLocalTime:HH:mm:ss}]";
            }
            else
            {
                targetLabel.text = "No status";
                timeLabel.text = "No status";
            }
        }

        static string TargetDisplayName(DeploymentTarget target)
        {
            return target == DeploymentTarget.Remote
                ? "Remote Cloud Code Server"
                : "Local Server";
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
