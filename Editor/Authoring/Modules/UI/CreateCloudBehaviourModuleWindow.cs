#if UNITY_6000_5_OR_NEWER
using System;
using System.IO;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    // Boilerplate: same UXML/structure as the behaviour module creator, Cloud Behaviour copy (labels, title).
    internal class CreateCloudBehaviourModuleWindow : EditorWindow
    {
        [SerializeField] ScriptableObject m_UxmlBehaviourCreationWindowAsset;
        [SerializeField] ScriptableObject m_StyleSheetDarkAsset;
        [SerializeField] ScriptableObject m_StyleSheetLightAsset;

        static readonly string k_WindowTitle = L10n.Tr("Cloud Behaviour Module Creator");
        static readonly Vector2 k_WindowSize = new(400, 337);

        internal const string k_CloudModuleInputFieldName = "cloud-module-name-input";
        internal const string k_CloudScriptInputFieldName = "cloud-script-name-input";
        internal const string k_ClientScriptInputFieldName = "client-bindings-input";
        internal const string k_CloudAssemblyInputFieldName = "cloud-assembly-input";
        internal const string k_ClientAssemblyInputFieldName = "client-assembly-input";

        Button m_ConfirmActionButton;
        VisualElement m_Root;
        TextField m_CloudScriptNameInputField;
        TextField m_CloudModuleNameInputField;
        TextElement m_CloudAssemblyNameInputField;
        TextElement m_ClientSideBindings;
        TextElement m_ClientAssemblyNameInputField;
        TextElement m_DirCloudInputField;
        TextElement m_DirClientInputField;
        string m_AssetName;
        string m_AssetPath;

        static readonly string k_WindowHeader = L10n.Tr("Assets to Create (Cloud Behaviour)");
        static readonly string k_WindowSubHeading = L10n.Tr("A Cloud Behaviour script is deployed with a module and server/client assemblies.");
        static readonly string k_CloudTabTitle = L10n.Tr("Cloud");
        static readonly string k_CloudTabHeading = L10n.Tr("Server side Cloud Behaviour and assembly.");
        static readonly string k_CloudTabSubHeading = L10n.Tr("Deploy to Cloud Code via the generated C# module.");
        static readonly string k_ClientTabTitle = L10n.Tr("Client");
        static readonly string k_ClientTabHeading = L10n.Tr("Client side bindings to call the server.");
        static readonly string k_DirectoryTitle = L10n.Tr("Directory");
        static readonly string k_ScriptInputTitle = L10n.Tr("Cloud Behaviour Script");
        static readonly string k_ModuleInputTitle = L10n.Tr("Module");
        static readonly string k_AssemblyInputTitle = L10n.Tr("Assembly Definition");
        static readonly string k_ClientSideBindingsTitle = L10n.Tr("Client Side Bindings");
        static readonly string k_ButtonConfirmText = L10n.Tr("Confirm");
        static readonly string k_ButtonCancelText = L10n.Tr("Cancel");
        static readonly string k_InvalidCharToolTip = L10n.Tr("A file name can't contain any of the following characters: /?<>\\:*|\"");

        internal delegate bool OnSubmitCallback(
            string moduleName,
            string cloudScriptName,
            string clientScriptName,
            string cloudAssemblyName,
            string clientAssemblyName);

        OnSubmitCallback m_OnSubmitForm;

        [InitializeOnLoadMethod]
        static void OnDomainReload()
        {
            if (!HasOpenInstances<CreateCloudBehaviourModuleWindow>())
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                var windows = Resources.FindObjectsOfTypeAll<CreateCloudBehaviourModuleWindow>();
                if (windows == null || windows.Length <= 0)
                {
                    return;
                }

                foreach (var window in windows)
                {
                    window.Close();
                }
            };
        }

        internal static void Show(string assetName, string assetPath, string moduleName, OnSubmitCallback onSubmit)
        {
            CreateCloudBehaviourModuleWindow window = GetWindow<CreateCloudBehaviourModuleWindow>(true, k_WindowTitle);
            window.Initialize(assetName, assetPath, moduleName, onSubmit);
        }

        void Initialize(string assetName, string assetPath, string moduleName, OnSubmitCallback onSubmit)
        {
            minSize = k_WindowSize;
            maxSize = k_WindowSize;
            m_OnSubmitForm = onSubmit;
            m_AssetName = assetName;
            m_AssetPath = assetPath;
            m_CloudModuleNameInputField.value = moduleName;
            m_CloudAssemblyNameInputField.text =  $"{moduleName}.asmdef";
            m_ClientAssemblyNameInputField.text = $"{moduleName}Client.asmdef";
            m_CloudScriptNameInputField.value = assetName;
            m_ClientSideBindings.text = $"{assetName}Client.cs";
            m_CloudScriptNameInputField.tooltip = k_InvalidCharToolTip;
            m_CloudModuleNameInputField.tooltip = k_InvalidCharToolTip;
            RefreshDirectoryPreview(moduleName);
            UpdateConfirmButtonState();
        }

        void UpdateConfirmButtonState()
        {
            var isNameEmpty = m_CloudScriptNameInputField.text.Trim().Length == 0;
            var isModuleEmpty = m_CloudModuleNameInputField.text.Trim().Length == 0;
            m_ConfirmActionButton.enabledSelf = !isNameEmpty && !isModuleEmpty;
        }

        void RefreshDirectoryPreview(string moduleName)
        {
            m_DirCloudInputField.text = GetModulePath(m_AssetName, m_AssetPath, moduleName, true, true);
            m_DirCloudInputField.tooltip = GetModulePath(m_AssetName, m_AssetPath, moduleName, true, false);
            m_DirClientInputField.text = GetModulePath(m_AssetName, m_AssetPath, moduleName, false, true);
            m_DirClientInputField.tooltip = GetModulePath(m_AssetName, m_AssetPath, moduleName, false, false);
        }

        public void CreateGUI()
        {
            var uxmlPath = AssetDatabase.GetAssetPath(m_UxmlBehaviourCreationWindowAsset);
            var styleSheetDarkPath = AssetDatabase.GetAssetPath(m_StyleSheetDarkAsset);
            var styleSheetLightPath = AssetDatabase.GetAssetPath(m_StyleSheetLightAsset);
            if (string.IsNullOrEmpty(uxmlPath) || string.IsNullOrEmpty(styleSheetDarkPath) || string.IsNullOrEmpty(styleSheetLightPath))
            {
                Debug.LogError("Missing UI Assets for Create Cloud Behaviour Module window");
                return;
            }

            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            m_Root = visualTreeAsset.CloneTree();

            var stylesheetPath = EditorGUIUtility.isProSkin ? styleSheetDarkPath : styleSheetLightPath;
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylesheetPath);
            m_Root.styleSheets.Add(stylesheet);

            m_Root.Q<TextElement>("create-info-title").text = k_WindowHeader;
            m_Root.Q<TextElement>("create-info-message").text = k_WindowSubHeading;
            m_Root.Q<Tab>("cloud-tab").label = k_CloudTabTitle;
            m_Root.Q<Tab>("client-tab").label = k_ClientTabTitle;

            CreateClientFormGUI();
            CreateCloudFormGUI();

            m_ConfirmActionButton = m_Root.Q<Button>("confirm-btn");
            m_ConfirmActionButton.text = k_ButtonConfirmText;
            m_ConfirmActionButton.clicked += OnCreateAssetClicked;
            var cancelButton = m_Root.Q<Button>("cancel-btn");
            cancelButton.text = k_ButtonCancelText;
            cancelButton.clicked += Close;

            rootVisualElement.Add(m_Root);
        }

        void CreateCloudFormGUI()
        {
            m_Root.Q<TextElement>("create-cloud-header").text = k_CloudTabHeading;
            m_Root.Q<TextElement>("create-cloud-subheader").text = k_CloudTabSubHeading;
            m_Root.Q<TextElement>("cloud-directory-label").text = k_DirectoryTitle;
            m_Root.Q<VisualElement>("cloud-directory-icon").AddToClassList("creation-window-directory__icon");
            m_DirCloudInputField = m_Root.Q<TextElement>("cloud-directory-input");
            m_Root.Q<TextElement>("cloud-script-name-label").text = k_ScriptInputTitle;
            m_Root.Q<TextElement>("cloud-module-name-label").text = k_ModuleInputTitle;
            m_Root.Q<TextElement>("cloud-assembly-label").text = k_AssemblyInputTitle;
            m_CloudScriptNameInputField = m_Root.Q<TextField>(k_CloudScriptInputFieldName);
            m_CloudModuleNameInputField = m_Root.Q<TextField>(k_CloudModuleInputFieldName);
            m_CloudAssemblyNameInputField = m_Root.Q<TextElement>(k_CloudAssemblyInputFieldName);
            m_CloudScriptNameInputField.RegisterValueChangedCallback((evt) =>
            {
                EnsureValidField(m_CloudScriptNameInputField, evt);
                m_ClientSideBindings.text = $"{m_CloudScriptNameInputField.text}Client.cs";
            });
            m_CloudModuleNameInputField.RegisterValueChangedCallback((evt) =>
            {
                EnsureValidField(m_CloudModuleNameInputField, evt);
                var sanitzedModuleName = m_CloudModuleNameInputField.text;
                m_CloudAssemblyNameInputField.text = $"{sanitzedModuleName}.asmdef";
                m_ClientAssemblyNameInputField.text = $"{sanitzedModuleName}Client.asmdef";
                RefreshDirectoryPreview(sanitzedModuleName);
            });
        }

        void CreateClientFormGUI()
        {
            m_Root.Q<TextElement>("create-client-header").text = k_ClientTabHeading;
            m_Root.Q<TextElement>("client-directory-label").text = k_DirectoryTitle;
            m_Root.Q<VisualElement>("client-directory-icon").AddToClassList("creation-window-directory__icon");
            m_DirClientInputField = m_Root.Q<TextElement>("client-directory-input");

            m_Root.Q<TextElement>("client-bindings-label").text = k_ClientSideBindingsTitle;
            m_Root.Q<TextElement>("client-assembly-label").text = k_AssemblyInputTitle;
            m_ClientSideBindings = m_Root.Q<TextElement>(k_ClientScriptInputFieldName);
            m_ClientAssemblyNameInputField = m_Root.Q<TextElement>(k_ClientAssemblyInputFieldName);
        }

        string GetModulePath(string assetName, string assetPath, string moduleName, bool isCloud, bool shouldTruncate)
        {
            int lastDirIndex = assetPath.LastIndexOf(assetName, StringComparison.Ordinal);
            string basePath = assetPath.Substring(0, lastDirIndex);

            var postFixDir = isCloud ? "Cloud" : "Client";
            var moduleSegment = string.IsNullOrEmpty(moduleName) ? string.Empty : $"{moduleName}/";
            var scriptPath = $"{basePath}{moduleSegment}{postFixDir}";

            if (!shouldTruncate)
            {
                return scriptPath;
            }

            const int visibleCharacters = 16;
            string ellipsisPrefix = PathUtils.Join("Assets", "....");
            int maxVisibleStringLength = ellipsisPrefix.Length + visibleCharacters;
            int pathLen = scriptPath.Length;
            if (pathLen > maxVisibleStringLength)
            {
                var truncatedPath = scriptPath.Substring(pathLen - visibleCharacters, visibleCharacters);
                scriptPath = $"{ellipsisPrefix}{truncatedPath}";
            }

            return scriptPath;
        }

        void EnsureValidField(TextField field, ChangeEvent<string> evt)
        {
            var invalidChars = Path.GetInvalidFileNameChars();
            var textValue = evt.newValue;
            var sanitizedAssetName = new string(textValue !.Where(c => !invalidChars.Contains(c)).ToArray());
            field.value = sanitizedAssetName;
            UpdateConfirmButtonState();
        }

        void OnCreateAssetClicked()
        {
            var moduleName = m_CloudModuleNameInputField.text;
            var cloudScriptName = m_CloudScriptNameInputField.value;
            var clientScriptName = Path.GetFileNameWithoutExtension(m_ClientSideBindings.text);
            var cloudAssemblyName = Path.GetFileNameWithoutExtension(m_CloudAssemblyNameInputField.text);
            var clientAssemblyName = Path.GetFileNameWithoutExtension(m_ClientAssemblyNameInputField.text);

            try
            {
                if (m_OnSubmitForm.Invoke(moduleName, cloudScriptName, clientScriptName, cloudAssemblyName, clientAssemblyName))
                {
                    Close();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Internal Error occurred in Create Cloud Behaviour module: {e.Message}");
            }
        }
    }
}
#endif
