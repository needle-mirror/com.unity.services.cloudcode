#if UNITY_6000_3_OR_NEWER
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Search;
using Directory = UnityEngine.Windows.Directory;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    internal class CloudCodeLocalServerSettings : ScriptableObject
    {
        const string k_SettingsProviderPath = "Assets/CloudCode/CloudCodeLocalServerSettings.asset";

        // Re-exposed here because the port constant lives in the runtime assembly, which the
        // authoring test assembly cannot see. Tests that restore defaults should use this rather
        // than repeating the number.
        internal const ushort k_DefaultPort = CloudCodeInitializer.k_DefaultLocalCloudCodeServerPort;

        [SerializeField]
        [Range(ushort.MinValue, ushort.MaxValue)]
        [Tooltip("The local port on your machine on which the local Cloud Code server will listen for calls.")]
        private ushort m_Port = CloudCodeInitializer.k_DefaultLocalCloudCodeServerPort;

        [SerializeField]
        [SearchContext("p: ext:json", "asset")]
        [Tooltip("A JSON asset containing key-value secret pairs to be used by your Cloud Code functions when running on the local server.")]
        private TextAsset m_SecretsFile;

        private TextAsset m_PreviousSecretsFile;

        private ISecretsFileDialogs m_Dialogs;

        internal ISecretsFileDialogs Dialogs
        {
            get => m_Dialogs ??= TryResolveDialogs();
            set => m_Dialogs = value;
        }

        static ISecretsFileDialogs TryResolveDialogs()
        {
            try
            {
                return CloudCodeAuthoringServices.Instance.GetService<ISecretsFileDialogs>();
            }
            catch (Exception)
            {
                return null;
            }
        }

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

            var assetPath = AssetDatabase.GetAssetPath(m_SecretsFile);
            var result = SecretsFileValidator.Validate(assetPath, ReadSecretsFile());
            if (result == SecretsFileValidator.Result.Valid)
            {
                m_PreviousSecretsFile = m_SecretsFile;
                return;
            }

            var rejected = m_SecretsFile;
            m_SecretsFile = m_PreviousSecretsFile;

            var dialogs = Dialogs;
            if (dialogs == null)
            {
                return;
            }

            EditorApplication.delayCall += () =>
            {
                if (result == SecretsFileValidator.Result.NotJsonExtension)
                {
                    dialogs.ShowInvalidFileType();
                }
                else if (dialogs.ShowInvalidJson())
                {
                    dialogs.OpenInIde(rejected);
                }
            };
        }

        private string ReadSecretsFile()
        {
            try
            {
                var physicalPath = SecretsFilePaths.GetPhysicalPath(m_SecretsFile);
                return string.IsNullOrEmpty(physicalPath) ? null : File.ReadAllText(physicalPath);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
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
#endif
