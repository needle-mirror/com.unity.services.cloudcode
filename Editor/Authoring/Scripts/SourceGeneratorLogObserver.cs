#if UNITY_6000_5_OR_NEWER

using System;
using System.Collections.Specialized;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Unity.Services.CloudCode.Authoring.Editor.Core.IO;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Services.CloudCode.Authoring.Editor.Scripts
{
    class SourceGeneratorLogObserver
    {
        static readonly string ManifestFolderPath = PathUtils.Join(Path.Combine(Application.dataPath, "../", "Logs", "CloudCodeSourceGenerator.log"));
        const string k_SourceGenErrorRegex = @"^\[\d{2}:\d{2}:\d{2}\.\d{4}\]\[Error\] - \s*(.*)";
        const string k_SourceGeneratorTag = "CloudCodeAuthoring";

        readonly CloudCodeModuleCollection m_CloudCodeModuleCollection;
        readonly IFileSystem m_FileSystem;
        CancellationTokenSource m_CancellationTokenSource;
        LogMonitor m_LogMonitor;

        public SourceGeneratorLogObserver(CloudCodeModuleCollection cloudCodeModuleCollection,
                                          IFileSystem fileSystem)
        {
            m_CloudCodeModuleCollection = cloudCodeModuleCollection;
            m_CloudCodeModuleCollection.CollectionChanged += OnCollectionChanged;
            m_FileSystem = fileSystem;

            RefreshLogObserverStatus();
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshLogObserverStatus();
        }

        void RefreshLogObserverStatus()
        {
            if (HasCloudCodeModulesToObserve())
                TryStartObservingLogs();
            else
                TryStopObservingLogs();
        }

        bool HasCloudCodeModulesToObserve()
        {
            return m_CloudCodeModuleCollection.Count > 0;
        }

        void TryStartObservingLogs()
        {
            if (m_CancellationTokenSource != null)
                return;

            m_CancellationTokenSource = new CancellationTokenSource();
            try
            {
                FileInfo sourceGenFileInfo = new FileInfo(ManifestFolderPath);
                m_LogMonitor = new LogMonitor(m_FileSystem, sourceGenFileInfo, k_SourceGeneratorTag);
                m_LogMonitor.OnLogMessageReceived += rawMessage =>
                {
                    var message = rawMessage.Replace("\\n", "\n").Replace("\\r", "\r");
                    if (IsErrorMessage(message, out var errorMessage))
                    {
                        Debug.LogError($"[{k_SourceGeneratorTag}]: {errorMessage}");
                    }
                };
                _ = m_LogMonitor.StartMonitor(TimeSpan.FromSeconds(1), m_CancellationTokenSource.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"[{k_SourceGeneratorTag}]: Error when Monitoring Source Generator Logs: {e.Message}");
                TryStopObservingLogs();
            }
        }

        bool IsErrorMessage(string message, out string sanitizedMessage)
        {
            Match match = Regex.Match(message, k_SourceGenErrorRegex, RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (match.Success)
            {
                sanitizedMessage = match.Groups[1].Value;
            }
            else
            {
                sanitizedMessage = null;
            }

            return match.Success;
        }

        void TryStopObservingLogs()
        {
            if (m_CancellationTokenSource == null)
                return;

            m_CancellationTokenSource.Cancel();
            m_CancellationTokenSource.Dispose();
            m_CancellationTokenSource = null;
            m_LogMonitor = null;
        }
    }
}

#endif
