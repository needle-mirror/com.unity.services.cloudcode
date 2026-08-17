#if UNITY_6000_3_OR_NEWER
using Unity.Services.CloudCode.Authoring.Editor.Projects;
using Unity.Services.CloudCode.Editor.Shared.UI;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    class SecretsFileDialogs : ISecretsFileDialogs
    {
        static readonly string k_InvalidFileTypeTitle = L10n.Tr("Invalid File Type");
        static readonly string k_InvalidFileTypeMessage = L10n.Tr("The secrets file must be a .json file. Please select a file with a .json extension.");
        static readonly string k_InvalidJsonTitle = L10n.Tr("Invalid JSON");
        static readonly string k_InvalidJsonMessage = L10n.Tr("The selected file could not be parsed. Please check for syntax errors and try again.");
        static readonly string k_OpenInIde = L10n.Tr("Open in IDE");
        static readonly string k_Ok = L10n.Tr("OK");

        readonly INotifications m_Notifications;
        readonly IDisplayDialog m_DisplayDialog;
        readonly IExternalCodeEditor m_CodeEditor;

        public SecretsFileDialogs(
            INotifications notifications,
            IDisplayDialog displayDialog,
            IExternalCodeEditor codeEditor)
        {
            m_Notifications = notifications;
            m_DisplayDialog = displayDialog;
            m_CodeEditor = codeEditor;
        }

        public void ShowInvalidFileType()
        {
            m_Notifications.DisplayDialog(k_InvalidFileTypeTitle, k_InvalidFileTypeMessage, k_Ok);
        }

        public bool ShowInvalidJson()
        {
            var dismissed = m_DisplayDialog.Show(k_InvalidJsonTitle, k_InvalidJsonMessage, k_Ok, k_OpenInIde);
            return !dismissed;
        }

        public void OpenInIde(TextAsset asset)
        {
            if (asset == null)
            {
                return;
            }

            if (AssetDatabase.OpenAsset(asset))
            {
                return;
            }

            var physicalPath = SecretsFilePaths.GetPhysicalPath(asset);
            if (!string.IsNullOrEmpty(physicalPath))
            {
                m_CodeEditor.OpenProject(physicalPath);
            }
        }
    }
}
#endif
