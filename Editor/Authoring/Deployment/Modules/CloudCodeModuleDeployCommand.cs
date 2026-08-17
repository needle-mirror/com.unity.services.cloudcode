#if UNITY_6000_5_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.AdminApi;
using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Core.IO;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.DeploymentApi.Editor;
using Unity.Services.CloudCode.Editor.Shared.DependencyInversion;
using UnityEditor.Build;
using UnityEngine;
using Exception = System.Exception;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules
{
    class CloudCodeModuleDeployCommand : Command<CloudCodeModule>
    {
        const string k_CompilationFailureTitle = "Compilation Failure";
        const string k_CompilationFailureDetails = "Script compilation has failed. Please fix all compilation errors before deploying Cloud Code Modules.";
        const string k_UnsupportedAPICompatiblityLevel = "Unsupported API Compatibility Level";
        const string k_UnsupportedAPICompatiblityLevelDetails = "The project uses an unsupported API compatibillity level. Consider switching \"Api Compatiblity Level\" to \".NET Standard 2.1\" in player settings.";
        static readonly string k_CloudCodeModulesDirectory =  Path.Combine(Application.dataPath, "../Library/CloudModules/");

        readonly IDeploymentAnalytics m_DeploymentAnalytics;
        readonly IFileSystem m_FileSystem;
        readonly IModuleZipper m_ModuleZipper;
        readonly IModuleContentHasher m_ContentHasher;
        readonly EditorCloudCodeLocalModuleDeploymentHandler m_LocalDeploymentHandler;
        readonly CloudCodeDeploymentHandler m_RemoteDeploymentHandler;

        public override string Name { get; } = L10n.Tr("Deploy");

        public CloudCodeModuleDeployCommand(
            ICloudCodeModulesClient modulesClient,
            IDeploymentAnalytics analytics,
            ILogger logger,
            IPreDeployValidator validator,
            IFileSystem fileSystem,
            IModuleZipper moduleZipper,
            EditorCloudCodeLocalModuleDeploymentHandler localDeploymentHandler,
            IModuleContentHasher contentHasher)
        {
            m_DeploymentAnalytics = analytics;
            m_FileSystem = fileSystem;
            m_ModuleZipper = moduleZipper;
            m_ContentHasher = contentHasher;
            m_LocalDeploymentHandler = localDeploymentHandler;
            m_RemoteDeploymentHandler =
                new CloudCodeDeploymentHandler(modulesClient, analytics, logger, validator);
        }

        public override async Task ExecuteAsync(IEnumerable<CloudCodeModule> items,
            CancellationToken cancellationToken = default)
        {
            var moduleReferences = items.ToList();
            m_LocalDeploymentHandler.ClearDeploymentStatuses(moduleReferences);

            var apiCompatibilityLevel =
                PlayerSettings.GetApiCompatibilityLevel(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup));
            if (apiCompatibilityLevel != ApiCompatibilityLevel.NET_Standard)
            {
                m_LocalDeploymentHandler.SetDeployStatusesWithState(moduleReferences, k_UnsupportedAPICompatiblityLevel,
                    k_UnsupportedAPICompatiblityLevelDetails, SeverityLevel.Error);
                m_DeploymentAnalytics.SendFailureDeploymentEvent(k_UnsupportedAPICompatiblityLevel);
                return;
            }

            if (ShouldDeployToLocal())
                await GenerateAndDeployToLocalAsync(moduleReferences, cancellationToken);
            else
                await GenerateAndDeployToRemoteAsync(moduleReferences, cancellationToken);
        }

        // Source of truth for a module's deployed name is the cloud assembly (asmdef) name, since that is
        // what the generated client and manifest call. Falls back to the asset file name only when the
        // asmdef cannot be read, preserving the previous behavior.
        internal static string GetDeployModuleName(CloudCodeModule ccm)
        {
            var asmdef = ccm.CloudAssemblyDefinition != null
                ? AsmdefJsonData.ParseAssemblyDefinitionAsset(ccm.CloudAssemblyDefinition)
                : null;

            return string.IsNullOrEmpty(asmdef?.name)
                ? Path.GetFileNameWithoutExtension(ccm.Name)
                : asmdef.name;
        }

        static bool ShouldDeployToLocal()
        {
            var server = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
            return server.GetCurrentServerStatus() == ICloudCodeLocalServer.LocalCloudCodeServerStatus.Started;
        }

        internal async Task<string> GenerateAndDeployToLocalAsync(List<CloudCodeModule> moduleReferences,
            CancellationToken cancellationToken = default)
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                m_LocalDeploymentHandler.SetDeployStatusesWithState(moduleReferences, k_CompilationFailureTitle,
                    k_CompilationFailureDetails,
                    severity: SeverityLevel.Error);
                throw new Exception(k_CompilationFailureDetails);
            }

            // Ensure that each CCMs references unique assemblies representing their module
            var(validCCMs, invalidCCMs) = PartitionValidCCMs(moduleReferences);
            m_LocalDeploymentHandler.UpdateDeployStatuses(invalidCCMs,
                "Assembly Failure",
                "Multiple CCMs reference the same assembly definition. " +
                "Please ensure each CCM has its own unique assembly definition.",
                severity: SeverityLevel.Error);

            var deployedHashes = await SnapshotContentHashesAsync(validCCMs);
            var modulesToZip = GetAllAssemblyPathsForModules(validCCMs);
            var deploymentDict = await ZipCloudCodeModuleAsync(modulesToZip, cancellationToken);
            var moduleDestinationDir = await m_LocalDeploymentHandler.DeployAsync(deploymentDict, cancellationToken);

            // Records the last successful deploy of the module. This reflects that the module was
            // deployed, separate from whether the local server is currently running.
            RecordSuccessfulDeployments(deploymentDict.Keys.ToList(), DeploymentTarget.Local, deployedHashes);
            await ReconcileAfterDeployAsync(deploymentDict.Keys);

            return moduleDestinationDir;
        }

        async Task GenerateAndDeployToRemoteAsync(List<CloudCodeModule> moduleReferences,
            CancellationToken cancellationToken = default)
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                m_LocalDeploymentHandler.SetDeployStatusesWithState(moduleReferences, k_CompilationFailureTitle,
                    k_CompilationFailureDetails,
                    severity: SeverityLevel.Error);
                return;
            }

            // Ensure that each CCMs references unique assemblies representing their module
            var(validCCMs, invalidCCMs) = PartitionValidCCMs(moduleReferences);
            m_LocalDeploymentHandler.UpdateDeployStatuses(invalidCCMs,
                "Assembly Failure",
                "Multiple CCMs reference the same assembly definition. " +
                "Please ensure each CCM has its own unique assembly definition.",
                severity: SeverityLevel.Error);

            var deployedHashes = await SnapshotContentHashesAsync(validCCMs);
            var modulesToZip = GetAllAssemblyPathsForModules(validCCMs);
            var deploymentDict = await ZipCloudCodeModuleAsync(modulesToZip, cancellationToken);

            var result = await m_RemoteDeploymentHandler.DeployAsync(deploymentDict.Values.ToList());

            // Map deployed scripts back to their module items.
            var deployedModules = deploymentDict
                .Where(entry => result.Deployed.Contains(entry.Value))
                .Select(entry => entry.Key)
                .ToList();

            RecordSuccessfulDeployments(deployedModules, DeploymentTarget.Remote, deployedHashes);
            await ReconcileAfterDeployAsync(deployedModules);
        }

        /// <summary>
        /// Fingerprints each module's source before the deploy runs, so the baseline recorded on success
        /// reflects the content that was deployed rather than any edit made while the deploy is in flight.
        /// </summary>
        async Task<Dictionary<CloudCodeModule, string>> SnapshotContentHashesAsync(IEnumerable<CloudCodeModule> modules)
        {
            var hashes = new Dictionary<CloudCodeModule, string>();
            foreach (var module in modules)
                hashes[module] = await m_ContentHasher.ComputeHashAsync(module);
            return hashes;
        }

        /// <summary>
        /// Records each module's last successful deployment, pairing the deployment target with the source
        /// fingerprint captured before the deploy so a deployed module always carries the hash the
        /// modified-tracker compares against. The hash is null only when a module's source cannot be hashed.
        /// </summary>
        internal void RecordSuccessfulDeployments(IEnumerable<IModuleItem> deployedModules, DeploymentTarget target,
            IReadOnlyDictionary<CloudCodeModule, string> deployedHashes)
        {
            foreach (var module in deployedModules)
            {
                string contentHash = null;
                if (module is CloudCodeModule ccm)
                {
                    deployedHashes.TryGetValue(ccm, out contentHash);
                    // Seed the modified-tracker cache: right after deploy the current source matches what
                    // was deployed, so a reload before any edit restores up-to-date without re-hashing.
                    ccm.CurrentContentHash = contentHash;
                }

                module.LastSuccessfulDeployment = LastSuccessfulDeploymentInfo.Create(target, contentHash);
            }
        }

        /// <summary>
        /// A change made while a module was mid-deploy arrives during a transient, non-reconcilable status
        /// and is dropped by the tracker; reconcile now against the just-recorded baseline so a mid-deploy
        /// edit surfaces as modified once the deploy settles. Awaited (rather than the fire-and-forget
        /// <see cref="ITrackableItem.TrackOrUpdate"/>) so the reconcile stays the last writer of status.
        /// </summary>
        static async Task ReconcileAfterDeployAsync(IEnumerable<IModuleItem> deployedModules)
        {
            CloudCodeModuleModifiedTracker tracker;
            try
            {
                tracker = CloudCodeAuthoringServices.Instance.GetService<CloudCodeModuleModifiedTracker>();
            }
            catch (Exception e) when (e is DependencyNotFoundException or NullReferenceException)
            {
                return;
            }

            foreach (var ccm in deployedModules.OfType<CloudCodeModule>())
                await tracker.ReconcileAsync(ccm);
        }

        internal static (List<CloudCodeModule>, List<CloudCodeModule>) PartitionValidCCMs(List<CloudCodeModule> ccms)
        {
            var assemblyGuidGroups = ccms
                .Where(ccm => ccm.CloudAssemblyDefinition != null)
                .GroupBy(ccm => AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(ccm.CloudAssemblyDefinition)))
                .ToList();

            var validNCCms = assemblyGuidGroups
                .Where(g => g.Count() == 1)
                .SelectMany(g => g)
                .ToList();

            var invalidNCCMs = assemblyGuidGroups
                .Where(g => g.Count() > 1)
                .SelectMany(g => g)
                .ToList();

            return (validNCCms, invalidNCCMs);
        }

        Dictionary<CloudCodeModule, List<string>> GetAllAssemblyPathsForModules(List<CloudCodeModule> ccms)
        {
            var allAssemblyDependencies = new Dictionary<CloudCodeModule, List<string>>();

            // Compose all known assembly paths to ones that Unity does not compile itself.
            var preCompiledAssemblyPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(
                CompilationPipeline.PrecompiledAssemblySources.UserAssembly);

            // Grab all assemblies that Unity manages and compiles.
            var unityCompiledAssemblies = CompilationPipeline.GetAssemblies();
            var unityCompiledAssemblyPaths = unityCompiledAssemblies.Select(a => a.outputPath);
            var unityPrecompiledReferencePaths = unityCompiledAssemblies.SelectMany(a => a.compiledAssemblyReferences);

            var assemblyCache = new Dictionary<string, string>();
            foreach (var path in preCompiledAssemblyPaths
                     .Concat(unityCompiledAssemblyPaths)
                     .Concat(unityPrecompiledReferencePaths))
            {
                assemblyCache[Path.GetFileNameWithoutExtension(path).ToLowerInvariant()] = path;
            }

#if UNITY_6000_5_OR_NEWER
            ApplyEditorLoadedAssemblyPaths(assemblyCache);
#endif

            // Loop through each CCM's list of assembly references, recursively grab its list of required
            // assemblies, verify them against the assembly cache and compose the result into allAssemblyDependencies.
            foreach (var ccm in ccms)
            {
                m_LocalDeploymentHandler.UpdateDeployStatus(ccm, "Processing Cloud Code Assemblies... ");

                if (ccm.CloudAssemblyDefinition == null)
                {
                    m_LocalDeploymentHandler.UpdateDeployStatus(ccm,
                        "Assemblies Failure ",
                        $"No assembly definition was found for module." ,
                        severity: SeverityLevel.Error);
                    continue;
                }

                // For a CCM's assembly definition, recursively search and grab all assembly references and its dependencies
                var references = new HashSet<string>();
                try
                {
                    GetAllReferencesFromAssemblyDefinition(ccm.CloudAssemblyDefinition, references);
                }
                catch (Exception e)
                {
                    m_LocalDeploymentHandler.UpdateDeployStatus(ccm,
                        "Assemblies Failure ",
                        $"Error grabbing assemblies for Cloud Code module {e.Message}.",
                        severity: SeverityLevel.Error);
                    continue;
                }

                // For each reference, look up the paths from the assembly cache and add it to the list.
                List<string> assemblyPaths = new List<string>();
                bool foundAssemblyPath = true;
                foreach (var reference in references)
                {
                    if (assemblyCache.ContainsKey(reference))
                    {
                        assemblyPaths.Add(assemblyCache[reference]);
                    }
                    else
                    {
                        m_LocalDeploymentHandler.UpdateDeployStatus(ccm,
                            "Assemblies Failure ",
                            $"Unable to find required assembly: {reference}.",
                            severity: SeverityLevel.Error);
                        foundAssemblyPath = false;
                        break;
                    }
                }

                if (foundAssemblyPath)
                {
                    allAssemblyDependencies.Add(ccm, assemblyPaths);
                    m_LocalDeploymentHandler.UpdateDeployStatus(ccm, "Cloud Code Assemblies Found.");
                }
            }

            return allAssemblyDependencies;
        }

        void GetAllReferencesFromAssemblyDefinition(AssemblyDefinitionAsset assemblyDefinitionAsset,
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
                    AssemblyDefinitionAsset assemblyAsset;
                    if (reference.StartsWith("GUID:"))
                    {
                        var assemblyGuid = new GUID(reference.Replace("GUID:", ""));
                        assemblyAsset = AssetDatabase.LoadAssetByGUID<AssemblyDefinitionAsset>(assemblyGuid);
                    }
                    else
                    {
                        assemblyAsset = FindAssemblyDefinitionByName(reference);
                    }

                    if (assemblyAsset != null)
                    {
                        GetAllReferencesFromAssemblyDefinition(assemblyAsset, allReferences);
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

#if UNITY_6000_5_OR_NEWER
        static void ApplyEditorLoadedAssemblyPaths(Dictionary<string, string> assemblyCache)
        {
            foreach (var entry in UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies()
                     .SelectMany(EditorLoadedAssemblyPathPairs)
                     .OrderBy(pair => pair.Key)
                     .ThenBy(pair => pair.Value))
            {
                assemblyCache[entry.Key] = entry.Value;
            }
        }

        static IEnumerable<KeyValuePair<string, string>> EditorLoadedAssemblyPathPairs(System.Reflection.Assembly assembly)
        {
            if (TryResolveEditorLoadedAssemblyPath(assembly, out var path))
            {
                yield return new KeyValuePair<string, string>(assembly.GetName().Name.ToLowerInvariant(), path);
            }
        }

        static bool TryResolveEditorLoadedAssemblyPath(System.Reflection.Assembly assembly, out string path)
        {
            if (assembly.IsDynamic)
            {
                path = null;
                return false;
            }
            try
            {
                path = assembly.GetLoadedAssemblyPath();
                return !string.IsNullOrEmpty(path);
            }
            catch (NotSupportedException)
            {
                path = null;
                return false;
            }
        }

#endif

        async Task<Dictionary<IModuleItem, IScript>> ZipCloudCodeModuleAsync(
            Dictionary<CloudCodeModule, List<string>> allAssemblyDependencies,
            CancellationToken cancellationToken)
        {
            var allAssembliesToDeploy = new Dictionary<IModuleItem, IScript>();

            // For each Cloud Code Module, zip up all the compiled lists of required assemblies to deploy
            foreach (var assembly in allAssemblyDependencies)
            {
                var ccm = assembly.Key;
                var assemblyPaths = assembly.Value;
                m_LocalDeploymentHandler.UpdateDeployStatus(ccm, "Zipping Cloud Code Module...", shouldLog: false);

                try
                {
                    // Name everything after the cloud assembly (asmdef) name, not the .ccmu asset name,
                    // so the stored zip matches what the generated client calls.
                    var moduleName = GetDeployModuleName(ccm);

                    // Flush the directory to ensure we are zipping the latest files.
                    var cloudModuleDir = Path.Combine(k_CloudCodeModulesDirectory, moduleName);
                    if (m_FileSystem.DirectoryExists(cloudModuleDir))
                    {
                        await m_FileSystem.DeleteDirectory(cloudModuleDir, true);
                    }
                    await m_FileSystem.CreateDirectory(cloudModuleDir);

                    // Move all required assemblies into the directory to be zipped.
                    foreach (var assemblyPath in assemblyPaths)
                    {
                        string fileName = Path.GetFileName(assemblyPath);
                        var dest = Path.Combine(cloudModuleDir, fileName);
                        await m_FileSystem.Copy(assemblyPath, dest, true, cancellationToken);
                    }

                    // Finally, zip the files and add it to the dictionary of assemblies to deploy
                    var result = await m_ModuleZipper.ZipCompilation(cloudModuleDir, k_CloudCodeModulesDirectory,
                        moduleName, cancellationToken);

                    var moduleToDeploy = GenerateModule(result, ccm, moduleName);
                    allAssembliesToDeploy.Add(ccm, moduleToDeploy);

                    m_LocalDeploymentHandler.UpdateDeployStatus(ccm, "Zipped Successfully");
                }
                catch (Exception e)
                {
                    m_LocalDeploymentHandler.UpdateDeployStatus(ccm, $"Zip Failure {e.Message}", severity: SeverityLevel.Error);
                }
            }

            return allAssembliesToDeploy;
        }

        static Module GenerateModule(string path, CloudCodeModule moduleReference, string moduleName)
        {
            var name = new ScriptName(moduleName);
            var module = new Module(path, moduleReference)
            {
                Name = name,
                Body = string.Empty,
                Parameters = new List<CloudCodeParameter>(),
                Language = Language.CS
            };

            return module;
        }
    }
}
#endif
