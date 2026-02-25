using System.IO;
using UnityEditor;
using UnityEngine;
using Directory = UnityEngine.Windows.Directory;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    internal class CloudCodeLocalServerSettings : ScriptableObject
    {
        const string k_SettingsProviderPath = "Assets/CloudCode/CloudCodeLocalServerSettings.asset";

        [SerializeField]
        private ushort m_Port = CloudCodeInitializer.k_DefaultLocalCloudCodeServerPort;

        [SerializeField]
        private TextAsset m_SecretsFile;

        private TextAsset m_PreviousSecretsFile;

        static readonly string k_InvalidSecretsFileErrorTitle = L10n.Tr("Invalid secrets file");
        static readonly string k_InvalidSecretsFileErrorDescription = L10n.Tr("Invalid secrets file. Please select a valid JSON file.");
        static readonly string k_Ok = L10n.Tr("OK");

        public ushort Port
        {
            get => m_Port;
            set
            {
                m_Port = value;
                EditorUtility.SetDirty(this);
            }
        }

        public TextAsset SecretsFile
        {
            get => m_SecretsFile;
            set
            {
                m_SecretsFile = value;
                OnValidate();
                EditorUtility.SetDirty(this);
            }
        }

        void OnEnable()
        {
            m_PreviousSecretsFile = m_SecretsFile;
        }

        private void OnValidate()
        {
            if (m_SecretsFile == null)
            {
                m_PreviousSecretsFile = m_SecretsFile;
                return;
            }

            var path = AssetDatabase.GetAssetPath(m_SecretsFile);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".json"))
            {
                m_PreviousSecretsFile = m_SecretsFile;
                return; // everything checks out
            }

            // Else invalid path
            // Display dialog
            EditorApplication.delayCall += () =>
            {
                EditorUtility.DisplayDialog(k_InvalidSecretsFileErrorTitle, k_InvalidSecretsFileErrorDescription, k_Ok);
            };
            m_SecretsFile = m_PreviousSecretsFile;
        }

        public static CloudCodeLocalServerSettings GetOrCreate()
        {
            var dirName = Path.GetDirectoryName(k_SettingsProviderPath);
            if (!Directory.Exists(dirName))
            {
                Directory.CreateDirectory(dirName);
            }
            var provider = AssetDatabase.LoadAssetAtPath<CloudCodeLocalServerSettings>(k_SettingsProviderPath);
            if (provider == null)
            {
                provider = CreateInstance<CloudCodeLocalServerSettings>();
                AssetDatabase.CreateAsset(provider, k_SettingsProviderPath);
            }

            return provider;
        }

        private void Reset()
        {
            Port = CloudCodeInitializer.k_DefaultLocalCloudCodeServerPort;
            SecretsFile = null;
        }
    }
}
