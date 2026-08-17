#if UNITY_6000_3_OR_NEWER
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    static class SecretsFileTemplate
    {
        internal const string k_FileName = "localServerSecret.json";

        const string k_DefaultFolder = "Assets";

        // Placeholder values only: the Editor must never write real secret material into the project.
        // Keep it a flat object of string values: the local server reads secrets as Dictionary<string, string>.
        internal const string k_Contents = "{\n  \"SECRET_EXAMPLE\": \"replace-with-your-secret-value\"\n}\n";

        internal static TextAsset CreateAndImport(string folder)
        {
            // Asset paths are forward-slashed, while Path.GetDirectoryName yields backslashes on Windows.
            var directory = string.IsNullOrEmpty(folder)
                ? k_DefaultFolder
                : folder.Replace('\\', '/').TrimEnd('/');

            try
            {
                Directory.CreateDirectory(directory);

                // Returns empty for a path the AssetDatabase cannot own, such as one outside the project.
                var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{k_FileName}");
                if (string.IsNullOrEmpty(assetPath))
                {
                    Debug.LogError($"\"{directory}\" is not a valid folder for a secrets file.");
                    return null;
                }

                File.WriteAllText(assetPath, k_Contents);
                AssetDatabase.ImportAsset(assetPath);

                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
                if (asset == null)
                {
                    Debug.LogError($"Failed to import the secrets file created at \"{assetPath}\".");
                }

                return asset;
            }
            catch (IOException e)
            {
                Debug.LogError($"Failed to create a secrets file in \"{directory}\": {e.Message}");
                return null;
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogError($"Failed to create a secrets file in \"{directory}\": {e.Message}");
                return null;
            }
        }
    }
}
#endif
