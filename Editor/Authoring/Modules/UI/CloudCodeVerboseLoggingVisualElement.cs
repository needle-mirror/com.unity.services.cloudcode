using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Logging;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    class CloudCodeVerboseLoggingVisualElement : VisualElement
    {
        const string k_RowName = "verbose-logging-row";
        const string k_ToggleName = "verbose-logging-toggle";

        // The Modules Binding row's own layout, so the checkbox lands on the same x as its button:
        // a 50%-wide label container, then the control nudged 7px right of it.
        const float k_RowMarginLeft = 10f;
        const float k_LabelWidthPercent = 50f;
        const float k_ControlOffset = 7f;

        static readonly string k_ToggleText = L10n.Tr("Verbose Logging");
        static readonly string k_ToggleTooltip = L10n.Tr("Log detailed Cloud Code activity to the Console, including the local server's own operation. Changing this sets a scripting define and recompiles; a running local server keeps its current verbosity until restarted.");
        static readonly string k_AllServicesTooltip = string.Format(
            L10n.Tr("Verbose logging is enabled for all Unity Services by the {0} scripting define."),
            VerboseLogging.k_ServicesDefine);

        readonly VisualElement m_Row;
        readonly Toggle m_Toggle;

        public CloudCodeVerboseLoggingVisualElement()
        {
            m_Row = new VisualElement { name = k_RowName };
            m_Row.style.flexDirection = FlexDirection.Row;
            m_Row.style.marginLeft = k_RowMarginLeft;

            var labelContainer = new VisualElement();
            labelContainer.style.flexDirection = FlexDirection.Row;
            labelContainer.style.width = Length.Percent(k_LabelWidthPercent);
            labelContainer.Add(new Label(k_ToggleText));
            m_Row.Add(labelContainer);

            // A bare Toggle: its own label would bring BaseField margins with it and shift the
            // checkbox off the column the button sits in.
            m_Toggle = new Toggle { name = k_ToggleName };
            m_Toggle.style.marginLeft = 0f;
            m_Toggle.style.left = k_ControlOffset;
            m_Toggle.RegisterValueChangedCallback(evt => SetVerboseLogging(evt.newValue));
            // The input and its checkmark carry their own margins in the default theme, which would
            // push the box right of the button's edge.
            foreach (var className in new[] { Toggle.inputUssClassName, Toggle.checkmarkUssClassName })
            {
                var element = m_Toggle.Q(className: className);
                if (element != null)
                {
                    element.style.marginLeft = 0f;
                }
            }
            m_Row.Add(m_Toggle);

            // The defines can change while the settings window is closed, or with the active build
            // target group.
            RegisterCallback<AttachToPanelEvent>(_ => Refresh());

            Add(m_Row);
            Refresh();
        }

        void Refresh()
        {
            var defines = GetDefines();

            // Cloud Code only ever writes its own define, so the services-wide one cannot be turned
            // off from here; show it as on and locked rather than as a toggle that does nothing.
            var enabledForAllServices = VerboseLogging.IsEnabledForAllServices(defines);
            m_Toggle.SetValueWithoutNotify(VerboseLogging.IsEnabled(defines));
            // On the row, so the label greys out with the checkbox and carries the tooltip too.
            m_Row.SetEnabled(!enabledForAllServices);
            m_Row.tooltip = enabledForAllServices ? k_AllServicesTooltip : k_ToggleTooltip;
        }

        static void SetVerboseLogging(bool enabled)
        {
            var target = CurrentBuildTarget();
            var defines = VerboseLogging.SetEnabled(GetDefines(), enabled);
            // Triggers a recompile, and with it a domain reload that rebuilds this element.
            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
        }

        static string[] GetDefines() =>
            PlayerSettings.GetScriptingDefineSymbols(CurrentBuildTarget())
                .Split(';')
                .Where(define => !string.IsNullOrEmpty(define))
                .ToArray();

        static NamedBuildTarget CurrentBuildTarget() =>
            NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
    }
}
