
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Scripts
{
    /// <summary>
    /// Asynchronously monitors a specified log file for new entries, acting as a file tailer.
    /// This class maintains its read state across domain reloads and editor sessions by persisting
    /// the last read byte position. It is also resilient to external file truncations.
    /// </summary>
    class LogMonitor
    {
        readonly IFileSystem m_FileSystem;
        readonly FileInfo m_LogFile;
        readonly LogInfo m_LogInfo;
        readonly string m_MonitorTag;

        FileStream m_FileStream;
        Reader m_StreamReader;

        /// <summary>
        /// Event triggered whenever a new, complete line is appended to the monitored log file.
        /// </summary>
        internal event Action<string> OnLogMessageReceived;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogMonitor"/> class.
        /// </summary>
        /// <param name="fileSystem">Authoring services injected file system wrapper used for IO operations.</param>
        /// <param name="logFile">File descriptor of the log file to be monitored.</param>
        /// <param name="monitorTag">A unique tag identifying this monitor, used for logging and preference keys.</param>
        /// <exception cref="Exception">Throws if the target log file does not exist on disk at the time of initialization.</exception>
        internal LogMonitor(IFileSystem fileSystem, FileInfo logFile, string monitorTag)
        {
            m_FileSystem = fileSystem;
            m_LogFile = logFile;
            m_MonitorTag = monitorTag;
            m_LogInfo = new LogInfo(monitorTag);
        }

        bool DoesLogFileExist()
        {
            return !string.IsNullOrEmpty(m_LogFile?.FullName) && m_FileSystem.FileExists(m_LogFile.FullName);
        }

        /// <summary>
        /// Initiates a continuous, asynchronous polling loop that tails the log file.
        /// </summary>
        /// <param name="pollingInterval">The duration to pause between read attempts when the end of the file is reached.</param>
        /// <param name="cancellationToken">A token used to gracefully terminate the monitoring loop.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        internal async Task StartMonitor(TimeSpan pollingInterval, CancellationToken cancellationToken)
        {
            if (m_FileStream != null || m_StreamReader != null)
            {
                Debug.LogWarning($"[{m_MonitorTag}]: Log Monitor has already been started");
                return;
            }

            try
            {
                await WaitForLogFileAndCreateStreams(cancellationToken);
                while (!cancellationToken.IsCancellationRequested)
                {
                    CorrectFileStreamIfLogFileChanged();
                    var (position, line) = await m_StreamReader!.ReadLineAsync();
                    if (line is not null)
                    {
                        OnLogMessageReceived?.Invoke(line);
                        m_LogInfo.LogFilePosition = position;
                        continue;
                    }

                    await Task.Delay(pollingInterval, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // No-op
            }
            catch (Exception e)
            {
                Debug.LogError($"[{m_MonitorTag}]: Error while tailing log file. {e}");
            }
            finally
            {
                if (cancellationToken.IsCancellationRequested)
                    m_LogInfo.Clear();

                m_StreamReader!.Dispose();
                await m_FileStream!.DisposeAsync();
            }
        }

        async Task WaitForLogFileAndCreateStreams(CancellationToken cancellationToken)
        {
            while (!DoesLogFileExist())
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var fileStream = new FileStream(m_LogFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var soughtPosition = m_LogInfo.LogFilePosition;
            var resultingPosition = fileStream.Seek(m_LogInfo.LogFilePosition, SeekOrigin.Begin);
            if (resultingPosition != soughtPosition)
            {
                m_LogInfo.LogFilePosition = 0L;
                fileStream.Seek(m_LogInfo.LogFilePosition, SeekOrigin.Begin);
                Debug.LogWarning($"[{m_MonitorTag}]: Could not restore log file position, resetting to start of file.");
            }

            m_FileStream = fileStream;
            m_StreamReader = new Reader(m_FileStream);
        }

        void CorrectFileStreamIfLogFileChanged()
        {
            if (m_FileStream == null || m_StreamReader == null)
                return;

            // Note: Filestream itself uses internal buffers and does not reset when the file is cleared.
            // Thus, always determine if we are attempting to seek past the end of the file and autocorrect.
            if (m_FileStream.Length < m_LogInfo.LogFilePosition)
            {
                m_LogInfo.LogFilePosition = 0L;
                m_FileStream.Seek(m_LogInfo.LogFilePosition, SeekOrigin.Begin);
                m_StreamReader.DiscardBuffer();
            }
        }

        class LogInfo
        {
            const string k_EditorPrefsBaseKey = "com.unity.services.cloudcode";
            const string k_EditorPrefsKeySeparator = ".";
            const string k_LogFilePrefix = "logfile";
            const string k_LogFilePositionSuffix = "position";
            const int k_MissingLogFilePosition = 0;

            readonly string m_LogFilePositionPrefKey;
            readonly string m_LogFilePathPrefKey;

            internal LogInfo(string logTag)
            {
                m_LogFilePositionPrefKey = string.Join(k_EditorPrefsKeySeparator,
                    k_EditorPrefsBaseKey,
                    logTag,
                    k_LogFilePrefix,
                    k_LogFilePositionSuffix);
            }

            internal long LogFilePosition
            {
                get => EditorPrefs.GetInt(m_LogFilePositionPrefKey, k_MissingLogFilePosition);
                set => EditorPrefs.SetInt(m_LogFilePositionPrefKey, SafeCastToInt(value));
            }

            static int SafeCastToInt(long value)
            {
                return value switch
                {
                    > int.MaxValue => int.MaxValue,
                    < int.MinValue => int.MinValue,
                    _ => (int)value
                };
            }

            internal void Clear()
            {
                EditorPrefs.DeleteKey(m_LogFilePositionPrefKey);
            }
        }

        class Reader : IDisposable
        {
            readonly StreamReader m_StreamReader;

            public Reader(FileStream stream)
            {
                m_StreamReader = new StreamReader(stream);
            }

            public async Task<(long position, string line)> ReadLineAsync()
            {
                var line = await m_StreamReader.ReadLineAsync();
                return (m_StreamReader.BaseStream.Position, line);
            }

            public void DiscardBuffer()
            {
                m_StreamReader.DiscardBufferedData();
            }

            public void Dispose()
            {
                m_StreamReader?.Dispose();
            }
        }
    }
}

