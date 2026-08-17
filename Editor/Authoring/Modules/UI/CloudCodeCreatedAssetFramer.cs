#if UNITY_6000_5_OR_NEWER
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    static class CloudCodeCreatedAssetFramer
    {
        // Module creation writes asmdefs, which queues a recompile + domain reload. Defer framing to
        // the next editor tick: it runs before the reload and moves the Project window to the new
        // script's folder, which the reload then restores. Framing inline (mid-import) doesn't stick.
        internal static void FrameAfterReload(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;

            EditorApplication.delayCall += () => Frame(assetPath);
        }

        internal static void Frame(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null)
                return;

            Selection.activeObject = asset;
            EditorUtility.FocusProjectWindow();
            EditorGUIUtility.PingObject(asset);
        }
    }
}
#endif
