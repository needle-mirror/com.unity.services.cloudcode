using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(NativeModuleReference))]
    class NativeModuleReferenceInspector : UnityEditor.Editor
    {
        [SerializeField]
        VisualTreeAsset m_VisualTreeAsset;

        static readonly string k_UxmlPath =
            Path.Combine(CloudCodePackage.EditorPath, UxmlConstants.UxmlAssetPath);

        public override VisualElement CreateInspectorGUI()
        {
            var uxmlAsset = m_VisualTreeAsset;
            if (uxmlAsset == null)
            {
                uxmlAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_UxmlPath);
                if (uxmlAsset == null)
                {
                    return DisplayMissingUxml();
                }
            }

            var root = new VisualElement();
            uxmlAsset.CloneTree(root);

            root.Bind(serializedObject);

            return root;
        }

        static VisualElement DisplayMissingUxml()
        {
            var uxmlAssetName = Path.GetFileName(k_UxmlPath);
            var errorMessage = $"Failed to load \"{uxmlAssetName}\". Please ensure the asset exists at: \"{k_UxmlPath}\".";
            Debug.LogError(errorMessage);
            var errorRoot = new VisualElement();
            errorRoot.Add(new HelpBox(errorMessage, HelpBoxMessageType.Error));
            return errorRoot;
        }

        static class UxmlConstants
        {
            public const string UxmlAssetPath = "Authoring/Modules/UI/Assets/NativeModuleReferenceUi.uxml";
        }
    }
}
