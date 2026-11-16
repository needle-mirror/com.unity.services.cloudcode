#if UNITY_6000_3_OR_NEWER
using System;
using System.IO;
using System.Linq;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    static class LocalServerFolders
    {
        // Revealing a folder selects it inside its parent; revealing an entry inside it shows the folder's contents.
        internal static string ModulesRevealTarget(string modulesRoot)
        {
            try
            {
                Directory.CreateDirectory(modulesRoot);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }

            return NewestEntry(modulesRoot) ?? modulesRoot;
        }

        static string NewestEntry(string dir) =>
            new DirectoryInfo(dir).EnumerateFileSystemInfos().OrderByDescending(i => i.LastWriteTimeUtc).FirstOrDefault()?.FullName;
    }
}
#endif
