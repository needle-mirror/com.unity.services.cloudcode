using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.SourceGenerator
{
    /// <summary>
    /// Generated manifests are written one-per-assembly to <c>Library/CloudModules</c> and would otherwise
    /// outlive the code they describe. When a module is deleted or renamed its assembly no longer exists, so
    /// no source generator runs for it to clean up — this removes any manifest whose assembly is no longer in
    /// the project, before each compile and each player build.
    /// </summary>
    /// <remarks>
    /// This handles only the removed/renamed case. The errored-module case is handled in the source
    /// generators themselves (they clear their assembly's manifest and skip generation when the compilation
    /// is invalid), because a failed assembly still runs its generator — so keying the error case off the
    /// generator is both sufficient and avoids deleting a manifest the generator is about to rewrite.
    /// <para>
    /// Orphans only — a live assembly's manifest is never touched, so an unrelated partial recompile (which
    /// doesn't regenerate module manifests) can't strand a module without its manifest.
    /// </para>
    /// </remarks>
    [InitializeOnLoad]
    static class StaleManifestCleaner
    {
        const string ManifestExtension = ".g.json";

        static readonly string s_Root =
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "CloudModules"));

        static readonly string[] s_ManifestDirectories =
        {
            Path.Combine(s_Root, "ModuleManifests"),
            Path.Combine(s_Root, "BindingManifests")
        };

        /// <summary>The real on-disk manifest directories the cleaner operates on. Exposed for tests.</summary>
        internal static IReadOnlyList<string> ManifestDirectories => s_ManifestDirectories;

        static StaleManifestCleaner()
        {
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
        }

        static void OnCompilationStarted(object _) => RemoveOrphans();

        internal static void RemoveOrphans()
        {
            // Fast path for projects with no Cloud modules: nothing has been generated, so skip the
            // (relatively expensive) full assembly enumeration below.
            if (!s_ManifestDirectories.Any(Directory.Exists))
                return;

            // Case-insensitive: filesystems (Windows/macOS) and case-only assembly renames must not cause a
            // live assembly's manifest to look orphaned and get deleted.
            var live = new HashSet<string>(
                CompilationPipeline.GetAssemblies(AssembliesType.Editor)
                    .Concat(CompilationPipeline.GetAssemblies(AssembliesType.Player))
                    .Select(assembly => assembly.name),
                StringComparer.OrdinalIgnoreCase);

            RemoveOrphans(s_ManifestDirectories, live);
        }

        internal static void RemoveOrphans(IEnumerable<string> directories, HashSet<string> liveAssemblies)
        {
            foreach (var directory in directories)
                RemoveOrphansIn(directory, liveAssemblies);
        }

        // Every step here is I/O that can throw (enumeration, deletion) — a lock or permission error must not
        // escape the compilation/build callback or take down the rest of the pass, so the whole scan is
        // guarded. A stale manifest left behind is recoverable; an escaped exception is not.
        internal static void RemoveOrphansIn(string directory, HashSet<string> liveAssemblies)
        {
            try
            {
                if (!Directory.Exists(directory))
                    return;

                foreach (var file in Directory.GetFiles(directory, "*" + ManifestExtension))
                {
                    var fileName = Path.GetFileName(file);
                    var assemblyName = fileName.Substring(0, fileName.Length - ManifestExtension.Length);
                    if (!liveAssemblies.Contains(assemblyName))
                        File.Delete(file);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"[CloudCode] Could not sweep stale manifests in '{directory}': {e.Message}");
            }
        }
    }

    class StaleManifestBuildCleaner : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report) => StaleManifestCleaner.RemoveOrphans();
    }
}
