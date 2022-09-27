using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Projects.Settings
{
    class ProjectSettings : IProjectSettings
    {
        const string k_NodeJsKey = "NodeJsPath";
        const string k_NpmKey = "NpmPath";
        const string k_ExternalEditorPath = "ExternalEditorPath";
        const string k_ExternalEditorArgsFormat = "ExternalEditorArgsFormat";

#if UNITY_EDITOR_WIN
        public string NodeJsPath { get; set; } = @"C:\Program Files\nodejs\node.exe";
        public string NpmPath { get; set; } = @"C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js";
#elif UNITY_EDITOR_LINUX
        public string NodeJsPath { get; set; } = "/usr/bin/node";
        public string NpmPath { get; set; } = "/usr/bin/npm";
#else
        public string NodeJsPath { get; set; } = "/usr/local/bin/node";
        public string NpmPath { get; set; } = "/usr/local/bin/npm";
#endif
        public string ExternalEditorApplicationPath { get; set; }

        public string ExternalEditorArgumentFormat { get; set; }

        public void Load()
        {
            if (EditorPrefs.HasKey(k_NodeJsKey))
            {
                NodeJsPath = EditorPrefs.GetString(k_NodeJsKey);
            }
            else
            {
                EditorPrefs.SetString(k_NodeJsKey, NodeJsPath);
            }

            if (EditorPrefs.HasKey(k_NpmKey))
            {
                NpmPath = EditorPrefs.GetString(k_NpmKey);
            }
            else
            {
                EditorPrefs.SetString(k_NpmKey, NpmPath);
            }

            ExternalEditorApplicationPath = EditorPrefs.GetString(k_ExternalEditorPath, string.Empty);
            ExternalEditorArgumentFormat = EditorPrefs.GetString(k_ExternalEditorArgsFormat, string.Empty);
        }

        public void Save()
        {
            EditorPrefs.SetString(k_NodeJsKey, NodeJsPath);
            EditorPrefs.SetString(k_NpmKey, NpmPath);
            EditorPrefs.SetString(k_ExternalEditorPath, ExternalEditorApplicationPath);
            EditorPrefs.SetString(k_ExternalEditorArgsFormat, ExternalEditorArgumentFormat);
        }
    }
}
