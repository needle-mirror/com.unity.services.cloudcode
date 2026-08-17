using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.SourceGenerator
{
    class ManifestWindow : EditorWindow
    {
        const long k_RefreshIntervalMs = 2000;
        static string ManifestFolderPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "CloudModules", "ModuleManifests"));

        readonly List<string> m_Files = new List<string>();
        string m_SelectedPath;
        ListView m_FileList;
        Label m_ContentLabel;
        Label m_FilePathLabel;

        [MenuItem("Services/CloudCode/Source Generator/Manifest Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<ManifestWindow>();
            window.titleContent = new GUIContent("Cloud Behaviours Manifests");
            window.minSize = new Vector2(550, 300);
            window.Show();
        }

        void CreateGUI()
        {
            // ── Toolbar ──────────────────────────────────────────────────────────
            var toolbar = new Toolbar();

            m_FilePathLabel = new Label(ManifestFolderPath);
            m_FilePathLabel.style.flexShrink = 1;
            m_FilePathLabel.style.overflow = Overflow.Hidden;
            m_FilePathLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            m_FilePathLabel.style.color = new Color(0.7f, 0.7f, 0.7f);
            toolbar.Add(m_FilePathLabel);

            var spacer = new VisualElement();
            spacer.style.flexGrow = 1;
            toolbar.Add(spacer);

            toolbar.Add(new ToolbarButton(RefreshFiles) { text = "Refresh" });

            rootVisualElement.Add(toolbar);

            // ── Split view ───────────────────────────────────────────────────────
            var splitView = new TwoPaneSplitView(0, 200, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1;

            // ── Left pane: file list ─────────────────────────────────────────────
            var leftPane = new VisualElement();
            leftPane.style.minWidth = 80;

            m_FileList = new ListView
            {
                itemsSource = m_Files,
                fixedItemHeight = 20,
                makeItem = () =>
                {
                    var label = new Label();
                    label.style.paddingLeft = 6;
                    label.style.unityTextAlign = TextAnchor.MiddleLeft;
                    return label;
                },
                bindItem = (element, index) =>
                {
                    var label = (Label)element;
                    label.text = Path.GetFileNameWithoutExtension(m_Files[index]);
                    label.tooltip = m_Files[index];
                }
            };
            m_FileList.style.flexGrow = 1;
            m_FileList.selectionChanged += OnSelectionChanged;

            leftPane.Add(m_FileList);
            splitView.Add(leftPane);

            // ── Right pane: content viewer ───────────────────────────────────────
            var rightPane = new VisualElement();
            rightPane.style.flexGrow = 1;
            rightPane.style.minWidth = 0;

            var scrollView = new ScrollView(ScrollViewMode.VerticalAndHorizontal);
            scrollView.style.flexGrow = 1;

            m_ContentLabel = new Label("(select a file)");
            m_ContentLabel.style.flexShrink = 0;
            m_ContentLabel.style.whiteSpace = WhiteSpace.Pre;
            m_ContentLabel.style.unityFont = new StyleFont(EditorStyles.miniFont);
            m_ContentLabel.style.paddingLeft = 4;
            m_ContentLabel.style.paddingTop = 2;
            m_ContentLabel.focusable = true;
            m_ContentLabel.selection.isSelectable = true;

            scrollView.Add(m_ContentLabel);
            rightPane.Add(scrollView);
            splitView.Add(rightPane);

            rootVisualElement.Add(splitView);

            RefreshFiles();

            rootVisualElement.schedule.Execute(RefreshFiles).Every(k_RefreshIntervalMs);
        }

        void OnFocus() => RefreshFiles();

        void OnSelectionChanged(IEnumerable<object> items)
        {
            if (items.FirstOrDefault() is string path)
            {
                m_SelectedPath = path;
                LoadFile(path);
            }
        }

        void RefreshFiles()
        {
            if (m_FileList == null)
                return;

            var folder = ManifestFolderPath;
            var newFiles = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.json").OrderBy(Path.GetFileName).ToList()
                : new List<string>();

            // Skip rebuild if the list hasn't changed
            if (newFiles.Count == m_Files.Count && !newFiles.Where((f, i) => f != m_Files[i]).Any())
            {
                // List is the same but reload selected file content in case it changed on disk
                if (m_SelectedPath != null)
                    LoadFile(m_SelectedPath);
                return;
            }

            m_Files.Clear();
            m_Files.AddRange(newFiles);
            m_FileList.Rebuild();

            // Restore the previous selection if the file still exists
            var restoredIndex = m_SelectedPath != null ? m_Files.IndexOf(m_SelectedPath) : -1;
            if (restoredIndex >= 0)
            {
                m_FileList.SetSelection(restoredIndex);
            }
            else
            {
                m_SelectedPath = null;
                if (m_ContentLabel != null)
                    m_ContentLabel.text = m_Files.Count > 0 ? "(select a file)" : "(no files found)";
            }
        }

        void LoadFile(string path)
        {
            if (m_ContentLabel == null)
                return;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                var rawText = reader.ReadToEnd();
                var jToken = JToken.Parse(rawText);
                string prettyJson = jToken.ToString(Formatting.Indented);
                m_ContentLabel.text = prettyJson;
            }
            catch (Exception ex)
            {
                m_ContentLabel.text = $"(error reading file: {ex.Message})";
            }
        }
    }
}
