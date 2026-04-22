#if UNITY_SERVICES_CLOUDCODE_EXPERIMENTAL
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    [CustomEditor(typeof(CloudCodeLocalServerSettings))]
    internal class CloudCodeLocalServerSettingsEditor : UnityEditor.Editor
    {
        ICloudCodeLocalServer m_LocalServer;
        VisualElement m_Root;

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
                m_Root.Add(objectField);
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
