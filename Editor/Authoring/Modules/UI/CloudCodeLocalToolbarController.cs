#if UNITY_6000_3_OR_NEWER

using System;
using System.IO;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using LocalCloudCodeServerStatus =
    Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    internal class CloudCodeLocalToolbarController
    {
        // Constants to uxml paths and assets
        static readonly string k_CloudCodeLocalToolbarIconsPath =
            Path.Combine(CloudCodePackage.EditorPath, "Authoring", "Modules", "UI", "Assets", "Icons");
        static readonly string k_ToolbarIdleIcon = "ToolbarIdle.png";
        static readonly string k_ToolbarStartedIcon = "ToolbarStarted.png";
        static readonly string k_ToolbarLoadingIcon = "ToolbarLoading.png";
        static readonly string k_ToolbarErrorIcon = "ToolbarError.png";

        // Constant values for visual text elements
        const string k_ToolbarTooltipTextBase = "Local Cloud Code Server options.\n";
        static readonly string k_ToolbarTooltipTextIdle =
            L10n.Tr($"{k_ToolbarTooltipTextBase}Current state: idle");
        static readonly string k_ToolbarTooltipTextStarting =
            L10n.Tr($"{k_ToolbarTooltipTextBase}Current state: starting server");
        static readonly string k_ToolbarTooltipTextStarted =
            L10n.Tr($"{k_ToolbarTooltipTextBase}Current state: server running");
        static readonly string k_ToolbarTooltipTextStopping =
            L10n.Tr($"{k_ToolbarTooltipTextBase}Current state: server stopping");
        static readonly string k_ToolbarTooltipTextError =
            L10n.Tr($"{k_ToolbarTooltipTextBase}Current state: error occured. Please view Console for more details");

        // Visual elements of the Local Cloud Code Toolbar control
        MainToolbarContent m_MainToolbarContent;
        CloudCodeLocalToolbarPopup m_PopupWindow;
        ICloudCodeLocalServer m_LocalServer;

        internal event Action OnToolbarInvalidated;

        internal CloudCodeLocalToolbarController()
        {
            m_LocalServer = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
            m_LocalServer.OnServerStatusChanged += OnServerStatusChanged;

            m_PopupWindow = new CloudCodeLocalToolbarPopup(this);
        }

        void OnServerStatusChanged(object server, LocalCloudCodeServerStatus status)
        {
            OnToolbarInvalidated?.Invoke();
        }

        internal ICloudCodeLocalServer GetLocalServer()
        {
            return m_LocalServer;
        }

        internal MainToolbarContent GetMainToolbarContent()
        {
            var icon = GetToolbarIconForStatus(m_LocalServer.GetCurrentServerStatus(),
                                               m_LocalServer.GetLastServerFailure()) as Texture2D;
            m_MainToolbarContent = new MainToolbarContent(icon);

            UpdateToolbarIconTooltip();
            return m_MainToolbarContent;
        }

        Texture2D GetToolbarIconForStatus(LocalCloudCodeServerStatus status, string lastKnownServerFailure)
        {
            // Always prioritize showing failures first
            if (status == LocalCloudCodeServerStatus.Idle && lastKnownServerFailure != null)
            {
                var errorIcon = EditorGUIUtility.isProSkin ? $"d_{k_ToolbarErrorIcon}" : k_ToolbarErrorIcon;
                var errorPath = Path.Combine(k_CloudCodeLocalToolbarIconsPath, errorIcon);
                return AssetDatabase.LoadAssetAtPath<Texture2D>(errorPath);
            }

            // Else show the Local CC icon based on server status
            string iconPath;
            switch (status)
            {
                case LocalCloudCodeServerStatus.Idle:
                    iconPath = k_ToolbarIdleIcon;
                    break;
                case LocalCloudCodeServerStatus.Started:
                    iconPath = k_ToolbarStartedIcon;
                    break;
                case LocalCloudCodeServerStatus.Starting:
                case LocalCloudCodeServerStatus.Preparing:
                case LocalCloudCodeServerStatus.Stopping:
                    iconPath = k_ToolbarLoadingIcon;
                    break;
                default:
                    Debug.LogError("Unknown Local CloudCodeServerStatus found in Toolbar.");
                    return null;
            }

            iconPath = EditorGUIUtility.isProSkin ? $"d_{iconPath}" : iconPath;
            iconPath = Path.Combine(k_CloudCodeLocalToolbarIconsPath, iconPath);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        }

        void UpdateToolbarIconTooltip()
        {
            // Always prioritize showing failures first
            var status = m_LocalServer.GetCurrentServerStatus();
            var failure = m_LocalServer.GetLastServerFailure();
            if (status == LocalCloudCodeServerStatus.Idle && failure != null)
            {
                m_MainToolbarContent.tooltip = k_ToolbarTooltipTextError;
                return;
            }

            // Else show the toolbar tooltip based on server status
            switch (status)
            {
                case LocalCloudCodeServerStatus.Idle:
                    m_MainToolbarContent.tooltip = k_ToolbarTooltipTextIdle;
                    break;
                case LocalCloudCodeServerStatus.Started:
                    m_MainToolbarContent.tooltip = k_ToolbarTooltipTextStarted;
                    break;
                case LocalCloudCodeServerStatus.Preparing:
                case LocalCloudCodeServerStatus.Starting:
                    m_MainToolbarContent.tooltip = k_ToolbarTooltipTextStarting;
                    break;
                case LocalCloudCodeServerStatus.Stopping:
                    m_MainToolbarContent.tooltip = k_ToolbarTooltipTextStopping;
                    break;
                default:
                    Debug.LogError("Unknown LocalCloudCodeServerStatus found in Toolbar.");
                    break;
            }
        }

        internal void OnOpenToolbarPopup(Rect rect)
        {
            Vector2 popupPosition = new Vector2(rect.x, rect.y + rect.height);
            PopupWindow.Show(new Rect(popupPosition, Vector2.zero), m_PopupWindow);
        }
    }
}

#endif
