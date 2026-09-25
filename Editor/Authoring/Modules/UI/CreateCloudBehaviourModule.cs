#if UNITY_6000_5_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;

#if UNITY_6000_4_OR_NEWER
using BaseClass = UnityEditor.ProjectWindowCallback.AssetCreationEndAction;
using ActionIdentifier = UnityEngine.EntityId;
#else
using BaseClass = UnityEditor.ProjectWindowCallback.EndNameEditAction;
using ActionIdentifier = System.Int32;
#endif

using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;

using Unity.Services.CloudCode.Authoring.Editor.Core.Modules.Exceptions;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    class CreateCloudBehaviourModule : BaseClass
    {
        internal const string k_DefaultCloudBehaviourScriptName = "MyCloudBehaviour";
        internal const string k_CloudBehaviourClientTemplateName = "BehaviourModuleClientTemplate";
        internal const string k_CloudBehaviourCloudTemplateName = "BehaviourModuleCloudTemplate";

        const string k_ClientDirName = "Client";
        const string k_CloudDirName = "Cloud";

        static readonly string k_CloudCodeLocalAssetPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "Authoring", "Modules", "UI", "Assets");

        static readonly string k_CloudCodeTemplateAssetPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "Authoring", "Scripts", "Templates~");

        static readonly string k_CloudBehaviourScriptPath = PathUtils.Join(CloudCodePackage.RootPath,
            "CloudBehaviours", "CloudBehaviour.cs");

        static readonly string k_CloudCodeAssemblyApisPath = PathUtils.Join(CloudCodePackage.RootPath,
            "Runtime", "Unity.Services.CloudCode.Apis", "Unity.Services.CloudCode.Apis.asmdef");

        static readonly string k_CloudCodeAssemblyCorePath = PathUtils.Join(CloudCodePackage.RootPath,
            "Runtime", "Unity.Services.CloudCode.Core", "Unity.Services.CloudCode.Core.asmdef");

        static readonly string k_CloudBehavioursAsmdefPath = PathUtils.Join(CloudCodePackage.RootPath,
            "Editor", "CloudBehaviours", "Unity.Services.CloudBehaviours.Editor.asmdef");

        static readonly string k_CloudBehavioursRuntimeAsmdefPath = PathUtils.Join(CloudCodePackage.RootPath,
            "Runtime", "CloudBehaviours", "Unity.Services.CloudBehaviours.Runtime.asmdef");

        const string k_NewtonsoftAssemblyName = "Newtonsoft.Json.dll";

        // Lives in com.unity.services.core, so it is resolved by assembly name rather than a path
        // inside this package. The generated client binding names IUnityServices.
        const string k_ServicesCoreAssemblyName = "Unity.Services.Core";

        [MenuItem("Assets/Create/Services/Cloud Behaviour Script", false, 67)]
        public static void CreateModuleReferenceFile()
        {
#if UNITY_6000_4_OR_NEWER
            UnityEngine.EntityId instanceId = new UnityEngine.EntityId();
#else
            int instanceId = 0;
#endif

            var scriptIcon = (Texture2D)EditorGUIUtility.IconContent("cs Script Icon").image;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                instanceId,
                CreateInstance<CreateCloudBehaviourModule>(),
                k_DefaultCloudBehaviourScriptName,
                scriptIcon,
                null);
        }

        public override void Action(ActionIdentifier instanceId, string assetPath, string resourceFile)
        {
            ValidateAndCreateCloudBehaviourModule(assetPath);
        }

        internal void ValidateAndCreateCloudBehaviourModule(string assetPath)
        {
            var directoryPath = Path.GetDirectoryName(assetPath) !;
            var editActionScriptName = Path.GetFileName(assetPath);

            var asmdefsInDir = Directory.GetFiles(directoryPath, "*.asmdef");
            if (asmdefsInDir.Length > 1)
            {
                ShowErrorDialog(CloudBehaviourSetupError.MultipleAsmdefs);
                return;
            }
            if (asmdefsInDir.Length == 1 && !DirectoryHasCloudBehaviourAsmdef(directoryPath))
            {
                ShowErrorDialog(CloudBehaviourSetupError.AsmdefNotPartOfModule);
                return;
            }

            if (ShouldCreateNewCloudBehaviourModule(directoryPath))
            {
                // Module name starts empty; the creation window requires the user to enter it.
                editActionScriptName = GetUniqueSanitizedName(assetPath, ".cs");
                assetPath = PathUtils.Join(directoryPath, editActionScriptName);
                CreateCloudBehaviourModuleWindow.Show(
                    editActionScriptName,
                    assetPath,
                    string.Empty,
                    (moduleName, cloudScriptName, clientScriptName, cloudAssemblyName, clientAssemblyName) =>
                    {
                        return CreateNewCloudBehaviourModule(
                            directoryPath, moduleName, cloudScriptName,
                            clientScriptName, cloudAssemblyName, clientAssemblyName);
                    });
                return;
            }

            if (!IsValidCloudBehaviourServerDirectory(directoryPath, out var associatedModule))
            {
                return;
            }

            if (!ModuleHasValidAsmdefFiles(associatedModule))
            {
                return;
            }

            CreateClientAndServerCloudBehaviourScriptsOnly(associatedModule, editActionScriptName);
        }

        bool ShouldCreateNewCloudBehaviourModule(string directoryPath)
        {
            // The module is created in its own <ModuleName> child folder, so unrelated Cloud/Client folders
            // in the selected folder no longer imply a module lives here. Treat the folder as an existing
            // module (i.e. add-script flow) only when it holds a module asset or a matching Cloud Behaviour asmdef.
            return !DirectoryHasCloudBehaviourAsmdef(directoryPath) &&
                !DirectoryHasModule(directoryPath);
        }

        static bool DirectoryHasModule(string directoryPath)
        {
            // The presence of a module file means a module already lives here. A corrupt module that fails
            // to import must still block creating another over it, so this deliberately does not load the
            // asset (LoadAssetAtPath would return null for a malformed or not-yet-imported .ccmu).
            return Directory.GetFiles(directoryPath, "*" + CloudCodeModuleResources.FileExtension).Length > 0;
        }

        bool DirectoryHasCloudBehaviourAsmdef(string directoryPath)
        {
            string[] allAsmdefAtPath = Directory.GetFiles(directoryPath, "*.asmdef");
            if (allAsmdefAtPath.Length != 1)
            {
                return false;
            }

            var possibleAsmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(allAsmdefAtPath[0]);
            AsmdefJsonData jsonAsmdef = AsmdefJsonData.ParseAssemblyDefinitionAsset(possibleAsmdef);
            if (jsonAsmdef == null)
            {
                ShowErrorDialog(CloudBehaviourSetupError.ModuleAsmdefCorrupted);
                return false;
            }

            var allCloudBehaviourModules = CloudCodeAuthoringServices.Instance
                .GetService<CloudCodeModuleCollection>()
                .ToList();
            foreach (var moduleReference in allCloudBehaviourModules)
            {
                if (moduleReference.CloudAssemblyDefinition == null)
                    continue;

                var moduleAsmdef = AsmdefJsonData.ParseAssemblyDefinitionAsset(moduleReference.CloudAssemblyDefinition);
                if (moduleAsmdef == null || !AsmdefHasRequiredCloudBehaviourRefs(moduleAsmdef))
                    continue;

                if (jsonAsmdef.name.Equals(moduleReference.CloudAssemblyDefinition.name))
                {
                    return true;
                }

                if (moduleReference.ClientAssemblyDefinition != null &&
                    jsonAsmdef.name.Equals(moduleReference.ClientAssemblyDefinition.name))
                {
                    return true;
                }
            }

            return false;
        }

        string GetUniqueSanitizedName(string assetPath, string fileExtension)
        {
            var assetName = Path.GetFileName(assetPath);
            var assetDir = Path.GetDirectoryName(assetPath);

            int lastOccurrenceIndex = assetName.LastIndexOf(fileExtension, StringComparison.Ordinal);
            if (lastOccurrenceIndex != -1)
            {
                assetName = assetName.Remove(lastOccurrenceIndex, fileExtension.Length);
            }

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedAssetName = new string(assetName.Where(c => !invalidChars.Contains(c)).ToArray());

            if (fileExtension == ".cs")
            {
                sanitizedAssetName = ClassNameSanitizer.Sanitize(sanitizedAssetName);
            }

            var sanitizedPath = PathUtils.Join(assetDir !, sanitizedAssetName + fileExtension);
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(sanitizedPath);
            return Path.GetFileNameWithoutExtension(uniquePath);
        }

        internal bool CreateNewCloudBehaviourModule(
            string moduleDirPath, string moduleName, string scriptNameCloud,
            string scriptNameClient, string assemblyNameCloud, string assemblyNameClient)
        {
            string moduleReferencePath = null;
            string moduleClientPath = null;
            string moduleCloudPath = null;
            string createdModuleDirPath = null;
            string createdScriptPathCloud = null;
            bool createdClientDir = false;
            bool createdCloudDir = false;

            try
            {
                if (AssemblyNameConflicts(assemblyNameCloud, out string foundCloudPath))
                    throw new AssemblyNameConflictException(CloudBehaviourSetupMessages.AssemblyNameConflict(assemblyNameCloud, foundCloudPath));

                if (AssemblyNameConflicts(assemblyNameClient, out string foundClientPath))
                    throw new AssemblyNameConflictException(CloudBehaviourSetupMessages.AssemblyNameConflict(assemblyNameClient, foundClientPath));

                // Parent all module files under a folder named after the module. Reuse the folder if it
                // already exists; a folder that already contains a module is a conflict.
                var moduleDir = PathUtils.Join(moduleDirPath, moduleName);
                if (Directory.Exists(moduleDir))
                {
                    // Only reachable once the module has lost its asmdefs: an intact one trips
                    // the assembly name check above first.
                    if (DirectoryHasModule(moduleDir))
                        throw new ModuleAlreadyExistsException(CloudBehaviourSetupMessages.ModuleAlreadyExists(moduleDir));
                }
                else
                {
                    Directory.CreateDirectory(moduleDir);
                    createdModuleDirPath = moduleDir;
                }
                moduleDirPath = moduleDir;

                moduleReferencePath = PathUtils.Join(moduleDirPath, $"{moduleName}{CloudCodeModuleResources.FileExtension}");

                moduleClientPath = PathUtils.Join(moduleDirPath, k_ClientDirName);
                moduleCloudPath = PathUtils.Join(moduleDirPath, k_CloudDirName);
                createdClientDir = !Directory.Exists(moduleClientPath);
                createdCloudDir = !Directory.Exists(moduleCloudPath);
                Directory.CreateDirectory(moduleClientPath);
                Directory.CreateDirectory(moduleCloudPath);

                var(asmdefClient, asmdefPathClient) = CreateAssembly(assemblyNameClient, true, moduleDirPath);
                var(asmdefCloud, asmdefPathCloud) = CreateAssembly(assemblyNameCloud, false, moduleDirPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                asmdefClient.AddAssemblyReferenceAtPath(asmdefPathCloud);
                asmdefClient.SerializeToPath(asmdefPathClient);
                var asmdefReferenceCloud = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPathCloud);
                var asmdefReferenceClient = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPathClient);
                asmdefReferenceCloud.name = asmdefCloud.name;
                asmdefReferenceClient.name = asmdefClient.name;

                AsmdefJsonData jsonAsmdef = AsmdefJsonData.ParseAssemblyDefinitionAsset(asmdefReferenceCloud);
                // Validates the asmdef this flow just wrote from the template, so only a broken
                // package install reaches here.
                if (jsonAsmdef == null || jsonAsmdef.references == null || !AsmdefHasRequiredCloudBehaviourRefs(jsonAsmdef))
                {
                    throw new AssemblyMissingReferencesException(CloudBehaviourSetupMessages.AssemblyMissingReferences);
                }

                File.WriteAllText(moduleReferencePath, CloudCodeModule.ToJson(asmdefReferenceCloud, asmdefReferenceClient));

                createdScriptPathCloud =
                    CreateCloudBehaviourScript(moduleCloudPath, false, k_CloudBehaviourCloudTemplateName, scriptNameCloud, moduleName);
                var resolvedCloudClassName = Path.GetFileNameWithoutExtension(createdScriptPathCloud);
                var createdScriptPathClient =
                    CreateCloudBehaviourScript(moduleClientPath, true, k_CloudBehaviourClientTemplateName, scriptNameClient, moduleName, resolvedCloudClassName);

                if (string.IsNullOrEmpty(createdScriptPathCloud) || string.IsNullOrEmpty(createdScriptPathClient))
                {
                    throw new Exception("Cloud Behaviour script template was not found.");
                }
            }
            catch (Exception e)
            {
                // Only remove what this attempt created - a reused folder may hold unrelated user assets.
                // FileUtil.DeleteFileOrDirectory also removes paths not yet imported into the AssetDatabase.
                if (createdModuleDirPath != null)
                {
                    FileUtil.DeleteFileOrDirectory(createdModuleDirPath);
                }
                else
                {
                    if (createdClientDir)
                    {
                        FileUtil.DeleteFileOrDirectory(moduleClientPath);
                    }

                    if (createdCloudDir)
                    {
                        FileUtil.DeleteFileOrDirectory(moduleCloudPath);
                    }

                    if (moduleReferencePath != null && File.Exists(moduleReferencePath))
                    {
                        FileUtil.DeleteFileOrDirectory(moduleReferencePath);
                    }
                }

                ShowErrorDialog(CloudBehaviourSetupMessages.CreationFailure(e.Message));
                CloudCodeAuthoringServices.Instance.GetService<CloudModuleCreationAnalytics>()
                    .SendCloudCodeModuleCreatedEvent(
                        ModuleType.CloudBehaviour,
                        AnalyticsErrorCode.FromException(e),
                        AnalyticsErrorData.FromException(e));
                return false;
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            CloudCodeAuthoringServices.Instance.GetService<CloudModuleCreationAnalytics>().SendCloudCodeModuleCreatedEvent(ModuleType.CloudBehaviour);

            CloudCodeCreatedAssetFramer.FrameAfterReload(createdScriptPathCloud);

            return true;
        }

        string CreateCloudBehaviourScript(string scriptOutputPath, bool isClient, string templateName, string scriptName, string moduleName, string cloudClassName = null)
        {
            var fullAssetPath = PathUtils.Join(scriptOutputPath, $"{scriptName}.cs");
            var sanitizedName = GetUniqueSanitizedName(fullAssetPath, ".cs");
            var scriptDir = isClient ? k_ClientDirName : k_CloudDirName;
            var scriptTemplatePath = PathUtils.Join(k_CloudCodeTemplateAssetPath, scriptDir, $"{templateName}.cs");
            if (!File.Exists(scriptTemplatePath))
            {
                return string.Empty;
            }

            var scriptTemplateRaw = File.ReadAllText(scriptTemplatePath);
            var scriptTemplateNew = scriptTemplateRaw.Replace(templateName, sanitizedName);
            if (isClient && cloudClassName != null)
            {
                var sanitizedCloudClassName = ClassNameSanitizer.Sanitize(cloudClassName);
                scriptTemplateNew = scriptTemplateNew.Replace(k_CloudBehaviourCloudTemplateName, sanitizedCloudClassName);
            }

            var sanitizedModuleName = ClassNameSanitizer.Sanitize(moduleName);
            var scriptNamespace = $"{sanitizedModuleName}.{(isClient ? "Client" : "Cloud")}";
            scriptTemplateNew = scriptTemplateNew.Replace("__NAMESPACE__", scriptNamespace);
            if (isClient)
            {
                var cloudNamespace = $"{sanitizedModuleName}.Cloud";
                scriptTemplateNew = scriptTemplateNew.Replace("__CLOUD_NAMESPACE__", cloudNamespace);
            }

            var scriptDestPath = PathUtils.Join(scriptOutputPath, $"{sanitizedName}.cs");
            File.WriteAllText(scriptDestPath, scriptTemplateNew);
            return scriptDestPath;
        }

#if MULTIPLAYER_AVAILABLE
        // The generated client names these only when a behaviour targets Scope.MultiplayerSession.
        // Added at creation time rather than in the template, because in a project without
        // com.unity.services.multiplayer neither assembly exists and Unity refuses to compile an
        // asmdef that references a missing assembly.
        static readonly string[] k_MultiplayerClientReferences =
        {
            "Unity.Services.CloudBehaviours.Multiplayer.Runtime",
            "Unity.Services.Multiplayer"
        };
#endif

        (AsmdefJsonData, string) CreateAssembly(string assemblyName, bool isClient, string moduleRootPath)
        {
            var assemblyDir = isClient ? k_ClientDirName : k_CloudDirName;
            var template = isClient ? k_CloudBehaviourClientTemplateName : k_CloudBehaviourCloudTemplateName;
            var assemblyTemplatePath = PathUtils.Join(k_CloudCodeTemplateAssetPath, assemblyDir, $"{template}.asmdef");
            var asmdefTemplate = AsmdefJsonData.DeserializeFromPath(assemblyTemplatePath);
            asmdefTemplate.name = assemblyName;

#if MULTIPLAYER_AVAILABLE
            if (isClient)
            {
                asmdefTemplate.references = asmdefTemplate.references
                    .Concat(k_MultiplayerClientReferences.Except(asmdefTemplate.references))
                    .ToArray();
            }
#endif

            var assemblyDestPath = PathUtils.Join(moduleRootPath, assemblyDir, $"{assemblyName}.asmdef");
            asmdefTemplate.SerializeToPath(assemblyDestPath);
            return (asmdefTemplate, assemblyDestPath);
        }

        bool IsValidCloudBehaviourServerDirectory(string directoryPath, out CloudCodeModule foundModule)
        {
            foundModule = null;

            string[] allAsmdefAtPath = Directory.GetFiles(directoryPath, "*.asmdef");
            if (allAsmdefAtPath.Length == 0)
            {
                ShowErrorDialog(CloudBehaviourSetupError.ScriptRequiresCloudAsmdef);
                return false;
            }

            if (allAsmdefAtPath.Length > 1)
            {
                ShowErrorDialog(CloudBehaviourSetupError.MultipleAsmdefs);
                return false;
            }

            var possibleAsmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(allAsmdefAtPath[0]);
            AsmdefJsonData jsonAsmdef = AsmdefJsonData.ParseAssemblyDefinitionAsset(possibleAsmdef);
            if (jsonAsmdef == null)
            {
                ShowErrorDialog(CloudBehaviourSetupError.ModuleAsmdefCorrupted);
                return false;
            }

            var allCloudBehaviourModules = CloudCodeAuthoringServices.Instance.GetService
                <CloudCodeModuleCollection>().ToList();
            foreach (var moduleReference in allCloudBehaviourModules)
            {
                if (moduleReference.CloudAssemblyDefinition != null &&
                    jsonAsmdef.name.Equals(moduleReference.CloudAssemblyDefinition.name))
                {
                    foundModule = moduleReference;
                    return true;
                }

                if (moduleReference.ClientAssemblyDefinition != null &&
                    jsonAsmdef.name.Equals(moduleReference.ClientAssemblyDefinition.name))
                {
                    ShowErrorDialog(CloudBehaviourSetupError.ScriptAddedInClientDir);
                    return false;
                }
            }

            ShowErrorDialog(CloudBehaviourSetupError.AsmdefNotPartOfModule);
            return false;
        }

        bool ModuleHasValidAsmdefFiles(CloudCodeModule moduleReference)
        {
            var serverPath = AssetDatabase.GetAssetPath(moduleReference.CloudAssemblyDefinition);
            var clientPath = AssetDatabase.GetAssetPath(moduleReference.ClientAssemblyDefinition);
            if (string.IsNullOrEmpty(clientPath) || !File.Exists(clientPath))
            {
                ShowErrorDialog(CloudBehaviourSetupError.ClientAsmdefMisconfigured);
                return false;
            }

            if (string.IsNullOrEmpty(serverPath) || !File.Exists(serverPath))
            {
                ShowErrorDialog(CloudBehaviourSetupError.ServerAsmdefMisconfigured);
                return false;
            }

            AsmdefJsonData jsonAsmdefClient = AsmdefJsonData.ParseAssemblyDefinitionAsset(moduleReference.ClientAssemblyDefinition);
            if (jsonAsmdefClient == null)
            {
                ShowErrorDialog(CloudBehaviourSetupError.ModuleAsmdefCorrupted);
                return false;
            }
            if (jsonAsmdefClient.references == null)
            {
                ShowErrorDialog(CloudBehaviourSetupError.ClientAsmdefMisconfigured);
                return false;
            }

            AsmdefJsonData jsonAsmdefServer = AsmdefJsonData.ParseAssemblyDefinitionAsset(moduleReference.CloudAssemblyDefinition);
            if (jsonAsmdefServer == null)
            {
                ShowErrorDialog(CloudBehaviourSetupError.ModuleAsmdefCorrupted);
                return false;
            }
            if (jsonAsmdefServer.references == null)
            {
                ShowErrorDialog(CloudBehaviourSetupError.ServerAsmdefMisconfigured);
                return false;
            }

            var includedPlatforms = jsonAsmdefServer.includePlatforms;
            var editorOnly = includedPlatforms != null && includedPlatforms.Length == 1 && includedPlatforms.Contains("Editor");
            var noEngineRefsAndEditorOnly = jsonAsmdefServer.noEngineReferences && editorOnly;
            if (!AsmdefHasRequiredCloudBehaviourRefs(jsonAsmdefServer) || !noEngineRefsAndEditorOnly || jsonAsmdefServer.autoReferenced)
            {
                ShowErrorDialog(CloudBehaviourSetupError.ServerAsmdefMisconfigured);
                return false;
            }

            if (!jsonAsmdefClient.HasAssemblyReferenceAtPath(serverPath))
            {
                ShowErrorDialog(CloudBehaviourSetupError.ClientAsmdefMisconfigured);
                return false;
            }

            return true;
        }

        (string CloudScript, string ClientScript) CreateClientAndServerCloudBehaviourScriptsOnly(
            CloudCodeModule foundModule, string editActionScriptName)
        {
            var serverAsmdefPath = AssetDatabase.GetAssetPath(foundModule.CloudAssemblyDefinition);
            var clientAsmdefPath = AssetDatabase.GetAssetPath(foundModule.ClientAssemblyDefinition);
            var serverDirectory = Path.GetDirectoryName(serverAsmdefPath);
            var clientDirectory = Path.GetDirectoryName(clientAsmdefPath);

            string createdServerPath = null;
            string createdClientPath = null;
            try
            {
                var moduleAssetPath = AssetDatabase.GetAssetPath(foundModule);
                var moduleName = Path.GetFileNameWithoutExtension(moduleAssetPath);

                createdServerPath = CreateCloudBehaviourScript(
                    serverDirectory, false, k_CloudBehaviourCloudTemplateName, editActionScriptName, moduleName);
                var resolvedServerClassName = Path.GetFileNameWithoutExtension(createdServerPath);
                createdClientPath = CreateCloudBehaviourScript(
                    clientDirectory, true, k_CloudBehaviourClientTemplateName, $"{editActionScriptName}Client", moduleName, resolvedServerClassName);
            }
            catch (Exception e)
            {
                Debug.LogError($"An error occurred while creating a Cloud Behaviour script {e.Message}");

                if (createdServerPath != null && File.Exists(createdServerPath))
                {
                    File.Delete(createdServerPath);
                }

                if (createdClientPath != null && File.Exists(createdClientPath))
                {
                    File.Delete(createdClientPath);
                }

                CloudCodeAuthoringServices.Instance.GetService<CloudModuleCreationAnalytics>()
                    .SendCloudCodeScriptAddedEvent(
                        ModuleType.CloudBehaviour,
                        AnalyticsErrorCode.FromException(e),
                        AnalyticsErrorData.FromException(e));
                throw;
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            CloudCodeAuthoringServices.Instance.GetService<CloudModuleCreationAnalytics>().SendCloudCodeScriptAddedEvent(ModuleType.CloudBehaviour);

            return (createdServerPath, createdClientPath);
        }

        /// <summary>
        /// Adds a Cloud Behaviour cloud/client script pair to an existing module, first giving the
        /// module's assembly definitions any Cloud Behaviour references they are missing — a module
        /// authored as a plain Cloud Code module carries the Core/Apis references but not the
        /// Cloud Behaviour ones. Returns false, with the reason on <see cref="ShowErrorDialog"/>,
        /// when the module's assemblies are not in a state a script can be added to.
        /// </summary>
        internal bool TryAddCloudBehaviourToModule(
            CloudCodeModule module, string behaviourName, out (string CloudScript, string ClientScript) created)
        {
            created = default;

            // The references go in before the check, because a module authored as a plain Cloud Code
            // module is missing exactly the ones the check requires and adding them is the point of this
            // call. That makes the asmdefs the first thing mutated, so snapshot them: a module the check
            // still rejects, or a script pair that fails to write, must not be left half-converted to a
            // Cloud Behaviour module by a call that reported failure.
            var asmdefBackup = ReadAsmdefs(module);
            try
            {
                EnsureCloudBehaviourReferences(module);

                if (!ModuleHasValidAsmdefFiles(module))
                {
                    RestoreAsmdefs(asmdefBackup);
                    return false;
                }

                created = CreateClientAndServerCloudBehaviourScriptsOnly(module, behaviourName);
                return true;
            }
            catch
            {
                RestoreAsmdefs(asmdefBackup);
                throw;
            }
        }

        static List<(string Path, string Contents)> ReadAsmdefs(CloudCodeModule module)
        {
            var paths = new[]
            {
                AssetDatabase.GetAssetPath(module.CloudAssemblyDefinition),
                AssetDatabase.GetAssetPath(module.ClientAssemblyDefinition)
            };

            return paths
                .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path))
                .Select(path => (path, File.ReadAllText(path)))
                .ToList();
        }

        static void RestoreAsmdefs(List<(string Path, string Contents)> backup)
        {
            var restored = false;
            foreach (var(path, contents) in backup)
            {
                if (!File.Exists(path) || File.ReadAllText(path) == contents)
                    continue;

                File.WriteAllText(path, contents);
                restored = true;
            }

            if (restored)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        /// <summary>
        /// Adds the Cloud Behaviour assembly references the module is missing, on both sides, and
        /// re-imports so the new references are in effect. A no-op for a module that already has them.
        /// </summary>
        void EnsureCloudBehaviourReferences(CloudCodeModule module)
        {
            var cloudPath = AssetDatabase.GetAssetPath(module.CloudAssemblyDefinition);
            var clientPath = AssetDatabase.GetAssetPath(module.ClientAssemblyDefinition);

            var cloudChanged = AddMissingReferences(cloudPath, new[]
            {
                k_CloudCodeAssemblyApisPath, k_CloudCodeAssemblyCorePath, k_CloudBehavioursAsmdefPath
            });

            // The generated cloud-side system serializes through Newtonsoft, and the cloud assembly
            // overrides its references, so the precompiled one has to be named explicitly.
            cloudChanged |= AddMissingPrecompiledReference(cloudPath, k_NewtonsoftAssemblyName);

            var clientReferences = new List<string> { k_CloudBehavioursRuntimeAsmdefPath };
            var servicesCorePath =
                CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(k_ServicesCoreAssemblyName);
            if (!string.IsNullOrEmpty(servicesCorePath))
                clientReferences.Add(servicesCorePath);

            var clientChanged = AddMissingReferences(clientPath, clientReferences.ToArray());

            if (cloudChanged || clientChanged)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        static bool AddMissingPrecompiledReference(string asmdefPath, string assemblyFileName)
        {
            var asmdef = AsmdefJsonData.DeserializeFromPath(asmdefPath);
            if (asmdef == null || !asmdef.overrideReferences)
                return false;

            var existing = asmdef.precompiledReferences ?? Array.Empty<string>();
            if (existing.Contains(assemblyFileName))
                return false;

            asmdef.precompiledReferences = existing.Append(assemblyFileName).ToArray();
            asmdef.SerializeToPath(asmdefPath);
            return true;
        }

        static bool AddMissingReferences(string asmdefPath, IReadOnlyList<string> referencePaths)
        {
            var asmdef = AsmdefJsonData.DeserializeFromPath(asmdefPath);
            if (asmdef == null)
                return false;

            var added = false;
            foreach (var referencePath in referencePaths)
            {
                if (asmdef.HasAssemblyReferenceAtPath(referencePath))
                    continue;

                asmdef.AddAssemblyReferenceAtPath(referencePath);
                added = true;
            }

            if (added)
                asmdef.SerializeToPath(asmdefPath);

            return added;
        }

        bool AsmdefHasRequiredCloudBehaviourRefs(AsmdefJsonData jsonAsmdef)
        {
            return jsonAsmdef.HasAssemblyReferenceAtPath(k_CloudCodeAssemblyApisPath)
                && jsonAsmdef.HasAssemblyReferenceAtPath(k_CloudCodeAssemblyCorePath)
                && jsonAsmdef.HasAssemblyReferenceAtPath(k_CloudBehavioursAsmdefPath);
        }

        bool AssemblyNameConflicts(string assemblyName, out string existingAsmdefPath)
        {
            existingAsmdefPath = null;
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            var guids = AssetDatabase.FindAssets("t:AssemblyDefinitionAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asmdef = AsmdefJsonData.DeserializeFromPath(path);
                if (asmdef != null && asmdef.name == assemblyName)
                {
                    existingAsmdefPath = path;
                    return true;
                }
            }

            return false;
        }

        void ShowErrorDialog(CloudBehaviourSetupError error)
        {
            ShowErrorDialog(CloudBehaviourSetupMessages.Get(error));
        }

        protected virtual void ShowErrorDialog(CloudBehaviourSetupMessage message)
        {
            CloudCodeDialogWindow.Show(
                CloudBehaviourSetupMessages.WindowTitle,
                message.Title,
                message.Body,
                documentationUrl: message.DocumentationUrl);
        }
    }
}
#endif
