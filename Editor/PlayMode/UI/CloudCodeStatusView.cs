using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.Core.Editor.OrganizationHandler;
using Unity.Services.DeploymentApi.Editor;
using Unity.Multiplayer.PlayMode.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    // The Cloud Code section of the Play Mode Status window. The window hands scenario controllers no
    // runtime, so everything here is read from the scenario settings and the Cloud Code services.
    class CloudCodeStatusView : VisualElement
    {
        internal const string k_TargetName = "cloud-code-status__target";
        internal const string k_PortRowName = "cloud-code-status__port-row";
        internal const string k_PortName = "cloud-code-status__port";
        internal const string k_DeploymentName = "cloud-code-status__deployment";
        internal const string k_DeploymentIconName = "cloud-code-status__deployment-icon";
        internal const string k_OpenDeploymentWindowName = "cloud-code-status__open-deployment-window";
        internal const string k_ServerStateRowName = "cloud-code-status__server-state-row";
        internal const string k_ClearName = "cloud-code-status__clear";
        internal const string k_StopName = "cloud-code-status__stop";

        internal const string k_TitleIconName = "cloud-code-status__title-icon";
        internal const string k_TitleServerName = "cloud-code-status__title-server";
        internal const string k_TitleServerIconName = "cloud-code-status__title-server-icon";
        internal const string k_TitleCopyPidName = "cloud-code-status__title-copy-pid";
        internal const string k_TitleTargetName = "cloud-code-status__title-target";
        internal const string k_TitleEnvironmentName = "cloud-code-status__title-environment";
        internal const string k_TitleDashboardName = "cloud-code-status__title-dashboard";
        internal const string k_TitleRemoteStatusName = "cloud-code-status__title-remote-status";
        internal const string k_TitleDeployingIconName = "cloud-code-status__title-deploying-icon";
        internal const string k_TitleDeployingName = "cloud-code-status__title-deploying";

        internal const string k_TargetLabel = "Deploy Target";
        internal const string k_PortLabel = "Port";
        internal const string k_DeploymentLabel = "Module Deployment";
        internal const string k_ServerStateLabel = "Local Server State";
        internal const string k_OpenDeploymentWindowText = "Open Deployment Window";
        internal const string k_NoModulesText = "No Cloud Code modules in this project";
        internal const string k_NoEnvironmentText = "No environment selected";
        internal const string k_ClearText = "Clear";
        internal const string k_ClearingText = "Clearing";
        internal const string k_StopText = "Stop Local Cloud Code Server";
        internal const string k_StoppingText = "Stopping Local Cloud Code Server";
        internal const string k_IdleText = "Idle";
        internal const string k_ErrorClass = "cloud-code-status__row-value--error";

        const string k_RootClass = "cloud-code-status";
        const string k_RowClass = "cloud-code-status__row";
        const string k_RowLabelClass = "cloud-code-status__row-label";
        const string k_RowValueClass = "cloud-code-status__row-value";
        const string k_RowValueTextClass = "cloud-code-status__row-value-text";
        const string k_TitleTextClass = "cloud-code-status__title-text";
        const string k_TitleSpacerClass = "cloud-code-status__title-spacer";
        const string k_TitleCloudIconClass = "cloud-code-status__title-cloud-icon";
        const string k_IconClass = "cloud-code-status__icon";
        const string k_IconButtonClass = "cloud-code-status__icon-button";
        const string k_ClearClass = "cloud-code-status__clear";
        const string k_StopClass = "cloud-code-status__stop";
        const string k_TitleBarClass = "cloud-code-status__title-bar";
        const string k_BadgeClass = "cloud-code-status__badge";

        // Styled by the status window's own sheet, so the icon matches the instance icons.
        const string k_TitleIconClass = "unity-instance-status__title-icon";
        const string k_TitleIcon = "ToolbarIdle.png";

        // The icon the Services project settings use for their Go to Dashboard link.
        const string k_ExternalLinkIconPath = "Packages/com.unity.services.core/Editor/Core/UiHelpers/Images/ExternalLinkIcon.png";
        const string k_CloudCodeSettingsPath = "Project/Services/Cloud Code";
        const string k_EnvironmentsSettingsPath = "Project/Services/Environments";
        internal const string k_ClearTooltip =
            "Clears the local server data. A running server restarts. Not available in Play Mode.";
        internal const string k_StopTooltip =
            "Stops the local server. The next Play Mode run starts it again. Not available in Play Mode.";

        static readonly string k_StyleSheetPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "PlayMode", "UI", "Assets", "CloudCodeStatusView.uss");

        // The icons of the Local Cloud Code Server toolbar button and its popup.
        static readonly string k_IconsPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "Authoring", "Modules", "UI", "Assets", "Icons");

        static readonly string k_SectionIconsPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "PlayMode", "UI", "Assets", "Icons");

        readonly CloudCodeSettings m_Settings;
        readonly ICloudCodeLocalServer m_Server;
        readonly IEnvironmentsApi m_Environments;
        readonly Func<int> m_ModuleCount;
        readonly IReadOnlyList<INotifyCollectionChanged> m_ModuleCollections;
        readonly Func<bool> m_IsInPlayMode;
        readonly ICloudCodeRemoteDrift m_Drift;
        readonly Func<float?> m_DeployProgress;

        // Port edits to the stopped server raise no server event, so the settings asset is tracked instead.
        readonly VisualElement m_SettingsTracker = new() { style = { display = DisplayStyle.None } };
        CloudCodeLocalServerSettings m_TrackedSettings;

        readonly Label m_Target;
        readonly VisualElement m_PortRow;
        readonly Label m_Port;
        readonly Label m_Deployment;
        readonly VisualElement m_DeploymentIcon;
        readonly VisualElement m_ServerStateRow;
        readonly Button m_Clear;
        readonly Button m_Stop;
        // Set while Clear or Stop runs, so the other cannot act on the server at the same time.
        bool m_ServerActionRunning;

        public CloudCodeStatusView(CloudCodeSettings settings)
            : this(
                settings,
                CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>(),
                CloudCodeAuthoringServices.Instance.GetService<IEnvironmentsApi>(),
                () => CloudCodeDeploymentRecord.ModuleCount(CloudCodeAuthoringServices.Instance),
                new INotifyCollectionChanged[]
                {
                    CloudCodeAuthoringServices.Instance.GetService<CloudCodeModuleReferenceCollection>(),
                    CloudCodeAuthoringServices.Instance.GetService<CloudCodeModuleCollection>()
                },
                () => EditorApplication.isPlayingOrWillChangePlaymode,
                CloudCodeRemoteDrift.Instance,
                CurrentDeployProgress)
        {
        }

        static float? CurrentDeployProgress() => RemoteDeployProgress.TryGet(out var progress) ? progress : null;

        internal CloudCodeStatusView(
            CloudCodeSettings settings,
            ICloudCodeLocalServer server,
            IEnvironmentsApi environments,
            Func<int> moduleCount,
            IReadOnlyList<INotifyCollectionChanged> moduleCollections,
            Func<bool> isInPlayMode,
            ICloudCodeRemoteDrift drift,
            Func<float?> deployProgress)
        {
            m_Settings = settings;
            m_Server = server;
            m_Environments = environments;
            m_ModuleCount = moduleCount;
            m_ModuleCollections = moduleCollections;
            m_IsInPlayMode = isInPlayMode;
            m_Drift = drift;
            m_DeployProgress = deployProgress;

            AddToClassList(k_RootClass);
            AddStyleSheet(this);
            Add(m_SettingsTracker);

            var isLocal = settings.DeployTarget == CloudCodeDeployTarget.Local;

            m_Target = AddRow(this, k_TargetLabel, out var targetValue);
            m_Target.name = k_TargetName;
            if (!isLocal)
                targetValue.Add(IconButton(BuiltinIcon("SettingsIcon"), "Environment settings",
                    () => SettingsService.OpenProjectSettings(k_EnvironmentsSettingsPath)));

            m_Port = AddRow(this, k_PortLabel, out var portValue);
            m_Port.name = k_PortName;
            m_PortRow = portValue.parent;
            m_PortRow.name = k_PortRowName;
            portValue.Add(IconButton(BuiltinIcon("SettingsIcon"), "Local Cloud Code server settings",
                () => SettingsService.OpenProjectSettings(k_CloudCodeSettingsPath)));

            m_Deployment = AddRow(this, k_DeploymentLabel, out var deploymentValue);
            m_Deployment.name = k_DeploymentName;
            m_DeploymentIcon = Icon(null);
            m_DeploymentIcon.name = k_DeploymentIconName;
            deploymentValue.Insert(0, m_DeploymentIcon);
            var openDeploymentWindow = IconButton(SectionIcon("WindowInstance"), k_OpenDeploymentWindowText, OpenDeploymentWindow);
            openDeploymentWindow.name = k_OpenDeploymentWindowName;
            // The Deployment package registers the window; without it there is nothing to open.
            openDeploymentWindow.SetEnabled(Deployments.Instance.DeploymentWindow != null);
            deploymentValue.Add(openDeploymentWindow);

            // The row has a button where the other rows have text.
            AddRow(this, k_ServerStateLabel, out var serverStateValue).RemoveFromHierarchy();
            m_ServerStateRow = serverStateValue.parent;
            m_ServerStateRow.name = k_ServerStateRowName;
            m_Clear = new Button(() => _ = ClearServerStateAsync()) { name = k_ClearName, text = k_ClearText, tooltip = k_ClearTooltip };
            m_Clear.AddToClassList(k_ClearClass);
            serverStateValue.Add(m_Clear);

            // Spans the section under the rows, like the Stop button of a server instance.
            m_Stop = new Button(() => _ = StopServerAsync()) { name = k_StopName, text = k_StopText, tooltip = k_StopTooltip };
            m_Stop.AddToClassList(k_StopClass);
            Add(m_Stop);

            // The port, the server state and stopping belong to the local server only.
            Show(m_PortRow, isLocal);
            Show(m_ServerStateRow, isLocal);
            Show(m_Stop, isLocal);

            RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
            RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());

            Refresh();
        }

        void Subscribe()
        {
            m_Server.OnServerStatusChanged += OnServerStatusChanged;
            CloudCodeDeploymentRecord.Changed += OnDeploymentRecorded;
            if (m_Environments != null)
                m_Environments.PropertyChanged += OnEnvironmentChanged;
            foreach (var collection in m_ModuleCollections)
                collection.CollectionChanged += OnModulesChanged;
            CloudCodeScenarioAssetWatcher.Changed += OnAssetsChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.focusChanged += OnEditorFocusChanged;
            m_Drift.Changed += Refresh;
            m_Drift.BaselineChanged += OnDeploymentBaselineChanged;
            ScenarioRunner.StatusChanged += OnScenarioStatusChanged;
            TrackServerSettings();
            CheckDrift();
            Refresh();
        }

        void Unsubscribe()
        {
            m_Server.OnServerStatusChanged -= OnServerStatusChanged;
            CloudCodeDeploymentRecord.Changed -= OnDeploymentRecorded;
            if (m_Environments != null)
                m_Environments.PropertyChanged -= OnEnvironmentChanged;
            foreach (var collection in m_ModuleCollections)
                collection.CollectionChanged -= OnModulesChanged;
            CloudCodeScenarioAssetWatcher.Changed -= OnAssetsChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.focusChanged -= OnEditorFocusChanged;
            m_Drift.Changed -= Refresh;
            m_Drift.BaselineChanged -= OnDeploymentBaselineChanged;
            ScenarioRunner.StatusChanged -= OnScenarioStatusChanged;
        }

        // A deploy from the Deployment window records a new baseline for each module it deployed, without
        // a scenario record or an asset import, so the drift is measured again from it. The store does not
        // promise the main thread, so the check waits for the next editor update.
        void OnDeploymentBaselineChanged() => EditorApplication.delayCall += CheckDrift;

        // Only the remote environment can drift: the local server is redeployed on every run and on focus.
        void CheckDrift()
        {
            if (m_Settings.DeployTarget == CloudCodeDeployTarget.Remote)
                m_Drift.Check();
        }

        void OnPlayModeStateChanged(PlayModeStateChange change) => Refresh();

        // Raised on every progress or state change of a node, so the percentage ticks through the
        // propagation wait. The node sets them from continuations on the main thread, so the refresh is
        // not deferred like the server and environment events below.
        void OnScenarioStatusChanged(ScenarioStatusData status) => Refresh();

        // Edits made in an external IDE raise no event until the editor regains focus.
        void OnEditorFocusChanged(bool focused)
        {
            if (focused)
                CheckDrift();
        }

        void OnDeploymentRecorded()
        {
            CheckDrift();
            Refresh();
        }

        // Neither event promises the main thread, so the refresh is deferred to the next editor update.
        void OnServerStatusChanged(object sender, LocalCloudCodeServerStatus status) => EditorApplication.delayCall += Refresh;

        void OnEnvironmentChanged(object sender, PropertyChangedEventArgs args) => EditorApplication.delayCall += () =>
        {
            CheckDrift();
            Refresh();
        };

        void OnModulesChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            CheckDrift();
            Refresh();
        }

        // The settings asset can be created, replaced or edited on disk after the view is built, and an
        // imported module source can drift from the remote environment.
        void OnAssetsChanged(IReadOnlyCollection<string> paths)
        {
            CheckDrift();
            foreach (var path in paths)
            {
                if (!CloudCodeLocalServerSettings.IsSettingsAssetPath(path))
                    continue;

                TrackServerSettings();
                Refresh();
                return;
            }
        }

        void TrackServerSettings()
        {
            var settings = CloudCodeLocalServerSettings.LoadExisting();
            if (settings == m_TrackedSettings)
                return;

            m_TrackedSettings = settings;
            // An element tracks one serialized object at most, so a new asset gets a new tracker.
            m_SettingsTracker.Clear();
            if (settings == null)
                return;

            var tracker = new VisualElement();
            tracker.TrackSerializedObjectValue(new SerializedObject(settings), _ => Refresh());
            m_SettingsTracker.Add(tracker);
        }

        internal void Refresh()
        {
            var target = m_Settings.DeployTarget;

            m_Target.text = DescribeTarget(target, GetEnvironmentName(m_Environments));
            m_Port.text = m_Server.GetPort().ToString();

            RefreshDeployment(target);

            if (m_ServerActionRunning)
                return;

            // A running scenario still needs the server, and a start or stop in progress has to finish
            // before the server can be acted on.
            var status = m_Server.GetCurrentServerStatus();
            var canAct = !m_IsInPlayMode();
            m_Clear.SetEnabled(canAct && status is LocalCloudCodeServerStatus.Idle or LocalCloudCodeServerStatus.Started);
            m_Stop.SetEnabled(canAct && status == LocalCloudCodeServerStatus.Started);
        }

        internal Task StopServerAsync()
        {
            return RunServerAction(m_Stop, k_StopText, k_StoppingText, async () =>
            {
                await m_Server.StopCompilationAndService();
                LocalServerOwnership.Release();
            });
        }

        async Task RunServerAction(Button button, string text, string runningText, Func<Task> action)
        {
            m_ServerActionRunning = true;
            m_Clear.SetEnabled(false);
            m_Stop.SetEnabled(false);
            button.text = runningText;
            try
            {
                await action();
            }
            finally
            {
                button.text = text;
                m_ServerActionRunning = false;
                Refresh();
            }
        }

        void RefreshDeployment(CloudCodeDeployTarget target)
        {
            m_Deployment.RemoveFromClassList(k_ErrorClass);
            m_Deployment.tooltip = null;

            if (target == CloudCodeDeployTarget.Remote)
                RefreshRemoteDeployment();
            else
                RefreshLocalDeployment();
        }

        void RefreshLocalDeployment()
        {
            SetStatusIcon(m_DeploymentIcon, RemoteStatus.None);

            // A record from the other target describes something this scenario no longer runs, and a
            // stopped local server runs nothing, so the next run deploys everything again. A failure
            // still shows, since the server is often stopped because it failed.
            if (CloudCodeDeploymentRecord.TryGet(out var deployment)
                && deployment.Target == CloudCodeDeployTarget.Local
                && (deployment.Outcome == CloudCodeDeploymentOutcome.Failed
                    || m_Server.GetCurrentServerStatus() == LocalCloudCodeServerStatus.Started))
            {
                switch (deployment.Outcome)
                {
                    case CloudCodeDeploymentOutcome.Succeeded:
                        ShowDeployed(deployment);
                        return;
                    case CloudCodeDeploymentOutcome.Failed:
                        ShowFailed(deployment);
                        return;
                    case CloudCodeDeploymentOutcome.NoModules:
                        m_Deployment.text = k_NoModulesText;
                        return;
                }
            }

            m_Deployment.text = DescribeToDeploy(CloudCodeDeployTarget.Local, m_ModuleCount());
        }

        void RefreshRemoteDeployment()
        {
            var environment = GetEnvironmentName(m_Environments);
            var progress = m_DeployProgress();
            if (progress != null)
            {
                SetStatusIcon(m_DeploymentIcon, RemoteStatus.Deploying);
                m_Deployment.text = RemoteDeployProgress.Label(progress.Value);
                m_Deployment.tooltip = environment == null
                    ? null
                    : $"Deploying to {environment}. Instances launch once the modules have propagated.";
                return;
            }

            var count = m_ModuleCount();
            var status = GetRemoteStatus(environment, m_Drift.ChangedModules, count, out var deployment);
            SetStatusIcon(m_DeploymentIcon, status);

            switch (status)
            {
                case RemoteStatus.Failed:
                    ShowFailed(deployment);
                    break;
                case RemoteStatus.Drifting:
                    m_Deployment.text = DescribeChanged(m_Drift.ChangedModules.Count);
                    m_Deployment.tooltip =
                        $"Changed or added since the last deploy to {environment}: {string.Join(", ", m_Drift.ChangedModules)}. " +
                        "Entering Play Mode deploys them.";
                    break;
                case RemoteStatus.Deployed:
                    ShowDeployed(deployment);
                    break;
                case RemoteStatus.ToDeploy:
                    m_Deployment.text = DescribeToDeploy(CloudCodeDeployTarget.Remote, count);
                    m_Deployment.tooltip = "Entering Play Mode deploys your Cloud Code modules to the environment before instances launch.";
                    break;
                default:
                    m_Deployment.text = k_NoModulesText;
                    break;
            }
        }

        void ShowDeployed(CloudCodeDeployment deployment)
        {
            m_Deployment.text = $"Latest deploy on {FormatTime(deployment.TimeUtc)}";
            m_Deployment.tooltip = $"Deployed to {deployment.Environment}";
        }

        void ShowFailed(CloudCodeDeployment deployment)
        {
            m_Deployment.text = $"Failed at {FormatTime(deployment.TimeUtc)}";
            m_Deployment.tooltip = deployment.Message;
            m_Deployment.AddToClassList(k_ErrorClass);
        }

        internal enum RemoteStatus
        {
            None,
            ToDeploy,
            Drifting,
            Deployed,
            Failed,
            Deploying
        }

        // Shared by the section and its title bar. A record for another environment says nothing about
        // the active one. Drift, which counts modules changed or added since their last deploy, only means
        // something after a successful deploy: before one, every module is simply to deploy.
        internal static RemoteStatus GetRemoteStatus(
            string environment, IReadOnlyList<string> changedModules, int moduleCount, out CloudCodeDeployment deployment)
        {
            var hasRecord = CloudCodeDeploymentRecord.TryGet(out deployment)
                && deployment.Target == CloudCodeDeployTarget.Remote
                && deployment.Environment == environment;

            if (hasRecord && deployment.Outcome == CloudCodeDeploymentOutcome.Failed)
                return RemoteStatus.Failed;
            if (hasRecord && deployment.Outcome == CloudCodeDeploymentOutcome.Succeeded)
                return changedModules.Count > 0 ? RemoteStatus.Drifting : RemoteStatus.Deployed;
            return moduleCount > 0 ? RemoteStatus.ToDeploy : RemoteStatus.None;
        }

        internal static string DescribeChanged(int count) => $"{count} module(s) changed locally";

        static void SetStatusIcon(VisualElement icon, RemoteStatus status)
        {
            Texture2D texture = status switch
            {
                RemoteStatus.Deployed => Icons.GetImage(Icons.ImageName.CompletedTask),
                RemoteStatus.ToDeploy or RemoteStatus.Drifting => Icons.GetImage(Icons.ImageName.Warning),
                RemoteStatus.Failed => Icons.GetImage(Icons.ImageName.Error),
                RemoteStatus.Deploying => Icons.GetImage(Icons.ImageName.Loading),
                _ => null
            };
            icon.style.backgroundImage = texture;
            Show(icon, texture != null);
        }

        internal static string DescribeTarget(CloudCodeDeployTarget target, string environment)
        {
            if (target == CloudCodeDeployTarget.Local)
                return "Local (localhost)";

            return environment == null ? k_NoEnvironmentText : $"Environment: {environment}";
        }

        internal static string DescribeToDeploy(CloudCodeDeployTarget target, int moduleCount)
        {
            if (moduleCount == 0)
                return k_NoModulesText;

            return target == CloudCodeDeployTarget.Local ? $"{moduleCount} to deploy locally" : $"{moduleCount} to deploy";
        }

        // The saved settings can keep a name without an id, or the id of an environment this project no
        // longer has, and the Environments settings treat both as none selected. Until the project's list
        // has loaded, the saved id is all there is to go on.
        internal static string GetEnvironmentName(IEnvironmentsApi environments)
        {
            var id = environments?.ActiveEnvironmentId;
            if (!EnvironmentIds.IsSet(id))
                return null;

            if (environments.Environments == null)
                return DeployRemoteCloudCodeNode.EnvironmentDisplayName(environments);

            foreach (var environment in environments.Environments)
            {
                if (environment.Id == id)
                    return string.IsNullOrEmpty(environment.Name) ? id.ToString() : environment.Name;
            }

            return null;
        }

        // The server keeps running between runs, so Clear restarts a running one around the clear.
        internal Task ClearServerStateAsync()
        {
            return RunServerAction(m_Clear, k_ClearText, k_ClearingText, async () =>
            {
                var wasRunning = m_Server.GetCurrentServerStatus() == LocalCloudCodeServerStatus.Started;
                var wasOwned = wasRunning && LocalServerOwnership.IsOwned(m_Server);

                if (wasRunning)
                    await m_Server.StopCompilationAndService();

                m_Server.ClearServerState();

                if (wasRunning)
                {
                    await m_Server.StartCompilationAndService();
                    // The restarted server has a new process, which the scenario still has to stop on a switch.
                    if (wasOwned && m_Server.GetCurrentServerStatus() == LocalCloudCodeServerStatus.Started)
                        LocalServerOwnership.Claim(m_Server);
                }
                else
                {
                    // Clearing is quick; the pause shows that it happened, as the toolbar popup does.
                    await Task.Delay(500);
                }
            });
        }

        // The title bar: the Cloud Code icon before the section name, then on the right the local server
        // status for a Local target, or the target and environment for a Remote one.
        internal static VisualElement CreateTitleBar(CloudCodeSettings settings)
        {
            var services = CloudCodeAuthoringServices.Instance;
            return CreateTitleBar(
                settings,
                services.GetService<ICloudCodeLocalServer>(),
                services.GetService<IEnvironmentsApi>(),
                CloudCodeRemoteDrift.Instance,
                () => CloudCodeDeploymentRecord.ModuleCount(services),
                CurrentDeployProgress);
        }

        internal static VisualElement CreateTitleBar(
            CloudCodeSettings settings,
            ICloudCodeLocalServer server,
            IEnvironmentsApi environments,
            ICloudCodeRemoteDrift drift,
            Func<int> moduleCount,
            Func<float?> deployProgress)
        {
            var bar = new VisualElement();
            bar.AddToClassList(k_TitleBarClass);
            AddStyleSheet(bar);

            var cloudIcon = new VisualElement { name = k_TitleIconName };
            cloudIcon.AddToClassList(k_TitleIconClass);
            cloudIcon.AddToClassList(k_TitleCloudIconClass);
            cloudIcon.style.backgroundImage = PackageIcon(k_TitleIcon);
            // The icon leaves the bar for its place before the section name, so it carries the sheet itself.
            AddStyleSheet(cloudIcon);
            cloudIcon.RegisterCallback<AttachToPanelEvent>(_ => MoveBeforeTitleLabel(cloudIcon));
            bar.Add(cloudIcon);

            // Takes the free space, so the content sits at the right. Once there is none left, the content
            // starts right after the section name and the bar clips it at the right edge, like the status
            // of an instance section.
            var spacer = new VisualElement();
            spacer.AddToClassList(k_TitleSpacerClass);
            bar.Add(spacer);

            if (settings.DeployTarget == CloudCodeDeployTarget.Local)
                AddServerStatus(bar, server);
            else
                AddRemoteBadges(bar, environments, drift, moduleCount, deployProgress);

            return bar;
        }

        static void AddServerStatus(VisualElement bar, ICloudCodeLocalServer server)
        {
            var icon = Icon(null);
            icon.name = k_TitleServerIconName;
            var label = new Label { name = k_TitleServerName };
            label.AddToClassList(k_TitleTextClass);
            var copyPid = IconButton(PackageIcon("copy.png"), "Copy the server process id",
                () => GUIUtility.systemCopyBuffer = server.GetServerPid().ToString());
            copyPid.name = k_TitleCopyPidName;

            bar.Add(icon);
            bar.Add(label);
            bar.Add(copyPid);

            // A running server shows its process id, a stopped one reads Idle like an idle instance.
            void Refresh()
            {
                var status = server.GetCurrentServerStatus();
                var started = status == LocalCloudCodeServerStatus.Started;
                var idle = status == LocalCloudCodeServerStatus.Idle;
                icon.style.backgroundImage = started ? PackageIcon("PopupStarted.png") : Icons.GetImage(Icons.ImageName.Idle);
                label.text = started ? $"PID: {server.GetServerPid()}" : idle ? k_IdleText : string.Empty;
                Show(icon, started || idle);
                Show(label, started || idle);
                Show(copyPid, started);
            }

            // The event does not promise the main thread, so the refresh waits for the next editor update.
            void OnStatusChanged(object sender, LocalCloudCodeServerStatus status) => EditorApplication.delayCall += Refresh;

            bar.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                server.OnServerStatusChanged += OnStatusChanged;
                Refresh();
            });
            bar.RegisterCallback<DetachFromPanelEvent>(_ => server.OnServerStatusChanged -= OnStatusChanged);
            Refresh();
        }

        static void AddRemoteBadges(
            VisualElement bar,
            IEnvironmentsApi environments,
            ICloudCodeRemoteDrift drift,
            Func<int> moduleCount,
            Func<float?> deployProgress)
        {
            var status = Icon(null);
            status.name = k_TitleRemoteStatusName;
            var deployingIcon = Icon(Icons.GetImage(Icons.ImageName.Loading));
            deployingIcon.name = k_TitleDeployingIconName;
            var deploying = new Label { name = k_TitleDeployingName };
            deploying.AddToClassList(k_TitleTextClass);
            var target = Badge(CloudCodeDeployTarget.Remote.ToString());
            target.name = k_TitleTargetName;
            var environment = Badge(string.Empty);
            environment.name = k_TitleEnvironmentName;
            var dashboard = IconButton(
                EditorGUIUtility.Load(k_ExternalLinkIconPath) as Texture2D, "Open Cloud Code in the Unity Dashboard",
                () => Application.OpenURL(DashboardUrl()));
            dashboard.name = k_TitleDashboardName;

            bar.Add(status);
            bar.Add(deployingIcon);
            bar.Add(deploying);
            bar.Add(target);
            bar.Add(environment);
            bar.Add(dashboard);

            void Refresh()
            {
                var name = GetEnvironmentName(environments);
                environment.text = name ?? k_NoEnvironmentText;

                var progress = deployProgress();
                var isDeploying = progress != null;
                Show(deployingIcon, isDeploying);
                Show(deploying, isDeploying);
                Show(target, !isDeploying);
                Show(environment, !isDeploying);
                if (isDeploying)
                {
                    deploying.text = RemoteDeployProgress.Label(progress.Value);
                    Show(status, false);
                    return;
                }

                SetStatusIcon(status, GetRemoteStatus(name, drift.ChangedModules, moduleCount(), out _));
            }

            void OnEnvironmentChanged(object sender, PropertyChangedEventArgs args) => EditorApplication.delayCall += Refresh;
            void OnScenarioStatusChanged(ScenarioStatusData scenarioStatus) => Refresh();

            // The section checks the drift, the title bar only shows it.
            bar.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                if (environments != null)
                    environments.PropertyChanged += OnEnvironmentChanged;
                drift.Changed += Refresh;
                CloudCodeDeploymentRecord.Changed += Refresh;
                ScenarioRunner.StatusChanged += OnScenarioStatusChanged;
                Refresh();
            });
            bar.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (environments != null)
                    environments.PropertyChanged -= OnEnvironmentChanged;
                drift.Changed -= Refresh;
                CloudCodeDeploymentRecord.Changed -= Refresh;
                ScenarioRunner.StatusChanged -= OnScenarioStatusChanged;
            });

            Refresh();
        }

        // The same page as the Go to Dashboard button in the Cloud Code project settings.
        static string DashboardUrl()
            => $"https://dashboard.unity3d.com/organizations/{OrganizationProvider.Organization.Key}" +
               $"/projects/{CloudProjectSettings.projectId}/cloud-code/overview";

        // The window puts title bar UI after the section name, so the icon moves itself in front of the
        // name once attached. Moving it re-raises the attach event, hence the guard.
        static void MoveBeforeTitleLabel(VisualElement icon)
        {
            for (var ancestor = icon.parent; ancestor != null; ancestor = ancestor.parent)
            {
                var label = ancestor.Q(ScenarioControllerStatusElement.k_TitleLabelName);
                if (label == null)
                    continue;

                if (label.parent != icon.parent)
                    label.parent.Insert(label.parent.IndexOf(label), icon);
                return;
            }
        }

        static void OpenDeploymentWindow() => Deployments.Instance.DeploymentWindow?.OpenWindow();

        static Label AddRow(VisualElement parent, string label, out VisualElement value)
        {
            var row = new VisualElement();
            row.AddToClassList(k_RowClass);

            var labelElement = new Label(label);
            labelElement.AddToClassList(k_RowLabelClass);

            value = new VisualElement();
            value.AddToClassList(k_RowValueClass);

            var text = new Label();
            text.AddToClassList(k_RowValueTextClass);
            value.Add(text);

            row.Add(labelElement);
            row.Add(value);
            parent.Add(row);
            return text;
        }

        static Label Badge(string text)
        {
            var badge = new Label(text);
            badge.AddToClassList(k_BadgeClass);
            return badge;
        }

        static VisualElement Icon(Texture2D texture)
        {
            var icon = new VisualElement();
            icon.AddToClassList(k_IconClass);
            icon.style.backgroundImage = texture;
            return icon;
        }

        static VisualElement IconButton(Texture2D texture, string tooltip, Action onClick)
        {
            var button = Icon(texture);
            button.AddToClassList(k_IconButtonClass);
            button.tooltip = tooltip;
            // In the title bar the icon sits on the foldout toggle, so a click must not also fold the section.
            button.RegisterCallback<MouseDownEvent>(evt => evt.StopImmediatePropagation(), TrickleDown.TrickleDown);
            button.RegisterCallback<ClickEvent>(evt =>
            {
                if (button.enabledInHierarchy)
                    onClick();
                evt.StopImmediatePropagation();
            });
            return button;
        }

        static void Show(VisualElement element, bool visible)
            => element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;

        static void AddStyleSheet(VisualElement element)
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetPath);
            if (styleSheet != null)
                element.styleSheets.Add(styleSheet);
        }

        static Texture2D PackageIcon(string file)
        {
            // Not every icon has a dark variant; fall back to the light one.
            if (EditorGUIUtility.isProSkin
                && EditorGUIUtility.Load(PathUtils.Join(k_IconsPath, "d_" + file)) is Texture2D dark)
                return dark;

            return EditorGUIUtility.Load(PathUtils.Join(k_IconsPath, file)) as Texture2D;
        }

        // The section's own icons come in light and dark (d_), each at 1x and 2x (@2x). EditorGUIUtility.Load
        // takes the exact file, so the variant is picked here. A 2x texture in the same box stays sharp on Retina.
        internal static Texture2D SectionIcon(string name)
        {
            var skin = EditorGUIUtility.isProSkin ? "d_" : string.Empty;
            var density = EditorGUIUtility.pixelsPerPoint > 1 ? "@2x" : string.Empty;
            return EditorGUIUtility.Load(PathUtils.Join(k_SectionIconsPath, $"{skin}{name}{density}.png")) as Texture2D
                ?? EditorGUIUtility.Load(PathUtils.Join(k_SectionIconsPath, $"{skin}{name}.png")) as Texture2D;
        }

        // IconContent picks the skin's variant and the @2x texture on a high-density display; FindTexture
        // returns the 1x texture, which a Retina screen scales up and blurs.
        static Texture2D BuiltinIcon(string name) => EditorGUIUtility.IconContent(name).image as Texture2D;
        static string FormatTime(DateTime timeUtc)
        {
            var local = timeUtc.ToLocalTime();
            return local.Date == DateTime.Today ? local.ToString("HH:mm:ss") : local.ToString("MMM d, HH:mm:ss");
        }
    }
}
