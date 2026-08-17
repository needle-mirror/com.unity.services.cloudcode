#if UNITY_6000_3_OR_NEWER
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    static class SecretsFilePaths
    {
        internal static string GetPhysicalPath(TextAsset asset)
        {
            if (asset == null)
            {
                return null;
            }

            var assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            return Path.GetFullPath(
                FileUtil.GetPhysicalPath(assetPath),
                Path.GetDirectoryName(Application.dataPath));
        }
    }
}
#endif
