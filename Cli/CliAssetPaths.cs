#if UNITY_PIPELINE_AVAILABLE && UNITY_EDITOR
using System;
using System.IO;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Cli
{
    /// <summary>
    /// Path normalization shared by the Cloud Code CLI authoring commands. The CLI takes paths as
    /// free-form strings, so each command first has to turn what a caller typed into a project-relative
    /// asset path with its folders in place, where the creation menus are handed one by the Project window.
    /// </summary>
    static class CliAssetPaths
    {
        /// <summary>Strip characters a script name can't contain.</summary>
        public static string SanitizeName(string name)
        {
            return string.IsNullOrWhiteSpace(name) ? null : ScriptNameSanitizer.Sanitize(name.Trim());
        }

        /// <summary>
        /// Normalize a caller-supplied parent folder into a project-relative Assets/... path,
        /// creating any intermediate folders. Forgiving about the Assets/ prefix and separators.
        /// </summary>
        public static string PrepareFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                path = CloudCodeCliConstants.AssetsRoot;

            path = UnderAssets(path);
            RequireUnderAssets(path);
            CreateFolderRecursive(path);
            return path;
        }

        /// <summary>
        /// Normalize a caller-supplied path into a unique project-relative path with the given
        /// extension, creating any intermediate folders. Forgiving about the Assets/ prefix,
        /// separators and a missing extension.
        /// </summary>
        public static string PrepareAssetPath(string path, string defaultName, string extension)
        {
            if (string.IsNullOrWhiteSpace(path))
                path = defaultName;

            path = UnderAssets(path);

            if (!path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                path += extension;

            RequireUnderAssets(path);

            var folder = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(folder))
                CreateFolderRecursive(folder);

            return AssetDatabase.GenerateUniqueAssetPath(path);
        }

        static string UnderAssets(string path)
        {
            path = path.Replace('\\', '/').Trim().Trim('/');
            if (!path.StartsWith(CloudCodeCliConstants.AssetsRoot + "/", StringComparison.OrdinalIgnoreCase) &&
                !path.Equals(CloudCodeCliConstants.AssetsRoot, StringComparison.OrdinalIgnoreCase))
            {
                path = CloudCodeCliConstants.AssetsRoot + "/" + path;
            }

            return path;
        }

        /// <summary>
        /// Reject a project-relative path that climbs out of the project's Assets folder once its
        /// <c>.</c> and <c>..</c> segments resolve. Commands pass caller-supplied strings straight
        /// through, so without this a <c>..</c> would author files outside the project.
        /// </summary>
        static void RequireUnderAssets(string assetsPath)
        {
            var root = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar, '/');
            var relative = assetsPath.Substring(CloudCodeCliConstants.AssetsRoot.Length).TrimStart('/');
            var resolved = Path.GetFullPath(Path.Combine(root, relative))
                .TrimEnd(Path.DirectorySeparatorChar, '/');

            if (resolved != root &&
                !resolved.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"'{assetsPath}' resolves to a location outside the project's Assets folder.");
            }
        }

        static void CreateFolderRecursive(string assetsFolder)
        {
            if (AssetDatabase.IsValidFolder(assetsFolder))
                return;

            var parent = Path.GetDirectoryName(assetsFolder)?.Replace('\\', '/');
            var name = Path.GetFileName(assetsFolder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
                throw new ArgumentException($"Invalid asset folder path '{assetsFolder}'.");

            if (!AssetDatabase.IsValidFolder(parent))
                CreateFolderRecursive(parent);

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
