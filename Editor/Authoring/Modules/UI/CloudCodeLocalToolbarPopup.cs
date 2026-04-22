#if UNITY_SERVICES_CLOUDCODE_EXPERIMENTAL
#if UNITY_6000_3_OR_NEWER

using System.Threading.Tasks;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using LocalCloudCodeServerStatus =
        Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    internal class CloudCodeLocalToolbarPopup : PopupWindowContent
    {
        // Constants to uxml and style asset paths
        static readonly string k_CloudCodeLocalAssetPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "Authoring", "Modules", "UI", "Assets");
        static readonly string k_UxmlPath =
            PathUtils.Join(k_CloudCodeLocalAssetPath, "CloudCodeLocalServerPopup.uxml");
        static readonly string k_StylesheetDarkPath =
            PathUtils.Join(k_CloudCodeLocalAssetPath, "CloudCodeLocalServerIconsDark.uss");
        static readonly string k_StylesheetLightPath =
            PathUtils.Join(k_CloudCodeLocalAssetPath, "CloudCodeLocalServerIconsLight.uss");

        // Constants identifying visual assets used by Local Cloud Code Popup window
        const string k_ServerIconStyleIdle = "server-status-idle__icon";
        const string k_ServerIconStyleStarting = "server-status-starting__icon";
        const string k_ServerIconStyleStarted = "server-status-started__icon";
        const string k_ServerIconStyleError = "server-status-error__icon";
        const string k_PidCopyButtonStyle = "popup-row-server-pid__button-icon";
        const string k_ServerPopupWindowName = "popup-window-container";
        const string k_ServerStatusTitleTxtName = "server-status-title-txt";
        const string k_ServerEditOnlyHelpBoxName = "server-edit-only-info-box";
        const string k_ServerStatusTxtName = "server-status-txt";
        const string K_ServerStatusIconName = "server-status-icon";
        const string k_ServerActionBtnName = "server-action-btn";
        const string k_ServerSettingsBtnName = "server-settings-btn";
        const string k_ServerPidTxtName = "server-pid-txt";
        const string k_ServerPidCopyButtonName = "server-pid-copy-btn";
        const string k_ServerResetBtn = "server-reset-btn";
        const string k_ServerResetBtnText = "server-reset-btn-txt";
        const string k_DeploymentWindowBtnName = "deployment-window-btn";

        // Constant values for visual text elements
        static readonly string k_ServerActionButtonTextIdle = L10n.Tr("Start Local Server");
        static readonly string k_ServerActionButtonTextStarting = L10n.Tr("Cancel");
        static readonly string k_ServerActionButtonTextStarted = L10n.Tr("Stop Local Server");
        static readonly string k_ServerActionButtonTextStopping = L10n.Tr("Stopping Local Server...");
        static readonly string k_ServerStatusTextIdle = L10n.Tr("Idle");
        static readonly string k_ServerStatusTextStarting = L10n.Tr("Starting");
        static readonly string k_ServerStatusTextStopping = L10n.Tr("Stopping");
        static readonly string k_ServerStatusTextError = L10n.Tr("Error");
        static readonly string k_ServerSettingsTitleText = L10n.Tr("Project Settings");
        static readonly string k_DeploymentWindowTitleText = L10n.Tr("Deployment Window");
        static readonly string k_ServerStatusTitleText = L10n.Tr("Local Cloud Code Server");
        static readonly string k_ServerEditOnlyHelpBoxText =
            L10n.Tr("Local Cloud Code server can only be started or stopped when not in play mode.");
        static readonly string k_ServerResetButtonTitle = L10n.Tr("Local Server State");
        static readonly string k_ServerResetButtonTextAction = L10n.Tr("Clear");
        static readonly string k_ServerResetButtonTextPending = L10n.Tr("Clearing");
        static readonly string k_ServerResetButtonToolTip = L10n.Tr("Clear data will require a local server restart. " +
            "This can only be performed while the server is " +
            "not running.");

        // Visual elements of the Local Cloud Code Popup window
        VisualElement m_ServerStatusIcon;
        TextElement m_ServerStatusText;
        Button m_ServerActionButton;
        VisualElement m_Root;
        Label m_ServerPidText;
        VisualElement m_ServerPidCopyButton;
        HelpBox m_ServerEditOnlyHelpbox;
        TextElement m_ServerResetButtonTitle;
        Button m_ServerResetButton;

        CloudCodeLocalToolbarController m_ToolbarController;
        const string k_CloudCodeProjectSettingsPath = "Project/Services/Cloud Code";

        internal CloudCodeLocalToolbarPopup(CloudCodeLocalToolbarController toolbarController)
        {
            m_ToolbarController = toolbarController;
            m_ToolbarController.OnToolbarInvalidated += RefreshPopupUI;
        }

        public override VisualElement CreateGUI()
        {
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UxmlPath);
            m_Root = visualTreeAsset.CloneTree();

            // Apply light or dark theme css
            var stylesheetPath = EditorGUIUtility.isProSkin ? k_StylesheetDarkPath : k_StylesheetLightPath;
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylesheetPath);
            m_Root.styleSheets.Add(stylesheet);

            // Bind and initialize views to current sever state
            m_ServerStatusText = m_Root.Q<TextElement>(k_ServerStatusTxtName);
            m_ServerStatusIcon = m_Root.Q<VisualElement>(K_ServerStatusIconName);

            m_ServerPidText = m_Root.Q<Label>(k_ServerPidTxtName);
            m_ServerPidText.selection.isSelectable = true;
            m_ServerPidCopyButton = m_Root.Q<VisualElement>(k_ServerPidCopyButtonName);
            m_ServerPidCopyButton.AddToClassList("icon");
            m_ServerPidCopyButton.AddToClassList(k_PidCopyButtonStyle);
            m_ServerPidCopyButton.RegisterCallback<ClickEvent>(evt =>
            {
                string pid = m_ToolbarController.GetLocalServer().GetServerPid().ToString();
                GUIUtility.systemCopyBuffer = pid;
            });

            m_ServerActionButton = m_Root.Q<Button>(k_ServerActionBtnName);
            m_ServerActionButton.clicked += OnServerActionButtonClicked;

            var deploymentWindowBtn = m_Root.Q<Button>(k_DeploymentWindowBtnName);
            deploymentWindowBtn.clicked += () => Deployments.Instance.DeploymentWindow.OpenWindow();
            deploymentWindowBtn.text = k_DeploymentWindowTitleText;

            var serverSettings = m_Root.Q<Button>(k_ServerSettingsBtnName);
            serverSettings.clicked += OnServerSettingsClicked;
            serverSettings.text = k_ServerSettingsTitleText;

            var serverStatusTitleText = m_Root.Q<TextElement>(k_ServerStatusTitleTxtName);
            serverStatusTitleText.text = k_ServerStatusTitleText;

            m_ServerEditOnlyHelpbox = m_Root.Q<HelpBox>(k_ServerEditOnlyHelpBoxName);
            m_ServerEditOnlyHelpbox.text = k_ServerEditOnlyHelpBoxText;

            // Bind reset server state controls
            m_ServerResetButtonTitle = m_Root.Q<TextElement>(k_ServerResetBtnText);
            m_ServerResetButtonTitle.text = k_ServerResetButtonTitle;
            m_ServerResetButton = m_Root.Q<Button>(k_ServerResetBtn);
            m_ServerResetButton.text = k_ServerResetButtonTextAction;
            m_ServerResetButton.clicked += async() =>
            {
                m_ServerResetButton.enabledSelf = false;
                m_ServerResetButton.text = k_ServerResetButtonTextPending;
                m_ToolbarController.GetLocalServer().ClearServerState();

                // Add custom delay for visual feedback since clearing can be quick.
                await Task.Delay(500);

                m_ServerResetButton.enabledSelf = true;
                m_ServerResetButton.text = k_ServerResetButtonTextAction;
            };
            m_ServerResetButton.tooltip = k_ServerResetButtonToolTip;
            m_ServerResetButtonTitle.tooltip = k_ServerResetButtonToolTip;

            RefreshPopupUI();
            return m_Root;
        }

        void RefreshPopupUI()
        {
            if (m_Root == null)
                return;

            var server = m_ToolbarController.GetLocalServer();
            var currentStatus = server.GetCurrentServerStatus();
            var failure = server.GetLastServerFailure();
            UpdateServerStatusText(currentStatus, failure);
            UpdateServerStatusIcon(currentStatus, failure);
            UpdateServerStatusPid(currentStatus);
            UpdateServerToggleability(currentStatus);
            UpdateServerAdditionalSettings(currentStatus);
        }

        void UpdateServerStatusText(LocalCloudCodeServerStatus status, string failure)
        {
            // Prioritize failures if any
            if (status == LocalCloudCodeServerStatus.Idle && failure != null)
            {
                m_ServerStatusText.text = k_ServerStatusTextError;
                return;
            }

            var statusText = "";
            switch (status)
            {
                case LocalCloudCodeServerStatus.Idle:
                    statusText = k_ServerStatusTextIdle;
                    break;
                case LocalCloudCodeServerStatus.Started:
                    statusText = "";
                    break;
                case LocalCloudCodeServerStatus.Preparing:
                case LocalCloudCodeServerStatus.Starting:
                    statusText = k_ServerStatusTextStarting;
                    break;
                case LocalCloudCodeServerStatus.Stopping:
                    statusText = k_ServerStatusTextStopping;
                    break;
                default:
                    Debug.LogError("Unknown LocalCloudCodeServerStatus found in Toolbar.");
                    break;
            }
            m_ServerStatusText.text = statusText;
        }

        void UpdateServerStatusIcon(LocalCloudCodeServerStatus status, string failure)
        {
            m_ServerStatusIcon.ClearClassList();
            m_ServerStatusIcon.AddToClassList("icon");

            // Prioritize failures if any
            if (status == LocalCloudCodeServerStatus.Idle && failure != null)
            {
                m_ServerStatusIcon.AddToClassList(k_ServerIconStyleError);
                return;
            }

            switch (status)
            {
                case LocalCloudCodeServerStatus.Idle:
                    m_ServerStatusIcon.AddToClassList(k_ServerIconStyleIdle);
                    break;
                case LocalCloudCodeServerStatus.Started:
                    m_ServerStatusIcon.AddToClassList(k_ServerIconStyleStarted);
                    break;
                case LocalCloudCodeServerStatus.Preparing:
                case LocalCloudCodeServerStatus.Starting:
                    m_ServerStatusIcon.AddToClassList(k_ServerIconStyleStarting);
                    break;
                case LocalCloudCodeServerStatus.Stopping:
                    m_ServerStatusIcon.AddToClassList(k_ServerIconStyleStarting);
                    m_ServerActionButton.enabledSelf = false;
                    break;
                default:
                    Debug.LogError("Unknown LocalCloudCodeServerStatus found in Toolbar.");
                    break;
            }
        }

        void UpdateServerStatusPid(LocalCloudCodeServerStatus status)
        {
            var hasStarted = status == LocalCloudCodeServerStatus.Started;
            var displayStyle = hasStarted ? DisplayStyle.Flex : DisplayStyle.None;
            m_ServerPidText.style.display = displayStyle;
            m_ServerPidCopyButton.style.display = displayStyle;

            string pid = m_ToolbarController.GetLocalServer().GetServerPid().ToString();
            m_ServerPidText.text = hasStarted ? $"PID:{pid}" : "";
        }

        void UpdateServerToggleability(LocalCloudCodeServerStatus status)
        {
            // If the Editor is in play mode, disable the ability to start / stop the server
            var isInPlayMode = EditorApplication.isPlaying;
            m_ServerEditOnlyHelpbox.style.display = isInPlayMode ? DisplayStyle.Flex : DisplayStyle.None;
            m_ServerActionButton.enabledSelf = !isInPlayMode;

            switch (status)
            {
                case LocalCloudCodeServerStatus.Idle:
                    m_ServerActionButton.text = k_ServerActionButtonTextIdle;
                    break;
                case LocalCloudCodeServerStatus.Started:
                    m_ServerActionButton.text = k_ServerActionButtonTextStarted;
                    break;
                case LocalCloudCodeServerStatus.Preparing:
                case LocalCloudCodeServerStatus.Starting:
                    m_ServerActionButton.text = k_ServerActionButtonTextStarting;
                    break;
                case LocalCloudCodeServerStatus.Stopping:
                    m_ServerActionButton.text = k_ServerActionButtonTextStopping;
                    m_ServerActionButton.enabledSelf = false;
                    break;
                default:
                    Debug.LogError("Unknown LocalCloudCodeServerStatus found in Toolbar.");
                    break;
            }
        }

        void UpdateServerAdditionalSettings(LocalCloudCodeServerStatus status)
        {
            var isIdle = status == LocalCloudCodeServerStatus.Idle;
            m_ServerResetButton.enabledSelf = isIdle;
        }

        void OnServerActionButtonClicked()
        {
            var server = m_ToolbarController.GetLocalServer();
            var status = server.GetCurrentServerStatus();
            if (status == LocalCloudCodeServerStatus.Idle)
            {
                server.StartCompilationAndService();
            }
            else if (status is LocalCloudCodeServerStatus.Started
                     or LocalCloudCodeServerStatus.Starting
                     or LocalCloudCodeServerStatus.Preparing)
            {
                server.StopCompilationAndService();
            }
        }

        void OnServerSettingsClicked()
        {
            SettingsService.OpenProjectSettings(k_CloudCodeProjectSettingsPath);
        }
    }
}

#endif
#endif
