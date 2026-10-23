#if UNITY_6000_5_OR_NEWER

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.SourceGenerator
{
    /// <summary>
    /// Player builds read client bindings from Cloud manifests in <c>Library/CloudModules</c>, which Bee doesn't track.
    /// Touching a client source when its manifest is newer makes Bee recompile it rather than reuse a stale DLL.
    /// </summary>
    class StalePlayerBindingsInvalidator : IPreprocessBuildWithReport
    {
        // Runs after StaleManifestBuildCleaner has removed orphaned manifests.
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            var clientSources = CompilationPipeline.GetAssemblies(AssembliesType.Player)
                .ToDictionary(assembly => assembly.name, assembly => (IReadOnlyList<string>)assembly.sourceFiles,
                    StringComparer.OrdinalIgnoreCase);

            InvalidateStaleClients(StaleManifestCleaner.BehaviourBindingManifestDirectory,
                StaleManifestCleaner.BehaviourModuleManifestDirectory, clientSources);
            InvalidateStaleClients(StaleManifestCleaner.BindingManifestDirectory,
                StaleManifestCleaner.ModuleManifestDirectory, clientSources);
        }

        /// <summary>Touches a source of each client whose Cloud manifest is newer than all its sources.</summary>
        /// <returns>The clients touched.</returns>
        internal static List<string> InvalidateStaleClients(string bindingDirectory, string moduleDirectory,
            IReadOnlyDictionary<string, IReadOnlyList<string>> clientSources,
            Action<string, DateTime> setLastWriteTimeUtc = null)
        {
            setLastWriteTimeUtc ??= (path, time) => File.SetLastWriteTimeUtc(path, time);
            var touched = new List<string>();
            string[] bindingManifests;
            try
            {
                if (!Directory.Exists(bindingDirectory))
                    return touched;
                bindingManifests = Directory.GetFiles(bindingDirectory, "*" + StaleManifestCleaner.ManifestExtension);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"[CloudCode] Could not check '{bindingDirectory}' for stale player bindings: {e.Message}");
                return touched;
            }

            foreach (var bindingManifest in bindingManifests)
            {
                var fileName = Path.GetFileName(bindingManifest);
                var client = fileName.Substring(0, fileName.Length - StaleManifestCleaner.ManifestExtension.Length);
                if (!clientSources.TryGetValue(client, out var sources) || sources.Count == 0)
                    continue;

                var cloudModule = ReadCloudModuleName(bindingManifest);
                if (cloudModule == null)
                    continue;

                var cloudManifest = Path.Combine(moduleDirectory, cloudModule + StaleManifestCleaner.ManifestExtension);
                try
                {
                    if (!File.Exists(cloudManifest))
                        continue;
                    // Not the client's own binding manifest: the Editor compile after each touch rewrites it.
                    var cloudChangedAt = File.GetLastWriteTimeUtc(cloudManifest);
                    // A future time (clock corrected backwards) would outrun every touch; clamp our own file instead.
                    var now = DateTime.UtcNow;
                    if (cloudChangedAt > now)
                    {
                        File.SetLastWriteTimeUtc(cloudManifest, now);
                        cloudChangedAt = now;
                    }
                    if (sources.Max(File.GetLastWriteTimeUtc) >= cloudChangedAt)
                        continue;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (TryTouchAny(sources, setLastWriteTimeUtc))
                {
                    touched.Add(client);
                    continue;
                }

                Debug.LogWarning(
                    $"[CloudCode] '{client}' may ship outdated bindings for Cloud module '{cloudModule}': none of its " +
                    "source files could be updated to force a rebuild (are they read-only?). Make any edit to a " +
                    "file in that assembly, or use Clean Build, before building again.");
            }

            return touched;
        }

        // Any source will do: Bee rebuilds an assembly when any of its inputs is newer.
        static bool TryTouchAny(IEnumerable<string> sources, Action<string, DateTime> setLastWriteTimeUtc)
        {
            var now = DateTime.UtcNow;
            foreach (var source in sources)
            {
                try
                {
                    setLastWriteTimeUtc(source, now);
                    return true;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                }
            }

            return false;
        }

        static string ReadCloudModuleName(string bindingManifest)
        {
            try
            {
                return (string)JObject.Parse(File.ReadAllText(bindingManifest))["CloudModule"];
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException
                                      or InvalidCastException or ArgumentException)
            {
                return null;
            }
        }
    }
}

#endif
