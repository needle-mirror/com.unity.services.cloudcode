using System.Collections.Generic;
using Unity.Multiplayer.PlayMode.Editor;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using LocalInstanceSettings = Unity.Multiplayer.PlayMode.Editor.LocalPlayerController.InstanceSettings;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [CustomPropertyDrawer(typeof(PlayModeControllerItem<CloudCodeController, CloudCodeSettings>))]
    class CloudCodeScenarioItemDrawer : PlayModeControllerItemDrawer
    {
        internal const string k_DeployCloudCodeLabel = "Deploy to Cloud Code";
        internal const string k_DeployTargetLabel = "Deploy Target";
        internal const string k_DeployCloudCodeName = "cloud-code-deploy-toggle";
        internal const string k_DeployTargetName = "cloud-code-deploy-target";
        internal const string k_WarningName = "cloud-code-local-instance-warning";
        internal const string k_CopyArgumentName = "cloud-code-local-instance-warning__copy-argument";
        internal const string k_DocumentationName = "cloud-code-local-instance-warning__documentation";
        internal const string k_DevelopmentBuildWarningName = "cloud-code-development-build-warning";
        internal const string k_OpenBuildProfilesName = "cloud-code-development-build-warning__open-build-profiles";
        internal const string k_DevelopmentBuildDocumentationName = "cloud-code-development-build-warning__documentation";

        const string k_AlignedFieldClass = "unity-base-field__aligned";
        const string k_RootClass = "cloud-code-scenario-item";
        const string k_DeployTargetClass = "cloud-code-scenario-item__deploy-target";
        const string k_WarningClass = "cloud-code-scenario-item__warning";
        const string k_WarningActionsClass = "cloud-code-scenario-item__warning-actions";
        const string k_LinkClass = "cloud-code-scenario-item__link";
        const string k_AfterFocusedButtonClass = "cloud-code-scenario-item__deploy-target-button--after-focused";
        const string k_LocalTooltip = "Run a local Cloud Code server on your machine and deploy your Cloud Code modules to it.";
        const string k_RemoteTooltip = "Connect to the Unity Cloud Code service and deploy your Cloud Code modules to the remote server.";
        const string k_CopyArgumentText = "Copy Argument";
        const string k_OpenBuildProfilesText = "Open Build Profiles";
        const string k_DocumentationText = "View Documentation";
        const string k_BuildProfilesMenuPath = "File/Build Profiles";
        const string k_DocumentationUrl =
            "https://docs.unity.com/en-us/cloud-code/3.0.0/modules/how-to-guides/local-cloud-code-server";

        static readonly string k_StyleSheetPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "PlayMode", "UI", "Assets", "CloudCodeScenarioItemDrawer.uss");

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            // The sheet also restyles the root the base drawer builds, so it has to hang from the root.
            var root = base.CreatePropertyGUI(property);
            root.AddToClassList(k_RootClass);
            root.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetPath));
            return root;
        }

        protected override VisualElement CreateSettingsField(SerializedProperty property)
        {
            var settingsProperty = property.FindPropertyRelative(IPlayModeControllerItem.k_SettingsPropertyPath);
            var deployProperty = settingsProperty.FindPropertyRelative(nameof(CloudCodeSettings.DeployCloudCode));
            var targetProperty = settingsProperty.FindPropertyRelative(nameof(CloudCodeSettings.DeployTarget));
            var serializedObject = property.serializedObject;

            var container = new VisualElement();

            var deployToggle = new Toggle(k_DeployCloudCodeLabel) { name = k_DeployCloudCodeName };
            deployToggle.AddToClassList(k_AlignedFieldClass);
            deployToggle.BindProperty(deployProperty);
            deployToggle.Bind(serializedObject);
            container.Add(deployToggle);

            var targetField = CreateDeployTargetField(targetProperty);
            container.Add(targetField);

            var copyArgumentButton = new Button(() => EditorGUIUtility.systemCopyBuffer = CloudCodeLocalArgument.GetArgument(GetConfiguredPort()))
            {
                name = k_CopyArgumentName,
                text = k_CopyArgumentText,
            };
            var warning = CreateWarning(k_WarningName, copyArgumentButton, k_DocumentationName);
            container.Add(warning);

            var openBuildProfilesButton = new Button(() => EditorApplication.ExecuteMenuItem(k_BuildProfilesMenuPath))
            {
                name = k_OpenBuildProfilesName,
                text = k_OpenBuildProfilesText,
            };
            var developmentBuildWarning = CreateWarning(k_DevelopmentBuildWarningName, openBuildProfilesButton, k_DevelopmentBuildDocumentationName);
            container.Add(developmentBuildWarning);

            container.TrackPropertyValue(deployProperty, p => SetDisplayed(targetField, p.boolValue));
            SetDisplayed(targetField, deployProperty.boolValue);

            var assetTracker = new AssetTracker(container, Refresh);

            void Refresh()
            {
                var scenario = serializedObject.targetObject as OrchestratedScenario;
                var profiles = LocalInstanceDevelopmentBuild.GetLocalInstanceBuildProfiles(scenario);
                RefreshWarning(warning, copyArgumentButton, scenario, deployProperty, targetProperty);
                RefreshDevelopmentBuildWarning(developmentBuildWarning, profiles, deployProperty, targetProperty);
            }

            void RetargetAndRefresh()
            {
                var scenario = serializedObject.targetObject as OrchestratedScenario;
                assetTracker.Retarget(GetTrackedAssets(LocalInstanceDevelopmentBuild.GetLocalInstanceBuildProfiles(scenario)));
                Refresh();
            }

            // The settings asset is checked by path because it may not exist yet, so it may not be watched.
            void OnAssetsChanged(IReadOnlyCollection<string> paths)
            {
                if (assetTracker.Watches(paths) || ContainsSettingsAssetPath(paths))
                    RetargetAndRefresh();
            }

            container.TrackSerializedObjectValue(serializedObject, _ => RetargetAndRefresh());
            container.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                CloudCodeScenarioAssetWatcher.Changed += OnAssetsChanged;
                RetargetAndRefresh();
            });
            container.RegisterCallback<DetachFromPanelEvent>(_ => CloudCodeScenarioAssetWatcher.Changed -= OnAssetsChanged);

            RetargetAndRefresh();

            return container;
        }

        static void SetDisplayed(VisualElement element, bool displayed)
            => element.style.display = displayed ? DisplayStyle.Flex : DisplayStyle.None;

        // The editor only binds a ToggleButtonGroup to a ToggleButtonGroupState property, so an enum
        // property is mirrored by hand in both directions.
        static ToggleButtonGroup CreateDeployTargetField(SerializedProperty targetProperty)
        {
            var optionNames = targetProperty.enumDisplayNames;
            var field = new ToggleButtonGroup(k_DeployTargetLabel)
            {
                name = k_DeployTargetName,
                isMultipleSelection = false,
                allowEmptySelection = false,
            };
            field.AddToClassList(k_AlignedFieldClass);
            field.AddToClassList(k_DeployTargetClass);

            var buttons = new Button[optionNames.Length];
            for (var i = 0; i < optionNames.Length; i++)
            {
                buttons[i] = new Button { text = optionNames[i], tooltip = GetTooltip((CloudCodeDeployTarget)i) };
                field.Add(buttons[i]);
            }

            for (var i = 0; i < buttons.Length; i++)
                MarkButtonAfterFocused(buttons[i], i + 1 < buttons.Length ? buttons[i + 1] : null);

            field.SetValueWithoutNotify(ToState(targetProperty.enumValueIndex, optionNames.Length));
            field.TrackPropertyValue(targetProperty, p => field.SetValueWithoutNotify(ToState(p.enumValueIndex, optionNames.Length)));
            field.RegisterValueChangedCallback(evt =>
            {
                var selected = FirstActiveOption(evt.newValue, optionNames.Length);
                if (selected < 0 || selected == targetProperty.enumValueIndex)
                    return;

                targetProperty.enumValueIndex = selected;
                targetProperty.serializedObject.ApplyModifiedProperties();
            });

            return field;
        }

        static void MarkButtonAfterFocused(Button button, Button next)
        {
            button.RegisterCallback<FocusInEvent>(_ => next?.AddToClassList(k_AfterFocusedButtonClass));
            button.RegisterCallback<FocusOutEvent>(_ => next?.RemoveFromClassList(k_AfterFocusedButtonClass));
        }

        static string GetTooltip(CloudCodeDeployTarget target) => target switch
        {
            CloudCodeDeployTarget.Local => k_LocalTooltip,
            CloudCodeDeployTarget.Remote => k_RemoteTooltip,
            _ => null,
        };

        static ToggleButtonGroupState ToState(int selectedIndex, int length)
            => new(selectedIndex >= 0 && selectedIndex < length ? 1UL << selectedIndex : 0UL, length);

        static int FirstActiveOption(ToggleButtonGroupState state, int length)
        {
            for (var i = 0; i < length; i++)
            {
                if (state[i])
                    return i;
            }

            return -1;
        }

        static HelpBox CreateWarning(string name, Button actionButton, string documentationName)
        {
            var warning = new HelpBox(string.Empty, HelpBoxMessageType.Warning) { name = name };
            warning.AddToClassList(k_WarningClass);

            var actions = new VisualElement();
            actions.AddToClassList(k_WarningActionsClass);

            var documentationButton = new Button(() => Application.OpenURL(k_DocumentationUrl))
            {
                name = documentationName,
                text = k_DocumentationText,
            };
            documentationButton.AddToClassList(k_LinkClass);

            actions.Add(actionButton);
            actions.Add(documentationButton);

            // The help box lays its children out in a row, so the actions wrap onto their own line.
            warning.Add(actions);
            return warning;
        }

        static void RefreshWarning(
            HelpBox warning,
            Button copyArgumentButton,
            OrchestratedScenario scenario,
            SerializedProperty deployProperty,
            SerializedProperty targetProperty)
        {
            var target = (CloudCodeDeployTarget)targetProperty.intValue;
            var text = scenario != null && deployProperty.boolValue
                ? CloudCodeLocalArgument.GetWarning(target, GetConfiguredPort(), GetLocalInstanceArguments(scenario))
                : null;

            Show(warning, text);
            copyArgumentButton.style.display = target == CloudCodeDeployTarget.Local ? DisplayStyle.Flex : DisplayStyle.None;
        }

        static void RefreshDevelopmentBuildWarning(
            HelpBox warning,
            List<BuildProfile> profiles,
            SerializedProperty deployProperty,
            SerializedProperty targetProperty)
        {
            var target = (CloudCodeDeployTarget)targetProperty.intValue;
            var text = deployProperty.boolValue ? LocalInstanceDevelopmentBuild.GetWarning(target, profiles) : null;

            Show(warning, text);
        }

        static void Show(HelpBox warning, string text)
        {
            warning.text = text ?? string.Empty;
            warning.style.display = text == null ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // Assets the scenario does not serialize but the warnings depend on, so editing them refreshes the warnings.
        static List<Object> GetTrackedAssets(List<BuildProfile> profiles)
        {
            var assets = new List<Object>(profiles.Count + 1);
            var settings = LoadServerSettings();
            if (settings != null)
                assets.Add(settings);

            foreach (var profile in profiles)
            {
                if (!assets.Contains(profile))
                    assets.Add(profile);
            }

            return assets;
        }

        static bool ContainsSettingsAssetPath(IReadOnlyCollection<string> paths)
        {
            foreach (var path in paths)
            {
                if (CloudCodeLocalServerSettings.IsSettingsAssetPath(path))
                    return true;
            }

            return false;
        }

        // An element tracks one serialized object at most, so each asset gets its own hidden child.
        sealed class AssetTracker
        {
            readonly VisualElement m_Host;
            readonly System.Action m_OnChanged;
            readonly List<Object> m_Tracked = new();
            readonly List<VisualElement> m_Elements = new();
            readonly HashSet<string> m_Paths = new();

            public AssetTracker(VisualElement host, System.Action onChanged)
            {
                m_Host = host;
                m_OnChanged = onChanged;
            }

            public bool Watches(IReadOnlyCollection<string> paths) => m_Paths.Overlaps(paths);

            public void Retarget(List<Object> assets)
            {
                // Paths are refreshed even for the same assets, since a move keeps the asset but changes its path.
                m_Paths.Clear();
                foreach (var asset in assets)
                    m_Paths.Add(AssetDatabase.GetAssetPath(asset));

                if (IsTracking(assets))
                    return;

                foreach (var element in m_Elements)
                    element.RemoveFromHierarchy();

                m_Elements.Clear();
                m_Tracked.Clear();

                foreach (var asset in assets)
                {
                    var element = new VisualElement { style = { display = DisplayStyle.None } };
                    element.TrackSerializedObjectValue(new SerializedObject(asset), _ => m_OnChanged());
                    m_Host.Add(element);
                    m_Elements.Add(element);
                    m_Tracked.Add(asset);
                }
            }

            bool IsTracking(List<Object> assets)
            {
                if (assets.Count != m_Tracked.Count)
                    return false;

                for (var i = 0; i < assets.Count; i++)
                {
                    if (assets[i] != m_Tracked[i])
                        return false;
                }

                return true;
            }
        }

        // Not GetOrCreate, so drawing the inspector never creates the settings asset.
        static CloudCodeLocalServerSettings LoadServerSettings() => CloudCodeLocalServerSettings.LoadExisting();

        static int GetConfiguredPort()
        {
            var settings = LoadServerSettings();
            return CloudCodeLocalArgument.ToEffectivePort(settings != null ? settings.Port : CloudCodeLocalArgument.k_DefaultPort);
        }

        static List<string> GetLocalInstanceArguments(OrchestratedScenario scenario)
        {
            var arguments = new List<string>();
            foreach (var item in scenario.GetAllInstances())
            {
                if (item is PlayModeControllerItem<LocalPlayerController, LocalInstanceSettings>)
                    arguments.Add(item.GetSettings<LocalInstanceSettings>().Arguments);
            }

            return arguments;
        }
    }
}
