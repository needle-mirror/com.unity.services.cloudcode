using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Core.IO;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.DeploymentApi.Editor;
using Exception = System.Exception;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules
{
    class NativeModuleDeployCommand : Command<NativeModuleReference>
    {
        const string k_CompilationFailureTitle = "Compilation Failure";
        const string k_CompilationFailureDetails = "Script compilation has failed. Please fix all compilation errors before deploying Native Modules.";
        readonly IFileSystem m_FileSystem;
        readonly IModuleZipper m_ModuleZipper;
        readonly EditorCloudCodeLocalModuleDeploymentHandler m_LocalDeploymentHandler;
        readonly EditorCloudCodeModuleDeploymentHandler m_RemoteDeploymentHandler;
        static readonly string k_NativeModulesDirectory =  Path.Combine(Application.dataPath, $"../Library/CloudModules/");

        public override string Name { get; } = L10n.Tr("Deploy");

        public NativeModuleDeployCommand(
            IFileSystem fileSystem,
            IModuleZipper moduleZipper,
            EditorCloudCodeLocalModuleDeploymentHandler localDeploymentHandler,
            EditorCloudCodeModuleDeploymentHandler remoteDeploymentHandler)
        {
            m_FileSystem = fileSystem;
            m_ModuleZipper = moduleZipper;
            m_LocalDeploymentHandler = localDeploymentHandler;
            m_RemoteDeploymentHandler = remoteDeploymentHandler;
        }

        public override async Task ExecuteAsync(IEnumerable<NativeModuleReference> items,
            CancellationToken cancellationToken = default)
        {
            var moduleReferences = items.ToList();
            m_LocalDeploymentHandler.ClearDeploymentStatuses(moduleReferences);

            if (ShouldDeployToLocal())
                await GenerateAndDeployToLocalAsync(moduleReferences, cancellationToken);
            else
                await GenerateAndDeployToRemoteAsync(moduleReferences, cancellationToken);
        }

        bool ShouldDeployToLocal()
        {
            var server = CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
            return server.GetCurrentServerStatus() == ICloudCodeLocalServer.LocalCloudCodeServerStatus.Started;
        }

        internal async Task<string> GenerateAndDeployToLocalAsync(List<NativeModuleReference> moduleReferences,
                                                                  CancellationToken cancellationToken = default)
        {
            if (EditorUtility.scriptCompilationFailed)
            {
                m_LocalDeploymentHandler.SetDeployStatusesWithState(moduleReferences, k_CompilationFailureTitle,
                    k_CompilationFailureDetails,
                    severity: SeverityLevel.Error);
                throw new Exception("Script compilation failure. Please fix all compilation errors before deploying Native Modules.");
            }

            // Ensure that each CCMs references unique assemblies representing their module
            var(validCCMs, invalidCCMs) = PartitionValidCCMs(moduleReferences);
            m_LocalDeploymentHandler.UpdateDeployStatuses(invalidCCMs,
                "Assembly Failure",
                "Multiple CCMs reference the same assembly definition. " +
                "Please ensure each CCM has its own unique assembly definition.",
                severity: SeverityLevel.Error);

            var modulesToZip = GetAllAssemblyPathsForModules(validCCMs);
            var deploymentDict = await ZipNativeModule(modulesToZip, cancellationToken);
            return await m_LocalDeploymentHandler.DeployAsync(deploymentDict, cancellationToken);
        }

        async Task GenerateAndDeployToRemoteAsync(List<NativeModuleReference> moduleReferences,
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
            var (validCCMs, invalidCCMs) = PartitionValidCCMs(moduleReferences);
            m_LocalDeploymentHandler.UpdateDeployStatuses(invalidCCMs,
                "Assembly Failure",
                "Multiple CCMs reference the same assembly definition. " +
                "Please ensure each CCM has its own unique assembly definition.",
                severity: SeverityLevel.Error);

            var modulesToZip = GetAllAssemblyPathsForModules(validCCMs);
            var deploymentDict = await ZipNativeModule(modulesToZip, cancellationToken);

            m_RemoteDeploymentHandler.SetReferenceFiles(deploymentDict.Keys.ToList());
            await m_RemoteDeploymentHandler.DeployAsync(deploymentDict.Values.ToList());
        }

        static (List<NativeModuleReference>, List<NativeModuleReference>) PartitionValidCCMs(List<NativeModuleReference> ccms)
        {
            var assemblyGuidGroups = ccms
                .Where(ccm => ccm.AssemblyDefinition != null)
                .GroupBy(ccm => AssetDatabase.GUIDFromAssetPath(AssetDatabase.GetAssetPath(ccm.AssemblyDefinition)))
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

        Dictionary<NativeModuleReference, List<string>> GetAllAssemblyPathsForModules(List<NativeModuleReference> ccms)
        {
            var allAssemblyDependencies = new Dictionary<NativeModuleReference, List<string>>();

            // Compose all known assembly paths from Compilation Pipeline to be compared against.
            var preCompiledAssemblyPaths = CompilationPipeline.GetPrecompiledAssemblyPaths(
                CompilationPipeline.PrecompiledAssemblySources.UserAssembly);

            var compiledAssemblyPaths = CompilationPipeline.GetAssemblies().Select(a => a.outputPath);

            var assemblyCache = preCompiledAssemblyPaths.Concat(compiledAssemblyPaths)
                .ToDictionary(p => Path.GetFileNameWithoutExtension(p).ToLowerInvariant(), p => p);

            // Loop through each CCM's list of assembly references, recursively grab its list of required
            // assemblies, verify them against the assembly cache and compose the result into allAssemblyDependencies.
            foreach (var ccm in ccms)
            {
                m_LocalDeploymentHandler.UpdateDeployStatus(ccm, "Processing Native Assemblies... ");

                if (ccm.AssemblyDefinition == null)
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
                    GetAllReferencesFromAssemblyDefinition(ccm.AssemblyDefinition, references);
                }
                catch (Exception e)
                {
                    m_LocalDeploymentHandler.UpdateDeployStatus(ccm,
                        "Assemblies Failure ",
                        $"Error grabbing assemblies for Native module {e.Message}.",
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
                    m_LocalDeploymentHandler.UpdateDeployStatus(ccm, "Native Assemblies Found.");
                }
            }

            return allAssemblyDependencies;
        }

        void GetAllReferencesFromAssemblyDefinition(AssemblyDefinitionAsset assemblyDefinitionAsset,
            HashSet<string> allReferences)
        {
            // First, add and track the parent assembly to the set of references.
            allReferences.Add(assemblyDefinitionAsset.name.ToLowerInvariant());

            // Next we parse through its dependencies.
            // First, walk through the Assembly Definition Reference asset and
            // - Grab the name of all Precompiled references.
            // - Grab the name of all Assembly Definition references (not precompiled).
            AsmdefJsonData data = AsmdefJsonData.ParseAssemblyDefinitionAsset(assemblyDefinitionAsset);

            // Walk through additional dependent assembly references (not precompiled) if it exists
            if (data != null && data.references != null && data.references.Length > 0)
            {
                foreach (var reference in data.references)
                {
                    GUID assemblyGuid = new GUID(reference.Replace("GUID:", ""));
                    var assemblyAsset = AssetDatabase.LoadAssetByGUID<AssemblyDefinitionAsset>(assemblyGuid);

                    // Now recursively walk through the dependency
                    GetAllReferencesFromAssemblyDefinition(assemblyAsset, allReferences);
                }
            }

            // Else, grab all the precompiled references by name
            if (data != null && data.precompiledReferences != null && data.precompiledReferences.Length > 0)
            {
                foreach (var precompiledReference in data.precompiledReferences)
                {
                    var referenceName = precompiledReference.Replace(".dll", "");
                    allReferences.Add(referenceName.ToLowerInvariant());
                }
            }
        }

        async Task<Dictionary<IModuleItem, IScript>> ZipNativeModule(
            Dictionary<NativeModuleReference, List<string>> allAssemblyDependencies,
            CancellationToken cancellationToken)
        {
            var allAssembliesToDeploy = new Dictionary<IModuleItem, IScript>();

            // For each Native Module, zip up all the compiled lists of required assemblies to deploy
            foreach (var assembly in allAssemblyDependencies)
            {
                var ccm = assembly.Key;
                var assemblyPaths = assembly.Value;
                m_LocalDeploymentHandler.UpdateDeployStatus(ccm, "Zipping Native Module...", shouldLog: false);

                try
                {
                    // Flush the directory to ensure we are zipping the latest files.
                    var cloudModuleDir = Path.Combine(k_NativeModulesDirectory, ccm.name);
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
                    var result = await m_ModuleZipper.ZipCompilation(cloudModuleDir, k_NativeModulesDirectory,
                        Path.GetFileNameWithoutExtension(ccm.Name), cancellationToken);
                    var moduleToDeploy = EditorCloudCodeModuleDeploymentHandler.GenerateModule(ccm.name, result);
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
    }

    // A serializable class to hold the relevant JSON data from the .asmdef file
    [Serializable]
    class AsmdefJsonData
    {
        public string name;
        public string[] references;
        public string[] precompiledReferences;

        internal static AsmdefJsonData ParseAssemblyDefinitionAsset(AssemblyDefinitionAsset asmdefAsset)
        {
            // Read the JSON content from the file
            string assetPath = AssetDatabase.GetAssetPath(asmdefAsset);
            string jsonText = File.ReadAllText(assetPath);

            // Parse the JSON into our serializable structure
            return JsonUtility.FromJson<AsmdefJsonData>(jsonText);
        }
    }
}
