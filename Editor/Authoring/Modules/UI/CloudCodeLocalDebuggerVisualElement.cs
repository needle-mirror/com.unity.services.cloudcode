#if UNITY_SERVICES_CLOUDCODE_EXPERIMENTAL
using System;
using System.IO;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using UnityEditor;
// Required for older unity versions.
// ReSharper disable once RedundantUsingDirective
using UnityEditor.UIElements;
// Required for older unity versions.
// ReSharper disable once RedundantUsingDirective
using UnityEngine;
using UnityEngine.UIElements;
using LocalCloudCodeServerStatus =
        Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    class CloudCodeLocalDebuggerVisualElement : VisualElement
    {
        static readonly string k_Uxml =
            Path.Combine(
                CloudCodePackage.EditorPath,
                "Authoring", "Modules", "UI",
                "Assets", "CloudCodeLocalDebuggerProjectSettings.uxml");

        static readonly string k_Uss =
            Path.Combine(CloudCodePackage.EditorPath,
                "Authoring", "Modules", "UI",
                "Assets", "CloudCodeLocalDebuggerProjectSettings.uss");

        static readonly string k_FoldoutText = L10n.Tr("Local Debugging");
        static readonly string k_FoldoutTooltip = L10n.Tr("A local server with debug capabilities to allow for stepping through debugging of your code modules.");
        static readonly string k_HelpBoxText = L10n.Tr("To use the local Cloud Code server, open the More (\u22ee) menu in the top toolbar and enable Cloud Code from the Service submenu.");
        static readonly string k_HelpBoxServerRunningText = L10n.Tr("Port and Secrets File cannot be modified while the local Cloud Code server is running. Stop the server to edit these settings.");

        HelpBox DebuggerStartHelpBox { get; }
        Foldout Foldout { get; }

        // TODO: MTT-13967 Temporary Code. Revisit once Design has finished Experimental pass.
        public CloudCodeLocalDebuggerVisualElement()
        {
            var containerAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            if (containerAsset != null)
            {
                var containerUI = containerAsset.CloneTree().contentContainer;

                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_Uss);
                if (styleSheet != null)
                {
                    containerUI.styleSheets.Add(styleSheet);
                }
                else
                {
                    throw new Exception("Asset not found: " + k_Uss);
                }

                var sp = CloudCodeLocalServerSettings.GetOrCreate();
                var inspectorElement = new InspectorElement(sp);

                Foldout = containerUI.Q<Foldout>("debugger-foldout");
                Foldout.tooltip = k_FoldoutTooltip;
                Foldout.text = k_FoldoutText;
                Foldout.Insert(0, inspectorElement);

                DebuggerStartHelpBox = containerUI.Q<HelpBox>("debugger-start-help-box");
                DebuggerStartHelpBox.text = k_HelpBoxText;

                Add(containerUI);

                var localServer = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
                if (localServer != null)
                {
                    localServer.OnServerStatusChanged += OnServerStatusChanged;
                    UpdateHelpBox(localServer.GetCurrentServerStatus());
                }
            }
            else
            {
                throw new Exception("Asset not found: " + k_Uxml);
            }
        }

        void OnServerStatusChanged(object sender, LocalCloudCodeServerStatus status)
        {
            UpdateHelpBox(status);
        }

        void UpdateHelpBox(LocalCloudCodeServerStatus status)
        {
            if (DebuggerStartHelpBox == null)
                return;

            switch (status)
            {
                case LocalCloudCodeServerStatus.Idle:
                    DebuggerStartHelpBox.text = k_HelpBoxText;
                    DebuggerStartHelpBox.messageType = HelpBoxMessageType.Warning;
                    break;
                default:
                    DebuggerStartHelpBox.text = k_HelpBoxServerRunningText;
                    DebuggerStartHelpBox.messageType = HelpBoxMessageType.Info;
                    break;
            }
        }
    }
}
#endif
