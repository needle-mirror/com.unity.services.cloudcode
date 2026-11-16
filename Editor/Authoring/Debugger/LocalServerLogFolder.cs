#if UNITY_6000_3_OR_NEWER
using System;
using System.IO;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    static class LocalServerLogFolder
    {
        // The server's log file when it has one on disk, else the folder itself (created if missing).
        // false means the folder cannot be created; failure says why.
        internal static bool TryResolveRevealPath(string logFile, string logDir, out string path, out string failure)
        {
            if (!string.IsNullOrEmpty(logFile) && File.Exists(logFile))
            {
                path = logFile;
                failure = null;
                return true;
            }

            try
            {
                path = Directory.CreateDirectory(logDir).FullName;
                failure = null;
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            {
                path = null;
                failure = e.Message;
                return false;
            }
        }
    }
}
#endif
