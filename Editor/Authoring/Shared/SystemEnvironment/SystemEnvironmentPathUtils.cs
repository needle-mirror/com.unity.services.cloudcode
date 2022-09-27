// WARNING: Auto generated code by Starbuck2. Modifications will be lost!
using System.IO;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.SystemEnvironment
{
    static class SystemEnvironmentPathUtils
    {
#if UNITY_EDITOR_WIN
        const string k_PathSeparator = ";";
#else
        const string k_PathSeparator = ":";
#endif
        const string k_Path = "PATH";

        public static bool DoesEnvironmentPathContain(string filePath)
        {
            var path = System.Environment.GetEnvironmentVariable(k_Path);
            if (string.IsNullOrEmpty(path))
                return false;

            var fileDirectory = Path.GetDirectoryName(filePath);
            return path.Contains(fileDirectory ?? string.Empty);
        }

        public static void AddProcessToPath(string processPath)
        {
            var processDirectory = Path.GetDirectoryName(processPath);
            System.Environment.SetEnvironmentVariable(
                k_Path,
                System.Environment.GetEnvironmentVariable(k_Path) + $"{k_PathSeparator}{processDirectory}");
        }
    }
}
