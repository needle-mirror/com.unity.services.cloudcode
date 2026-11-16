#if UNITY_6000_7_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using UnityEditor;
using UnityEditor.Compilation;
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
            var data = AsmdefJsonData.ParseAssemblyDefinitionAssetOrThrow(assemblyDefinitionAsset);

            // Use the deserialized name (internal assembly name), not the asset name
            allReferences.Add(data.name.ToLowerInvariant());

            // Walk through dependent assembly references (not precompiled)
            if (data.references != null && data.references.Length > 0)
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

            if (data.precompiledReferences != null && data.precompiledReferences.Length > 0)
            {
                foreach (var precompiledReference in data.precompiledReferences)
                {
                    var referenceName = precompiledReference.Replace(".dll", "");
                    allReferences.Add(referenceName.ToLowerInvariant());
                }
            }
        }

        internal static List<string> DependencyAssemblyPaths(AssemblyDefinitionAsset assemblyDefinitionAsset)
        {
            var names = new HashSet<string>();
            CollectAssemblyNames(assemblyDefinitionAsset, names);

            var projectCompiled = new HashSet<string>(CompilationPipeline.GetAssemblies()
                .Where(IsProjectCompiled)
                .Select(assembly => assembly.name.ToLowerInvariant()));

            var assemblyPaths = CloudCodeModuleDeployCommand.BuildAssemblyPathCache();
            return names
                .Where(name => !projectCompiled.Contains(name) && assemblyPaths.ContainsKey(name))
                .Select(name => assemblyPaths[name])
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        // A define or optimization change rebuilds the uploaded DLL without touching a source file.
        internal static string CompilationSettings(AssemblyDefinitionAsset assemblyDefinitionAsset)
        {
            var names = new HashSet<string>();
            CollectAssemblyNames(assemblyDefinitionAsset, names);

            var settings = CompilationPipeline.GetAssemblies()
                .Where(assembly => IsProjectCompiled(assembly) && names.Contains(assembly.name.ToLowerInvariant()))
                .Select(assembly => DescribeCompilation(assembly.name, assembly.defines, assembly.compilerOptions))
                .OrderBy(setting => setting, StringComparer.Ordinal);
            return string.Join("\n", settings);
        }

        internal static string DescribeCompilation(string assemblyName, IEnumerable<string> defines, ScriptCompilerOptions options)
        {
            var orderedDefines = (defines ?? Array.Empty<string>()).Distinct().OrderBy(define => define, StringComparer.Ordinal);
            var arguments = options.AdditionalCompilerArguments ?? Array.Empty<string>();
            return $"{assemblyName}|{string.Join(";", orderedDefines)}|{options.ApiCompatibilityLevel}|" +
                $"{options.CodeOptimization}|{options.AllowUnsafeCode}|{string.Join(" ", arguments)}";
        }

        static bool IsProjectCompiled(UnityEditor.Compilation.Assembly assembly)
        {
            return assembly.sourceFiles.Any(file => file.StartsWith(AssetsRoot, StringComparison.Ordinal));
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

            var data = AsmdefJsonData.ParseAssemblyDefinitionAssetOrThrow(assemblyDefinitionAsset);
            if (data.references == null)
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
            var path = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(path);
        }
    }
}
#endif
