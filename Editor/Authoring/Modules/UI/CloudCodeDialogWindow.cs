#if UNITY_6000_5_OR_NEWER
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    /// <summary>
    /// Icon shown to the left of a <see cref="CloudCodeDialogWindow"/> heading.
    /// </summary>
    enum DialogIcon
    {
        None,
        Info,
        Warning,
        Error,
    }

    /// <summary>
    /// General-purpose modal dialog for Cloud Code authoring: a heading, a body, an optional documentation
    /// link, and an OK button.
    /// </summary>
    class CloudCodeDialogWindow : EditorWindow
    {
        const float k_Width = 460f;
        const float k_Padding = 10f;
        const float k_IconColumn = 20f; // icon + its right margin
        const float k_Gap = 10f; // vertical space between blocks
        static readonly string k_DefaultOk = L10n.Tr("OK");
        static readonly string k_ViewDocumentation = L10n.Tr("View Documentation");
        static readonly string k_StyleSheetPath = PathUtils.Join(
            CloudCodePackage.EditorPath, "Authoring", "Modules", "UI", "Assets", "CloudCodeDialogWindow.uss");

        string m_Heading;
        string m_Body;
        string m_DocumentationUrl;
        string m_OkLabel;
        DialogIcon m_IconKind;
        Image m_Icon;
        Label m_HeadingLabel;

        /// <summary>
        /// Shows a modal dialog centered on the main editor window.
        /// </summary>
        /// <param name="windowTitle">Text shown in the window's title bar.</param>
        /// <param name="heading">Bold heading shown at the top of the body.</param>
        /// <param name="body">Body text shown below the heading.</param>
        /// <param name="icon">Icon shown beside the heading, or <see cref="DialogIcon.None"/> for no icon.</param>
        /// <param name="documentationUrl">Optional URL; when set, a "View Documentation" link is shown.</param>
        /// <param name="okLabel">Label for the dismiss button; defaults to "OK".</param>
        internal static void Show(
            string windowTitle,
            string heading,
            string body,
            DialogIcon icon = DialogIcon.Warning,
            string documentationUrl = null,
            string okLabel = null)
        {
            var window = CreateInstance<CloudCodeDialogWindow>();
            window.m_Heading = heading;
            window.m_Body = body;
            window.m_DocumentationUrl = documentationUrl;
            window.m_IconKind = icon;
            window.m_OkLabel = string.IsNullOrEmpty(okLabel) ? k_DefaultOk : okLabel;
            window.titleContent = new GUIContent(windowTitle);

            var height = CalculateHeight(heading, body, documentationUrl, icon);
            var size = new Vector2(k_Width, height);
            window.minSize = size;
            window.maxSize = size;

            var main = EditorGUIUtility.GetMainWindowPosition();
            window.position = new Rect(
                main.center.x - (k_Width / 2f),
                main.center.y - (height / 2f),
                k_Width, height);

            window.ShowModalUtility();
        }

        static float CalculateHeight(string heading, string body, string documentationUrl, DialogIcon icon)
        {
            var contentWidth = k_Width - (k_Padding * 2f);
            var iconColumn = icon == DialogIcon.None ? 0f : k_IconColumn;
            var headingHeight = EditorStyles.boldLabel.CalcHeight(
                new GUIContent(heading), contentWidth - iconColumn);
            var bodyHeight = EditorStyles.wordWrappedLabel.CalcHeight(
                new GUIContent(body), contentWidth);
            var linkHeight = string.IsNullOrEmpty(documentationUrl)
                ? 0f
                : (k_Gap + 20f);
            var buttonsHeight = k_Gap + 20f; // marginTop + button

            return Mathf.Ceil((k_Padding * 2f) + headingHeight + k_Gap
                + bodyHeight + linkHeight + buttonsHeight);
        }

        void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = k_Padding;
            root.style.paddingBottom = k_Padding;
            root.style.paddingLeft = k_Padding;
            root.style.paddingRight = k_Padding;
            root.AddToClassList("cc-dialog");
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StyleSheetPath);
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            var headingRow = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center }
            };
            if (m_IconKind != DialogIcon.None)
            {
                m_Icon = new Image
                {
                    image = EditorGUIUtility.IconContent(IconName(m_IconKind)).image,
                    scaleMode = ScaleMode.ScaleToFit,
                    style = { width = 14, height = 14, marginRight = 6, flexShrink = 0 }
                };
                headingRow.Add(m_Icon);
            }
            m_HeadingLabel = new Label(m_Heading)
            {
                style = { whiteSpace = WhiteSpace.Normal, unityFontStyleAndWeight = FontStyle.Bold, flexShrink = 1 }
            };
            headingRow.Add(m_HeadingLabel);
            root.Add(headingRow);

            var body = new Label(m_Body)
            {
                style = { whiteSpace = WhiteSpace.Normal, marginTop = k_Gap }
            };
            root.Add(body);

            if (!string.IsNullOrEmpty(m_DocumentationUrl))
            {
                var link = new Button(() => Application.OpenURL(m_DocumentationUrl))
                {
                    text = k_ViewDocumentation
                };
                link.AddToClassList("cc-dialog__link");
                link.style.marginTop = k_Gap;
                root.Add(link);
            }

            var buttonRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd,
                    marginTop = k_Gap
                }
            };
            var ok = new Button(Close) { text = m_OkLabel };
            ok.AddToClassList("cc-dialog__ok-button");
            buttonRow.Add(ok);
            root.Add(buttonRow);

            // Match the icon to the heading's font size.
            if (m_Icon != null)
                root.RegisterCallback<GeometryChangedEvent>(SizeIconToHeading);
        }

        static string IconName(DialogIcon icon)
        {
            switch (icon)
            {
                case DialogIcon.Info:
                    return "console.infoicon";
                case DialogIcon.Error:
                    return "console.erroricon";
                default:
                    return "console.warnicon";
            }
        }

        void SizeIconToHeading(GeometryChangedEvent evt)
        {
            var fontSize = m_HeadingLabel.resolvedStyle.fontSize;
            if (fontSize <= 0f)
                return;

            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(SizeIconToHeading);
            m_Icon.style.width = fontSize;
            m_Icon.style.height = fontSize;
        }
    }
}
#endif
