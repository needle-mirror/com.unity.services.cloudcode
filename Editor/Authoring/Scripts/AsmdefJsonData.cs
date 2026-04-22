using System;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Scripts
{
    // A serializable class to hold the relevant JSON data from the .asmdef file
    [Serializable]
    class AsmdefJsonData
    {
        public string name;
        public string rootNamespace;
        public string[] references;
        public string[] includePlatforms;
        public string[] excludePlatforms;
        public bool allowUnsafeCode;
        public bool overrideReferences;
        public string[] precompiledReferences;
        public bool autoReferenced;
        public string[] defineConstraints;
        public VersionDefine[] versionDefines;
        public bool noEngineReferences;

        [Serializable]
        internal class VersionDefine
        {
            public string name;
            public string expression;
            public string define;
        }

        internal static AsmdefJsonData ParseAssemblyDefinitionAsset(AssemblyDefinitionAsset asmdefAsset)
        {
            // Grab the path for a given Assembly definition asset.
            string assetPath = AssetDatabase.GetAssetPath(asmdefAsset);
            return DeserializeFromPath(assetPath);
        }

        internal static AsmdefJsonData DeserializeFromPath(string assetPath)
        {
            // Read the JSON content from the file
            string jsonText = File.ReadAllText(assetPath);

            // Parse the JSON into our serializable structure
            return JsonUtility.FromJson<AsmdefJsonData>(jsonText);
        }

        internal static void SerializeToPath(AsmdefJsonData asmdefJsonData, string assetPath)
        {
            File.WriteAllText(assetPath, JsonUtility.ToJson(asmdefJsonData));
        }
    }
}
