#if UNITY_6000_5_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    /// <summary>
    /// What a Cloud Code Module is made of: its own assembly definition plus every one it references,
    /// transitively. Deployment packages exactly this closure into the .ccm, so everything that decides
    /// whether a module or its dependencies have changed resolves it from here — otherwise the deploy and
    /// the change detection drift apart about what belongs to the module.
    /// </summary>
    static class ModuleAssemblyClosure
    {
        const string AssetsRoot = "Assets/";

        /// <summary>
        /// Every assembly name that ends up in the module's .ccm: the asmdef's own, each assembly it
        /// references transitively, and its precompiled references. Names are lowercased.
        /// </summary>
        internal static void CollectAssemblyNames(AssemblyDefinitionAsset assemblyDefinitionAsset,
            HashSet<string> allReferences)
        {
            AsmdefJsonData data = AsmdefJsonData.ParseAssemblyDefinitionAsset(assemblyDefinitionAsset);

            // Use the deserialized name (internal assembly name), not the asset name
            allReferences.Add(data.name.ToLowerInvariant());

            // Walk through dependent assembly references (not precompiled)
            if (data != null && data.references != null && data.references.Length > 0)
            {
                foreach (var reference in data.references)
                {
                    var assemblyAsset = ResolveAssemblyDefinition(reference);
                    if (assemblyAsset != null)
                    {
                        CollectAssemblyNames(assemblyAsset, allReferences);
                    }
                }
            }

            if (data != null && data.precompiledReferences != null && data.precompiledReferences.Length > 0)
            {
                foreach (var precompiledReference in data.precompiledReferences)
                {
                    var referenceName = precompiledReference.Replace(".dll", "");
                    allReferences.Add(referenceName.ToLowerInvariant());
                }
            }
        }

        /// <summary>
        /// The directories holding the closure's source, restricted to assembly definitions inside the
        /// project. Package assemblies are excluded: they are not authored here, so watching them would be
        /// cost without signal.
        /// </summary>
        internal static List<string> ProjectSourceDirectories(AssemblyDefinitionAsset assemblyDefinitionAsset)
        {
            var directories = new List<string>();
            CollectProjectSourceDirectories(assemblyDefinitionAsset,
                new HashSet<string>(StringComparer.Ordinal), directories);
            return directories;
        }

        static void CollectProjectSourceDirectories(AssemblyDefinitionAsset assemblyDefinitionAsset,
            HashSet<string> visited, List<string> directories)
        {
            var assetPath = AssetDatabase.GetAssetPath(assemblyDefinitionAsset);
            if (string.IsNullOrEmpty(assetPath) || !visited.Add(assetPath))
                return;

            if (assetPath.StartsWith(AssetsRoot, StringComparison.Ordinal))
            {
                var directory = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(directory))
                {
                    directories.Add(directory);
                }
            }

            var data = AsmdefJsonData.ParseAssemblyDefinitionAsset(assemblyDefinitionAsset);
            if (data?.references == null)
                return;

            foreach (var reference in data.references)
            {
                var referenced = ResolveAssemblyDefinition(reference);
                if (referenced != null)
                {
                    CollectProjectSourceDirectories(referenced, visited, directories);
                }
            }
        }

        static AssemblyDefinitionAsset ResolveAssemblyDefinition(string reference)
        {
            if (reference.StartsWith("GUID:", StringComparison.Ordinal))
            {
                var assemblyGuid = new UnityEngine.GUID(reference.Replace("GUID:", ""));
                return AssetDatabase.LoadAssetByGUID<AssemblyDefinitionAsset>(assemblyGuid);
            }

            return FindAssemblyDefinitionByName(reference);
        }

        static AssemblyDefinitionAsset FindAssemblyDefinitionByName(string assemblyName)
        {
            var guids = AssetDatabase.FindAssets($"{assemblyName} t:AssemblyDefinitionAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
                if (asset != null)
                {
                    var asmdef = AsmdefJsonData.DeserializeFromPath(path);
                    if (asmdef.name == assemblyName)
                        return asset;
                }
            }

            return null;
        }
    }
}
#endif
