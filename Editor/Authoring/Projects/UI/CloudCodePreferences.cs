using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Package;
using UnityEditor;
using UnityEngine;
using Unity.Services.CloudCode.Authoring.Editor.Projects.Settings;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Assets;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Infrastructure.IO;
using Unity.Services.CloudCode.Authoring.Editor.Shared.UI;
using UnityEngine.UIElements;

using Logger = Unity.Services.CloudCode.Authoring.Editor.Shared.Logging.Logger;

namespace Unity.Services.CloudCode.Authoring.Editor.Projects.UI
{
    class CloudCodePreferences : SettingsProvider
    {
        static readonly string k_Revert = L10n.Tr("Revert");
        static readonly string k_Apply = L10n.Tr("Apply");
        static readonly string k_NodeJsPathLabel = L10n.Tr("NodeJS Path");
        static readonly string k_NpmPathLabel = L10n.Tr("NPM Path");
        static readonly string k_ExternalEditorLabel = L10n.Tr("Application Path");
        static readonly string k_ExternalEditorFormatLabel = L10n.Tr("Editor Arguments");
        static readonly string k_JsProjectInfo = L10n.Tr("Configures your Unity project as a NodeJS project."
            + " Setting up your NodeJS project enables tooling such as autocomplete and in-script parameters.");
        static readonly string k_JsEditorInfo = L10n.Tr("Set and configure the script editor used when opening Javascript files.");

        internal static readonly string PackageName = L10n.Tr("com.unity.services.cloudcode");
        internal static readonly string VersionMismatchWarning = L10n.Tr("NPM was initialized by a package version that does not match you installed package version."
            + "You can update your version through 'Preferences > Cloud Code > Initialize JS Project'");
        static readonly string k_InitializeNmpTitle = L10n.Tr("Initialize Npm");
        static readonly string k_InitializingNmpMessage = L10n.Tr("Initializing npm");
        static readonly string k_InstallingPackages = L10n.Tr("Installing packages");
        static readonly string k_JavascriptEnv = L10n.Tr("Javascript development environment");
        static readonly string k_InitButtonText = L10n.Tr("Initialize JS project");
        static readonly string k_JavascriptEditor = L10n.Tr("Javascript editor");
        static readonly string k_ChooseFile = L10n.Tr("Choose File...");
        static readonly string k_ChooseExternalEditor = L10n.Tr("Choose external editor");
        static readonly string k_SupportedLaunchArguments = L10n.Tr("Supported launch arguments:");

#if UNITY_EDITOR_OSX || UNITY_EDITOR_WIN
        static readonly string k_ExternalEditorDefaultDirectory =
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles);
#else
        static readonly string k_ExternalEditorDefaultDirectory = string.Empty;
#endif

        ProjectSettings m_ProjectSettings;
        bool m_Dirty;
        GUIStyle m_Heading1;

        internal CloudCodePreferences() : base("Preferences/Cloud Code", SettingsScope.User)
        {
            activateHandler = OnActivated;
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            return new CloudCodePreferences();
        }

        public static IProjectSettings LoadProjectSettings()
        {
            var settings = new ProjectSettings();
            settings.Load();
            return settings;
        }

        [InitializeOnLoadMethod]
        static async Task OnValidateNodeProject()
        {
            if (!CloudCodeProject.IsInitialized())
            {
                return;
            }

            var packageVersionProvider = CloudCodeAuthoringServices.Instance.GetService<IPackageVersionProvider>();
            var notifications = CloudCodeAuthoringServices.Instance.GetService<INotifications>();
            var npm = CloudCodeAuthoringServices.Instance.GetService<NodePackageManager>();
            await ValidateNodeProject(CloudCodeProject.OpenDefault(), packageVersionProvider, notifications, npm);
        }

        static async Task InitializeNpm(
            INodePackageManager npm,
            IPackageVersionProvider packageVersionProvider,
            INotifications notifications)
        {
            using var progressBar = notifications.ProgressBar(k_InitializeNmpTitle, k_InitializingNmpMessage, 2);

            await npm.Init();

            progressBar.OperationInfo = k_InstallingPackages;
            progressBar.CompleteStep();

            await UpdateNpmProject(CloudCodeProject.OpenDefault(), npm, packageVersionProvider, progressBar);
        }

        internal static async Task UpdateNpmProject(
            CloudCodeProject project,
            INodePackageManager npm,
            IPackageVersionProvider packageVersionProvider,
            IProgressBar progressBar)
        {
            var packageVersion = await packageVersionProvider.GetPackageVersionAsync(PackageName);

            project.AddDependencies();
            project.AddPackageVersion(packageVersion);
            project.Save();

            await npm.Install();
            progressBar.CompleteStep();

            using var assets = new ObservableAssets<CloudCodeScript>();
            foreach (var jsScript in assets)
            {
                AssetDatabase.ImportAsset(PathUtils.GetRelativePath(".", jsScript.Path), ImportAssetOptions.ForceUpdate);
            }
        }

        internal static async Task ValidateNodeProject(CloudCodeProject project,
            IPackageVersionProvider packageVersionProvider,
            INotifications notifications,
            INodePackageManager npm)
        {
            var packageVersion = await packageVersionProvider.GetPackageVersionAsync(PackageName);

            if (packageVersion != project.PackageVersion)
            {
                Logger.LogWarning(VersionMismatchWarning);
            }
        }

        void OnActivated(string searchContext, VisualElement visualElement)
        {
            m_ProjectSettings = new ProjectSettings();
            m_ProjectSettings.Load();

            m_Heading1 = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 16
            };
        }

        public override void OnGUI(string searchContext)
        {
            base.OnGUI(searchContext);

            var style = new GUIStyle()
            {
                margin = new RectOffset(6, 0, 0, 0)
            };
            EditorGUILayout.BeginVertical(style);
            DrawVerticalSettingsArea();
            EditorGUILayout.EndVertical();
        }

        void DrawVerticalSettingsArea()
        {
            EditorGUILayout.Space();
            DrawJsDevEnvironment();
            GUILayout.Space(24);
            DrawExternalEditor();
            ApplyButtons();
        }

        void DrawJsDevEnvironment()
        {
            EditorGUILayout.LabelField(k_JavascriptEnv, m_Heading1);
            EditorGUILayout.Space();
            m_ProjectSettings.NodeJsPath = CheckChange(
                EditorGUILayout.TextField(k_NodeJsPathLabel, m_ProjectSettings.NodeJsPath),
                m_ProjectSettings.NodeJsPath);
            m_ProjectSettings.NpmPath = CheckChange(
                EditorGUILayout.TextField(k_NpmPathLabel, m_ProjectSettings.NpmPath),
                m_ProjectSettings.NpmPath);

            EditorGUILayout.Space();

            DrawInitJsProject();

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(k_JsProjectInfo, MessageType.Info);
        }

        /// <summary>
        /// Async void required for use with OnGUI.
        /// Should remove need for async void with UITK implementation.
        /// </summary>
        static async void DrawInitJsProject()
        {
            if (GUILayout.Button(k_InitButtonText, GUILayout.ExpandWidth(false)))
            {
                var npm = CloudCodeAuthoringServices.Instance.GetService<NodePackageManager>();
                var packageVersionProvider = CloudCodeAuthoringServices.Instance.GetService<IPackageVersionProvider>();
                var notifications = CloudCodeAuthoringServices.Instance.GetService<INotifications>();

                await InitializeNpm(npm, packageVersionProvider, notifications);
            }
        }

        void DrawExternalEditor()
        {
            EditorGUILayout.LabelField(k_JavascriptEditor, m_Heading1);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(k_JsEditorInfo);
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                m_ProjectSettings.ExternalEditorApplicationPath = CheckChange(
                    EditorGUILayout.TextField(
                        k_ExternalEditorLabel,
                        m_ProjectSettings.ExternalEditorApplicationPath,
                        GUILayout.ExpandWidth(true)),
                    m_ProjectSettings.ExternalEditorApplicationPath);

                if (GUILayout.Button(k_ChooseFile, GUILayout.ExpandWidth(false)))
                {
                    var applicationPath = EditorUtility.OpenFilePanel(
                        k_ChooseExternalEditor,
                        k_ExternalEditorDefaultDirectory,
                        string.Empty);

                    if (!string.IsNullOrEmpty(applicationPath))
                        m_ProjectSettings.ExternalEditorApplicationPath = CheckChange(applicationPath, m_ProjectSettings.ExternalEditorApplicationPath);
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                m_ProjectSettings.ExternalEditorArgumentFormat = CheckChange(
                    EditorGUILayout.TextField(
                        k_ExternalEditorFormatLabel,
                        m_ProjectSettings.ExternalEditorArgumentFormat,
                        GUILayout.ExpandWidth(true)),
                    m_ProjectSettings.ExternalEditorArgumentFormat);
            }

            var args = JsAssetHandler
                .SupportedArguments
                .Select(kvp => $"{kvp.Key}").ToList();
            args.Insert(0, k_SupportedLaunchArguments);
            var argsString = string.Join("\n  ", args);

            EditorGUILayout.Space();

            EditorGUILayout.HelpBox(argsString, MessageType.Info);
        }

        void ApplyButtons()
        {
            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginHorizontal();
            DrawApplyRevertButtons();
            EditorGUILayout.EndHorizontal();
        }

        void DrawApplyRevertButtons()
        {
            GUI.enabled = m_Dirty;
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(k_Revert))
            {
                GUI.FocusControl(null);
                m_ProjectSettings.Load();
                m_Dirty = false;
            }

            if (GUILayout.Button(k_Apply))
            {
                m_ProjectSettings.Save();
                m_Dirty = false;
            }
            GUI.enabled = true;
        }

        string CheckChange(string result, string input)
        {
            if (!m_Dirty && result != input)
            {
                m_Dirty = true;
            }
            return result;
        }
    }
}
