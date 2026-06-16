using System;
using System.IO;
using System.Linq;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    internal class CreateCloudCodeModuleWindow : EditorWindow
    {
        [SerializeField] ScriptableObject m_UxmlCloudCodeCreationWindowAsset;
        [SerializeField] ScriptableObject m_StyleSheetDarkAsset;
        [SerializeField] ScriptableObject m_StyleSheetLightAsset;

        static readonly string k_WindowTitle = L10n.Tr("Cloud Code Module Creator");
        static readonly Vector2 k_WindowSize = new(400, 337);

        internal const string k_CloudModuleInputFieldName = "cloud-module-name-input";
        internal const string k_CloudScriptInputFieldName = "cloud-script-name-input";
        internal const string k_ClientScriptInputFieldName = "client-bindings-input";
        internal const string k_CloudAssemblyInputFieldName = "cloud-assembly-input";
        internal const string k_ClientAssemblyInputFieldName = "client-assembly-input";

        // UI Controls within this window
        Button m_ConfirmActionButton;
        VisualElement m_Root;
        TextField m_CloudScriptNameInputField;
        TextField m_CloudModuleNameInputField;
        TextElement m_CloudAssemblyNameInputField;
        TextElement m_ClientSideBindings;
        TextElement m_ClientAssemblyNameInputField;
        TextElement m_DirCloudInputField;
        TextElement m_DirClientInputField;

        // Const for all string localizations
        static readonly string k_WindowHeader = L10n.Tr("Assets to Create");
        static readonly string k_WindowSubHeading = L10n.Tr("A Cloud Code Script requires a Cloud Code Module to deploy and an Assembly representing your server code.");
        static readonly string k_CloudTabTitle = L10n.Tr("Cloud");
        static readonly string k_CloudTabHeading = L10n.Tr("Server side setup that creates a Cloud Code Script and its assembly representing your server code.");
        static readonly string k_CloudTabSubHeading = L10n.Tr("This can be deployed onto Cloud Code servers via the created Cloud C# Module.");
        static readonly string k_ClientTabTitle = L10n.Tr("Client");
        static readonly string k_ClientTabHeading = L10n.Tr("Client side bindings are generated to call the server side functions.");
        static readonly string k_DirectoryTitle = L10n.Tr("Directory");
        static readonly string k_ScriptInputTitle = L10n.Tr("Cloud Code Script");
        static readonly string k_ModuleInputTitle = L10n.Tr("Cloud Code C# Module");
        static readonly string k_AssemblyInputTitle = L10n.Tr("Assembly Definition");
        static readonly string k_ClientSideBidingsTitle = L10n.Tr("Client Side Bindings");
        static readonly string k_ButtonConfirmText = L10n.Tr("Confirm");
        static readonly string k_ButtonCancelText = L10n.Tr("Cancel");
        static readonly string k_InvalidCharToolTip =
            L10n.Tr("A file name can't contain any of the following characters: /?<>\\:*|\"");

        internal delegate bool OnSubmitCallback(string moduleName, string cloudScriptName, string clientScriptName,
            string cloudAssemblyName, string clientAssemblyName);

        OnSubmitCallback m_OnSubmitForm;

        [InitializeOnLoadMethod]
        static void OnDomainReload()
        {
            // The Editor window does not survive across a domain reload.
            // All Text Fields and callbacks are wiped out and it is not possible to restore them.
            // However, a triggered Domain reload usually signifies that the User is actively working on
            // something else that is unrelated, and as such we close this window to mitigate data loss.
            if (!HasOpenInstances<CreateCloudCodeModuleWindow>())
                return;

            EditorApplication.delayCall += () =>
            {
                var windows = Resources.FindObjectsOfTypeAll<CreateCloudCodeModuleWindow>();
                if (windows == null || windows.Length <= 0)
                    return;

                foreach (var window in windows)
                    window.Close();
            };
        }

        internal static void Show(string assetName, string assetPath, string moduleName, OnSubmitCallback onSubmit)
        {
            CreateCloudCodeModuleWindow window = GetWindow<CreateCloudCodeModuleWindow>(true, k_WindowTitle);
            window.Initialize(assetName, assetPath, moduleName, onSubmit);
        }

        void Initialize(string assetName, string assetPath, string moduleName, OnSubmitCallback onSubmit)
        {
            minSize = k_WindowSize;
            maxSize = k_WindowSize;
            m_OnSubmitForm = onSubmit;
            m_CloudModuleNameInputField.value = moduleName;
            m_CloudAssemblyNameInputField.text =  $"{moduleName}.asmdef";;
            m_ClientAssemblyNameInputField.text = $"{moduleName}Client.asmdef";;
            m_CloudScriptNameInputField.value = assetName;
            m_ClientSideBindings.text = $"{assetName}Client.cs";
            m_CloudScriptNameInputField.tooltip = k_InvalidCharToolTip;
            m_CloudModuleNameInputField.tooltip = k_InvalidCharToolTip;
            m_DirCloudInputField.text = GetModulePath(assetName, assetPath, true, true);
            m_DirCloudInputField.tooltip = GetModulePath(assetName, assetPath, true, false);
            m_DirClientInputField.text = GetModulePath(assetName, assetPath, false, true);
            m_DirClientInputField.tooltip = GetModulePath(assetName, assetPath, false, false);
        }

        public void CreateGUI()
        {
            var uxmlPath = AssetDatabase.GetAssetPath(m_UxmlCloudCodeCreationWindowAsset);
            var styleSheetDarkPath = AssetDatabase.GetAssetPath(m_StyleSheetDarkAsset);
            var styleSheetLightPath = AssetDatabase.GetAssetPath(m_StyleSheetLightAsset);
            if (string.IsNullOrEmpty(uxmlPath) || string.IsNullOrEmpty(styleSheetDarkPath) || string.IsNullOrEmpty(styleSheetLightPath))
            {
                Debug.LogError("Missing UI Assets for Create Cloud Code Module window");
                return;
            }

            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            m_Root = visualTreeAsset.CloneTree();

            // Apply light or dark theme css
            var stylesheetPath = EditorGUIUtility.isProSkin ? styleSheetDarkPath : styleSheetLightPath;
            var stylesheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(stylesheetPath);
            m_Root.styleSheets.Add(stylesheet);

            // Create and bind Header elements
            m_Root.Q<TextElement>("create-info-title").text = k_WindowHeader;
            m_Root.Q<TextElement>("create-info-message").text = k_WindowSubHeading;

            // Inflate tabs and both cloud + client forms
            m_Root.Q<Tab>("cloud-tab").label = k_CloudTabTitle;
            m_Root.Q<Tab>("client-tab").label = k_ClientTabTitle;
            CreateCloudFormGUI();
            CreateClientFormGUI();

            // Create and bind the bottom elements.
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
            });
        }

        void CreateClientFormGUI()
        {
            m_Root.Q<TextElement>("create-client-header").text = k_ClientTabHeading;
            m_Root.Q<TextElement>("client-directory-label").text = k_DirectoryTitle;
            m_Root.Q<VisualElement>("client-directory-icon").AddToClassList("creation-window-directory__icon");
            m_DirClientInputField = m_Root.Q<TextElement>("client-directory-input");

            m_Root.Q<TextElement>("client-bindings-label").text = k_ClientSideBidingsTitle;
            m_Root.Q<TextElement>("client-assembly-label").text = k_AssemblyInputTitle;
            m_ClientSideBindings = m_Root.Q<TextElement>(k_ClientScriptInputFieldName);
            m_ClientAssemblyNameInputField = m_Root.Q<TextElement>(k_ClientAssemblyInputFieldName);
        }

        string GetModulePath(string assetName, string assetPath, bool isCloud, bool shouldTruncate)
        {
            int lastDirIndex = assetPath.LastIndexOf(assetName, StringComparison.Ordinal);
            string scriptPath = assetPath.Substring(0, lastDirIndex);

            var postFixDir = isCloud ? "Cloud" : "Client";
            scriptPath = $"{scriptPath}{postFixDir}";

            if (!shouldTruncate)
                return scriptPath;

            const int visibleCharacters = 16;
            string basePath = PathUtils.Join("Assets", "....");
            int maxVisibleStringLength = basePath.Length + visibleCharacters;
            int pathLen = scriptPath.Length;

            if (pathLen > maxVisibleStringLength)
            {
                var truncatedPath = scriptPath.Substring(pathLen - visibleCharacters, visibleCharacters);
                scriptPath = $"{basePath}{truncatedPath}";
            }

            return scriptPath;
        }

        void EnsureValidField(TextField field, ChangeEvent<string> evt)
        {
            // Next check for invalid names.
            var invalidChars = Path.GetInvalidFileNameChars();
            var textValue = evt.newValue;
            var sanitizedAssetName = new string(textValue !.Where(c => !invalidChars.Contains(c)).ToArray());

            field.value = sanitizedAssetName;

            // Disable the confirm button if fields are empty.
            var isNameEmpty = m_CloudScriptNameInputField.text.Trim().Length == 0;
            var isModuleEmpty = m_CloudModuleNameInputField.text.Trim().Length == 0;
            m_ConfirmActionButton.enabledSelf = !isNameEmpty && !isModuleEmpty;
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
                    Close();
            }
            catch (Exception e)
            {
                Debug.LogError($"Internal Error occured in Create Cloud Code module: {e.Message}");
            }
        }
    }
}
