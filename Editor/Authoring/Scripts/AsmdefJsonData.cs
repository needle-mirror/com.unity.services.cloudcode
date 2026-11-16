using System;
using System.IO;
using System.Linq;
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
            if (asmdefAsset == null)
                return null;

            // Grab the path for a given Assembly definition asset.
            string assetPath = AssetDatabase.GetAssetPath(asmdefAsset);
            return DeserializeFromPath(assetPath);
        }

        internal static bool IsUnreadable(string asmdefPath)
        {
            try
            {
                ParseAssemblyDefinitionAssetOrThrow(AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPath));
                return false;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        internal static AsmdefJsonData ParseAssemblyDefinitionAssetOrThrow(AssemblyDefinitionAsset asmdefAsset)
        {
            var path = AssetDatabase.GetAssetPath(asmdefAsset);
            AsmdefJsonData data;
            try
            {
                data = DeserializeFromPath(path);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"The assembly definition '{path}' is not valid JSON: {e.Message}", e);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                throw new InvalidOperationException($"The assembly definition '{path}' could not be read: {e.Message}", e);
            }

            if (string.IsNullOrEmpty(data?.name))
                throw new InvalidOperationException($"The assembly definition '{path}' has no name.");

            return data;
        }

        internal static AsmdefJsonData DeserializeFromPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            // Read the JSON content from the file
            string jsonText = File.ReadAllText(assetPath);

            // Parse the JSON into our serializable structure
            return JsonUtility.FromJson<AsmdefJsonData>(jsonText);
        }

        internal void SerializeToPath(string assetPath)
        {
            File.WriteAllText(assetPath, JsonUtility.ToJson(this));
        }

        internal void AddAssemblyReferenceAtPath(string asmdefPath)
        {
            string newReference;
            if (ReferencesUseGUIDs())
            {
                newReference = AssetDatabase.AssetPathToGUID(asmdefPath);

                if (string.IsNullOrEmpty(newReference))
                {
                    throw new FileNotFoundException($"Failed to find GUID for asset at path: {asmdefPath}. Ensure the asset has been imported.");
                }

                newReference = $"GUID:{newReference}";
            }
            else
            {
                var referenceToAdd = DeserializeFromPath(asmdefPath);
                newReference = referenceToAdd.name;
            }

            references = references.Append(newReference).ToArray();
        }

        private bool ReferencesUseGUIDs()
        {
            // Default to false if no references have yet been set.
            if (references == null || references.Length == 0)
                return false;

            return references[0].Contains("GUID");
        }

        internal bool HasAssemblyReferenceAtPath(string asmdefPath)
        {
            var guidReference = $"GUID:{AssetDatabase.AssetPathToGUID(asmdefPath)}";
            var nameReference = DeserializeFromPath(asmdefPath)?.name;

            foreach (var reference in references)
            {
                var isGuid = reference.StartsWith("GUID:", StringComparison.Ordinal);
                var target = isGuid ? guidReference : nameReference;
                // Hex GUIDs match in either case; asmdef names are resolved case-sensitively by CompilationPipeline.
                if (string.Equals(reference, target, isGuid ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        internal bool HasNameThatMatches(AssemblyDefinitionAsset asmdefAsset)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            var parsed = ParseAssemblyDefinitionAsset(asmdefAsset);
            return parsed != null && string.Equals(name, parsed.name, StringComparison.Ordinal);
        }
    }
}
