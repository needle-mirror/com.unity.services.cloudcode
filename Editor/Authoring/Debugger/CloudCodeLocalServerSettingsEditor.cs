#if UNITY_6000_3_OR_NEWER
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    [CustomEditor(typeof(CloudCodeLocalServerSettings))]
    internal class CloudCodeLocalServerSettingsEditor : UnityEditor.Editor
    {
        const string k_SecretsFilePropertyName = "m_SecretsFile";
        const string k_SecretsFileRowName = "debugger-secrets-file-row";
        const string k_SecretsFileFieldName = "debugger-secrets-file-field";
        const string k_SecretsFileButtonName = "debugger-secrets-file-btn";

        static readonly string k_CreateButtonText = L10n.Tr("Create and assign");
        static readonly string k_CreateButtonTooltip = L10n.Tr("Creates and assigns a new JSON file containing secrets information. Values will be placeholders only.");
        static readonly string k_EditButtonText = L10n.Tr("Edit");
        static readonly string k_EditButtonTooltip = L10n.Tr("Opens the assigned secrets file in your external editor.");

        ICloudCodeLocalServer m_LocalServer;
        VisualElement m_Root;
        Button m_SecretsFileButton;

        void OnEnable()
        {
            m_LocalServer = CloudCodeAuthoringServices.Instance
                .GetService<ICloudCodeLocalServer>();

            if (m_LocalServer != null)
            {
                m_LocalServer.OnServerStatusChanged += OnServerStatusChanged;
            }
        }

        void OnDisable()
        {
            if (m_LocalServer != null)
            {
                m_LocalServer.OnServerStatusChanged -= OnServerStatusChanged;
            }
        }

        void OnServerStatusChanged(object _,
            ICloudCodeLocalServer.LocalCloudCodeServerStatus status)
        {
            UpdateFieldsEnabledState(status);
        }

        public override VisualElement CreateInspectorGUI()
        {
            m_Root = new VisualElement();
            var it = serializedObject.GetIterator();

            var inspectChildren = true;
            while (it.NextVisible(inspectChildren))
            {
                inspectChildren = false;
                var isScript = it.name == "m_Script";
                var objectField = new PropertyField(it) { name = $"PropertyField:{it.name}", enabledSelf = !isScript };
                objectField.style.display = isScript ? DisplayStyle.None : DisplayStyle.Flex;
                m_Root.Add(it.name == k_SecretsFilePropertyName
                    ? CreateSecretsFileRow(objectField)
                    : objectField);
            }

            // For the range ports, remove the slider in favor of just showing the text field.
            m_Root.RegisterCallback<GeometryChangedEvent, VisualElement>((_, arg) =>
            {
                var portField = arg.Q<PropertyField>("PropertyField:m_Port");
                var slider = portField?.Q("unity-drag-container");
                if (slider != null)
                {
                    slider.style.display = DisplayStyle.None;
                }
                var textInput = portField?.Q("unity-text-field");
                if (textInput  != null)
                {
                    textInput.style.marginLeft = 0f;
                    textInput.style.flexGrow = 1f;
                }
            }, m_Root);

            UpdateFieldsEnabledState(m_LocalServer?.GetCurrentServerStatus() ??
                ICloudCodeLocalServer.LocalCloudCodeServerStatus.Idle);

            return m_Root;
        }

        VisualElement CreateSecretsFileRow(PropertyField propertyField)
        {
            var row = new VisualElement { name = k_SecretsFileRowName };
            row.AddToClassList(k_SecretsFileRowName);

            propertyField.AddToClassList(k_SecretsFileFieldName);
            row.Add(propertyField);

            m_SecretsFileButton = new Button(OnSecretsFileButtonClicked) { name = k_SecretsFileButtonName };
            m_SecretsFileButton.AddToClassList(k_SecretsFileButtonName);
            row.Add(m_SecretsFileButton);

            // Also fires for the object picker, Undo, and validation reverting an assignment.
            propertyField.RegisterValueChangeCallback(_ => RefreshSecretsFileButton());
            RefreshSecretsFileButton();

            return row;
        }

        void RefreshSecretsFileButton()
        {
            if (m_SecretsFileButton == null)
            {
                return;
            }

            // Read the target rather than the SerializedObject: validation can revert an assignment.
            var isAssigned = ((CloudCodeLocalServerSettings)target).SecretsFile != null;
            m_SecretsFileButton.text = isAssigned ? k_EditButtonText : k_CreateButtonText;
            m_SecretsFileButton.tooltip = isAssigned ? k_EditButtonTooltip : k_CreateButtonTooltip;
        }

        void OnSecretsFileButtonClicked()
        {
            var settings = (CloudCodeLocalServerSettings)target;

            var assigned = settings.SecretsFile;
            if (assigned != null)
            {
                settings.Dialogs?.OpenInIde(assigned);
                return;
            }

            // Create the file next to the settings asset; an unsaved instance has no path to derive from.
            var settingsPath = AssetDatabase.GetAssetPath(settings);
            var created = SecretsFileTemplate.CreateAndImport(
                string.IsNullOrEmpty(settingsPath) ? null : Path.GetDirectoryName(settingsPath));
            if (created == null)
            {
                return;
            }

            // Assign through the SerializedProperty so the bound field repaints and the change is undoable.
            serializedObject.Update();
            serializedObject.FindProperty(k_SecretsFilePropertyName).objectReferenceValue = created;
            serializedObject.ApplyModifiedProperties();

            RefreshSecretsFileButton();
        }

        void UpdateFieldsEnabledState(ICloudCodeLocalServer.LocalCloudCodeServerStatus status)
        {
            if (m_Root == null)
            {
                return;
            }

            var fieldsEnabled = status == ICloudCodeLocalServer.LocalCloudCodeServerStatus.Idle;
            m_Root.tooltip = fieldsEnabled ? string.Empty : "Preferences cannot be changed while the local Cloud Code server is running.";
            m_Root.SetEnabled(fieldsEnabled);
        }
    }
}
#endif
