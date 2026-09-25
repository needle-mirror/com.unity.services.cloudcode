using System;
using System.IO;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.SourceGenerator
{
    class LogWindow : EditorWindow
    {
        const int k_MinFontSize = 8;
        const int k_MaxFontSize = 32;
        const int k_DefaultFontSize = 12;
        const int k_MaxLogLength = 65535;
        const string k_LogFileName = "CloudCodeSourceGenerator.log";

        static readonly TimeSpan k_RefreshInterval = TimeSpan.FromSeconds(2d);
        static readonly string k_RobotoMonoRegularPath = "Fonts/RobotoMono/RobotoMono-Regular.ttf";
        static string LogFilePath
        {
            get
            {
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", k_LogFileName));

                if (!File.Exists(path) || File.ReadAllText(path).Length == 0)
                {
                    path = PathUtils.Join(Path.GetTempPath(), "Logs", k_LogFileName);

                    if (!File.Exists(path))
                    {
                        return string.Empty;
                    }
                }

                return path;
            }
        }

        Label m_LogLabel;
        int m_FontSize = k_DefaultFontSize;
        Label m_PathLabel;

        void CreateGUI()
        {
            // ── Toolbar ──────────────────────────────────────────────────────────
            var toolbar = new Toolbar();
            m_PathLabel = new Label(LogFilePath);
            m_PathLabel.style.flexShrink = 1;
            m_PathLabel.style.overflow = Overflow.Hidden;
            m_PathLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            m_PathLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            m_PathLabel.focusable = true;
            m_PathLabel.selection.isSelectable = true;
            toolbar.Add(m_PathLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            toolbar.Add(new ToolbarButton(RefreshLog) { text = "Refresh" });
            toolbar.Add(new ToolbarButton(ClearLog) { text = "Clear" });
            toolbar.Add(new ToolbarButton(() => ChangeFontSize(1)) { text = "+" });
            toolbar.Add(new ToolbarButton(() => ChangeFontSize(-1)) { text = "-" });

            rootVisualElement.Add(toolbar);

            // ── LogView ──────────────────────────────────────────────────────────
            var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scrollView.style.flexGrow = 1;

            var font = EditorGUIUtility.Load(k_RobotoMonoRegularPath) as Font;

            m_LogLabel = new Label();
            // flexShrink = 0 is required: without it UI Toolkit shrinks the label to fit
            // the viewport, so content never overflows and scrollbars never appear.
            m_LogLabel.style.flexShrink = 0;
            m_LogLabel.style.whiteSpace = WhiteSpace.Pre;
            m_LogLabel.style.unityFontDefinition = new StyleFontDefinition(font);
            m_LogLabel.style.fontSize = m_FontSize;
            m_LogLabel.style.paddingLeft = 4;
            m_LogLabel.style.paddingTop = 2;
            m_LogLabel.focusable = true;
            m_LogLabel.selection.isSelectable = true;

            scrollView.Add(m_LogLabel);
            rootVisualElement.Add(scrollView);

            RefreshLog();

            rootVisualElement.schedule.Execute(RefreshLog).Every((long)k_RefreshInterval.TotalMilliseconds);
        }

        void OnFocus()
        {
            RefreshLog();
        }

        [MenuItem("Services/CloudCode/Source Generator/Log Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<LogWindow>();
            window.titleContent = new GUIContent("Source Generator Logs");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        void RefreshLog()
        {
            if (m_LogLabel == null)
            {
                return;
            }

            var path = LogFilePath;
            var content = string.Empty;

            if (File.Exists(path))
            {
                try
                {
                    using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream);
                    var rawContent = reader.ReadToEnd();
                    if (rawContent.Length > k_MaxLogLength)
                    {
                        rawContent = rawContent.Substring(0, k_MaxLogLength)
                            + "\n\n **** Too Much Log Data.... Open file to view complete log, or Clear. ****";
                    }

                    m_PathLabel.text = path;
                    content = rawContent;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[CC Generator] Error when reading Generator log output {e.Message}");
                }
            }

            m_LogLabel.text = string.IsNullOrEmpty(content) ? "(log is empty)" : content;
        }

        void ChangeFontSize(int delta)
        {
            m_FontSize = Mathf.Clamp(m_FontSize + delta, k_MinFontSize, k_MaxFontSize);
            if (m_LogLabel != null)
            {
                m_LogLabel.style.fontSize = m_FontSize;
            }
        }

        void ClearLog()
        {
            var path = LogFilePath;
            try
            {
                if (File.Exists(path))
                {
                    File.WriteAllText(path, string.Empty);
                }

                if (m_LogLabel != null)
                {
                    m_LogLabel.text = "(log is empty)";
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CC Generator] Could not clear Generator log file: {e.Message}");
            }
        }
    }
}
