#if UNITY_6000_5_OR_NEWER
using System;
using System.IO;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

#if UNITY_6000_4_OR_NEWER
using BaseClass = UnityEditor.ProjectWindowCallback.AssetCreationEndAction;
using ActionIdentifier = UnityEngine.EntityId;
#else
using BaseClass = UnityEditor.ProjectWindowCallback.EndNameEditAction;
using ActionIdentifier = System.Int32;
#endif

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    class CreateCloudCodeModule : BaseClass
    {
        // Const for referencing asset and template paths
        internal const string k_DefaultCloudCodeScriptName = "MyCloudCodeScript";
        internal const string k_CloudCodeClientTemplateName = "CloudCodeModuleClientTemplate";
        internal const string k_CloudCodeCloudTemplateName = "CloudCodeModuleCloudTemplate";
        internal const string k_CloudCodeAssemblyTemplateName = "CloudCodeModuleAssemblyTemplate";

        const string k_ClientDirName = "Client";
        const string k_CloudDirName = "Cloud";

        static readonly string k_CloudCodeLocalAssetPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "Authoring", "Modules", "UI", "Assets");

        static readonly string k_CloudCodeTemplateAssetPath =
            PathUtils.Join(CloudCodePackage.EditorPath, "Authoring", "Scripts", "Templates~");

        static readonly string k_CloudCodeModuleDefinitionPath = PathUtils.Join(CloudCodePackage.EditorPath,
            "Authoring", "Modules", "CloudCodeModule.cs");

        static readonly string k_CloudCodeAssemblyApisPath = PathUtils.Join(CloudCodePackage.RootPath,
            "Runtime", "Unity.Services.CloudCode.Apis", "Unity.Services.CloudCode.Apis.asmdef");

        static readonly string k_CloudCodeAssemblyCorePath  = PathUtils.Join(CloudCodePackage.RootPath,
            "Runtime", "Unity.Services.CloudCode.Core", "Unity.Services.CloudCode.Core.asmdef");

        [MenuItem("Assets/Create/Services/Cloud Code Module Script", false, 68)]
        public static void CreateModuleFile()
        {
            // We use ActionIdentifier so the alias automatically
            // resolves to the correct type based on the Unity version.
            // ReSharper disable once PreferConcreteValueOverDefault
            // ReSharper disable once ConvertToConstant.Local
            var instanceId = new ActionIdentifier();

            var scriptIcon = (Texture2D)EditorGUIUtility.IconContent("cs Script Icon").image;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                instanceId,
                CreateInstance<CreateCloudCodeModule>(),
                k_DefaultCloudCodeScriptName,
                scriptIcon,
                null);
        }

        // Called when the User finishes Name editing of the cloud code modules script
        public override void Action(ActionIdentifier instanceId, string assetPath, string resourceFile)
        {
            ValidateAndCreateCloudCodeModule(assetPath);
        }

        internal void ValidateAndCreateCloudCodeModule(string assetPath)
        {
            var directoryPath = Path.GetDirectoryName(assetPath) !;
            var editActionScriptName = Path.GetFileName(assetPath);

            if (ShouldCreateNewCloudCodeModule(directoryPath))
            {
                // Module name starts empty; the creation window requires the user to enter it.
                editActionScriptName = GetUniqueSanitizedName(assetPath, ".cs");
                assetPath = PathUtils.Join(directoryPath, editActionScriptName);
                CreateCloudCodeModuleWindow.Show(editActionScriptName, assetPath, string.Empty,
                    (moduleName, cloudScriptName, clientScriptName, cloudAssemblyName, clientAssemblyName) =>
                    {
                        return CreateNewCloudCodeModule(directoryPath, moduleName, cloudScriptName,
                            clientScriptName, cloudAssemblyName, clientAssemblyName);
                    });
                return;
            }

            // Else check for situation where the User be attempting to Add a new Cloud Code Script.
            // Module Scripts can ONLY be at the same directory level as the Server Asmdef.
            if (!IsValidCloudCodeServerDirectory(directoryPath, out var associatedModule))
                return;

            // From the found Cloud Code Module, verify the Client + Server Asmdef configurations.
            if (!ModuleHasValidAsmdefFiles(associatedModule))
                return;

            CreateClientAndServerScriptsOnly(associatedModule, editActionScriptName);
        }

        bool ShouldCreateNewCloudCodeModule(string directoryPath)
        {
            // The module is created in its own <ModuleName> child folder, so unrelated Cloud/Client folders
            // in the selected folder no longer imply a module lives here. Treat the folder as an existing
            // module (i.e. add-script flow) only when it holds a module asset or a matching Cloud Code asmdef.
            return !DirectoryHasCloudCodeAsmdef(directoryPath) && !DirectoryHasModule(directoryPath);
        }

        static bool DirectoryHasModule(string directoryPath)
        {
            // The presence of a module file means a module already lives here. A corrupt module that fails
            // to import must still block creating another over it, so this deliberately does not load the
            // asset (LoadAssetAtPath would return null for a malformed or not-yet-imported .ccmu).
            return Directory.GetFiles(directoryPath, "*" + CloudCodeModuleResources.FileExtension).Length > 0;
        }

        bool DirectoryHasCloudCodeAsmdef(string directoryPath)
        {
            // Verify directory for missing Asmdefs
            string[] allAsmdefAtPath = Directory.GetFiles(directoryPath, "*.asmdef");
            if (allAsmdefAtPath.Length != 1)
                return false;

            // Grab the AsmdefJsonData for the Asmdef in question
            var possibleAsmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(allAsmdefAtPath[0]);
            AsmdefJsonData jsonAsmdef = AsmdefJsonData.ParseAssemblyDefinitionAsset(possibleAsmdef);
            if (jsonAsmdef == null)
            {
                ShowErrorDialog(CloudCodeSetupError.ModuleAsmdefCorrupted);
                return false;
            }

            // With valid Server asmdef, cross check against known Cloud Code Modules.
            var allCloudCodeModules = CloudCodeAuthoringServices.Instance.GetService
                <CloudCodeModuleCollection>().ToList();
            foreach (var cloudCodeModule in allCloudCodeModules)
            {
                if (cloudCodeModule.CloudAssemblyDefinition == null ||
                    cloudCodeModule.ClientAssemblyDefinition == null)
                {
                    Debug.LogWarning($"Encountered Corrupted Client or Cloud Assembly definition! " +
                        $"Cloud Code Module: {cloudCodeModule.name}");
                    continue;
                }

                if (jsonAsmdef.HasNameThatMatches(cloudCodeModule.CloudAssemblyDefinition) ||
                    jsonAsmdef.HasNameThatMatches(cloudCodeModule.ClientAssemblyDefinition))
                    return true;
            }

            return false;
        }

        string GetUniqueSanitizedName(string assetPath, string fileExtension)
        {
            var assetName = Path.GetFileName(assetPath);
            var assetDir = Path.GetDirectoryName(assetPath);

            // If the name includes a file Extension, emulate Editor behavior and remove.
            int lastOccurrenceIndex = assetName.LastIndexOf(fileExtension, StringComparison.Ordinal);
            if (lastOccurrenceIndex != -1)
                assetName =  assetName.Remove(lastOccurrenceIndex, fileExtension.Length);

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitizedAssetName = new string(assetName.Where(c => !invalidChars.Contains(c)).ToArray());

            if (fileExtension == ".cs")
            {
                sanitizedAssetName = ClassNameSanitizer.Sanitize(sanitizedAssetName);
            }

            var sanitizedPath  = PathUtils.Join(assetDir !, sanitizedAssetName + fileExtension);
            var uniquePath = AssetDatabase.GenerateUniqueAssetPath(sanitizedPath);
            return Path.GetFileNameWithoutExtension(uniquePath);
        }

        internal bool CreateNewCloudCodeModule(string moduleDirPath, string moduleName, string scriptNameCloud,
            string scriptNameClient, string assemblyNameCloud, string assemblyNameClient)
        {
            string modulePath = null;
            string moduleClientPath = null;
            string moduleCloudPath = null;
            string createdModuleDirPath = null;
            string createdScriptPathCloud = null;
            bool createdClientDir = false;
            bool createdCloudDir = false;

            // Attempt creation of all Cloud Code Script and dependencies.
            // On failure, ensure a clean state by wiping out any transient created artifacts.
            try
            {
                // First check for duplicate Module Names
                if (AssemblyNameConflicts(assemblyNameCloud, out string foundCloudPath))
                    throw new Exception(CloudCodeSetupMessages.AssemblyNameConflict(assemblyNameCloud, foundCloudPath));

                if (AssemblyNameConflicts(assemblyNameClient, out string foundClientPath))
                    throw new Exception(CloudCodeSetupMessages.AssemblyNameConflict(assemblyNameClient, foundClientPath));

                // Parent all module files under a folder named after the module. Reuse the folder if it
                // already exists; a folder that already contains a module is a conflict.
                var moduleDir = PathUtils.Join(moduleDirPath, moduleName);
                if (Directory.Exists(moduleDir))
                {
                    if (DirectoryHasModule(moduleDir))
                        throw new Exception(CloudCodeSetupMessages.ModuleAlreadyExists(moduleName, moduleDir));
                }
                else
                {
                    Directory.CreateDirectory(moduleDir);
                    createdModuleDirPath = moduleDir;
                }
                moduleDirPath = moduleDir;

                modulePath = PathUtils.Join(moduleDirPath, $"{moduleName}{CloudCodeModuleResources.FileExtension}");

                // Create both Client and Cloud Directories in preparation for modules.
                moduleClientPath = PathUtils.Join(moduleDirPath, k_ClientDirName);
                moduleCloudPath = PathUtils.Join(moduleDirPath, k_CloudDirName);
                createdClientDir = !Directory.Exists(moduleClientPath);
                createdCloudDir = !Directory.Exists(moduleCloudPath);
                Directory.CreateDirectory(moduleClientPath);
                Directory.CreateDirectory(moduleCloudPath);

                // Create both Client and Cloud Assemblies in corresponding directories
                var(asmdefClient, asmdefPathClient) = CreateAssembly(assemblyNameClient, true, moduleDirPath);
                var(asmdefCloud, asmdefPathCloud) = CreateAssembly(assemblyNameCloud, false, moduleDirPath);

                // Save the assemblies so they get a uniquely assigned Unity GUID to reference within the module.
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                // Link the Client Asmdef to the Cloud one and save it.
                asmdefClient.AddAssemblyReferenceAtPath(asmdefPathCloud);
                asmdefClient.SerializeToPath(asmdefPathClient);
                var asmdefReferenceCloud = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPathCloud);
                var asmdefReferenceClient = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPathClient);
                asmdefReferenceCloud.name = asmdefCloud.name;
                asmdefReferenceClient.name = asmdefClient.name;

                // Sanity check if in case the template is broken
                AsmdefJsonData jsonAsmdef = AsmdefJsonData.ParseAssemblyDefinitionAsset(asmdefReferenceCloud);
                if (jsonAsmdef == null || jsonAsmdef.references == null || !AsmdefHasRequiredCoreApiRef(jsonAsmdef))
                    throw new Exception(CloudCodeSetupMessages.AssemblyMissingReferences);

                // Create the Cloud Code module. As it is a custom asset (CloudCodeModuleImporter
                // ScriptedImporter), the JSON is written to disk and the asset is built by the importer.
                File.WriteAllText(modulePath, CloudCodeModule.ToJson(asmdefReferenceCloud, asmdefReferenceClient));

                // Create the Scripts
                var fullAssetPathCloud = PathUtils.Join(moduleCloudPath, $"{scriptNameCloud}.cs");
                var sanitizedNameCloud = GetUniqueSanitizedName(fullAssetPathCloud, ".cs");

                var fullAssetPathClient = PathUtils.Join(moduleClientPath, $"{scriptNameClient}.cs");
                var sanitizedNameClient = GetUniqueSanitizedName(fullAssetPathClient, ".cs");

                var sanitizedNamespace = NamespaceSanitizer.Sanitize(asmdefCloud.name);

                createdScriptPathCloud = CreateCloudCodeScript(moduleCloudPath, false, k_CloudCodeClientTemplateName,
                    k_CloudCodeCloudTemplateName, sanitizedNameClient, sanitizedNameCloud, sanitizedNamespace);
                CreateCloudCodeScript(moduleClientPath, true, k_CloudCodeClientTemplateName,
                    k_CloudCodeCloudTemplateName, sanitizedNameClient, sanitizedNameCloud, sanitizedNamespace);
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
                        FileUtil.DeleteFileOrDirectory(moduleClientPath);

                    if (createdCloudDir)
                        FileUtil.DeleteFileOrDirectory(moduleCloudPath);

                    if (modulePath != null && File.Exists(modulePath))
                        FileUtil.DeleteFileOrDirectory(modulePath);
                }

                Debug.LogError($"Error when creating a new Cloud Code module: {e.Message}");
                ShowErrorDialog(CloudCodeSetupMessages.CreationFailure(e.Message));
                return false;
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            CloudCodeAuthoringServices.Instance.GetService<CloudModuleCreationAnalytics>().SendCloudCodeModuleCreatedEvent();

            CloudCodeCreatedAssetFramer.FrameAfterReload(createdScriptPathCloud);

            return true;
        }

        string CreateCloudCodeScript(string scriptOutputPath, bool isClient, string clientTemplateName,
            string serverTemplateName, string sanitizedClientName, string sanitizedServerName, string sanitizedNamespace)
        {
            // Copy the Cloud Code Script template and insert the user chosen script name.
            var scriptDir = isClient ? k_ClientDirName : k_CloudDirName;
            var templateName = isClient ? clientTemplateName : serverTemplateName;
            var targetName = isClient ? sanitizedClientName : sanitizedServerName;
            var scriptTemplatePath = PathUtils.Join(k_CloudCodeTemplateAssetPath, scriptDir, $"{templateName}.cs");
            var scriptTemplateRaw = File.ReadAllText(scriptTemplatePath);

            // Templates can have both Cloud and Client replacements within them
            var scriptTemplateNew = scriptTemplateRaw.Replace(clientTemplateName, sanitizedClientName);
            scriptTemplateNew = scriptTemplateNew.Replace(serverTemplateName, sanitizedServerName);
            scriptTemplateNew = scriptTemplateNew.Replace(k_CloudCodeAssemblyTemplateName, sanitizedNamespace);

            var scriptDestPath = PathUtils.Join(scriptOutputPath, $"{targetName}.cs");
            File.WriteAllText(scriptDestPath, scriptTemplateNew);
            return scriptDestPath;
        }

        (AsmdefJsonData, string) CreateAssembly(string assemblyName, bool isClient, string moduleRootPath)
        {
            var assemblyDir = isClient ? k_ClientDirName : k_CloudDirName;
            var template = isClient ? k_CloudCodeClientTemplateName : k_CloudCodeCloudTemplateName;

            // Grab the template Assembly to duplicate
            var assemblyTemplatePath = PathUtils.Join(k_CloudCodeTemplateAssetPath, assemblyDir, $"{template}.asmdef");
            var asmdefTemplate = AsmdefJsonData.DeserializeFromPath(assemblyTemplatePath);

            // Assign the template the user given name of the module and copy it
            asmdefTemplate.name = assemblyName;
            var assemblyDestPath = PathUtils.Join(moduleRootPath, assemblyDir, $"{assemblyName}.asmdef");
            asmdefTemplate.SerializeToPath(assemblyDestPath);

            return (asmdefTemplate, assemblyDestPath);
        }

        bool IsValidCloudCodeServerDirectory(string directoryPath, out CloudCodeModule foundModule)
        {
            foundModule = null;

            // Verify directory for missing Asmdefs (improper setup)
            string[] allAsmdefAtPath = Directory.GetFiles(directoryPath, "*.asmdef");
            if (allAsmdefAtPath.Length == 0)
            {
                ShowErrorDialog(CloudCodeSetupError.ScriptRequiresCloudAsmdef);
                return false;
            }

            // Verify directory for multiple Asmdefs (improper setup)
            if (allAsmdefAtPath.Length > 1)
            {
                ShowErrorDialog(CloudCodeSetupError.MultipleAsmdefs);
                return false;
            }

            // Grab the AsmdefJsonData for the Asmdef in question
            var possibleAsmdef = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(allAsmdefAtPath[0]);
            AsmdefJsonData jsonAsmdef = AsmdefJsonData.ParseAssemblyDefinitionAsset(possibleAsmdef);
            if (jsonAsmdef == null)
            {
                ShowErrorDialog(CloudCodeSetupError.ModuleAsmdefCorrupted);
                return false;
            }

            // Verify if the Asmdef is a Cloud Code Associated one.
            var allCloudCodeModules = CloudCodeAuthoringServices.Instance.GetService
                <CloudCodeModuleCollection>().ToList();
            foreach (var cloudCodeModule in allCloudCodeModules)
            {
                if (cloudCodeModule.CloudAssemblyDefinition == null ||
                    cloudCodeModule.ClientAssemblyDefinition == null)
                {
                    Debug.LogWarning($"Encountered Corrupted Client or Cloud Assembly definition! " +
                        $"Cloud Code Module: {cloudCodeModule.name}");
                    continue;
                }

                if (jsonAsmdef.HasNameThatMatches(cloudCodeModule.CloudAssemblyDefinition))
                {
                    foundModule = cloudCodeModule;
                    return true;
                }

                if (jsonAsmdef.HasNameThatMatches(cloudCodeModule.ClientAssemblyDefinition))
                {
                    ShowErrorDialog(CloudCodeSetupError.ScriptAddedInClientDir);
                    return false;
                }
            }

            // If we have reached this point, the Asmdef Is not a Cloud Code one, prompt user to create new module.
            ShowErrorDialog(CloudCodeSetupError.AsmdefNotPartOfModule);
            return false;
        }

        bool ModuleHasValidAsmdefFiles(CloudCodeModule module)
        {
            var serverPath = AssetDatabase.GetAssetPath(module.CloudAssemblyDefinition);
            var clientPath = AssetDatabase.GetAssetPath(module.ClientAssemblyDefinition);
            if (string.IsNullOrEmpty(clientPath) || !File.Exists(clientPath))
            {
                ShowErrorDialog(CloudCodeSetupError.ClientAsmdefMisconfigured);
                return false;
            }

            if (string.IsNullOrEmpty(serverPath) || !File.Exists(serverPath))
            {
                ShowErrorDialog(CloudCodeSetupError.ServerAsmdefMisconfigured);
                return false;
            }

            AsmdefJsonData jsonAsmdefClient = AsmdefJsonData.ParseAssemblyDefinitionAsset(module.ClientAssemblyDefinition);
            if (jsonAsmdefClient == null)
            {
                ShowErrorDialog(CloudCodeSetupError.ModuleAsmdefCorrupted);
                return false;
            }
            if (jsonAsmdefClient.references == null)
            {
                ShowErrorDialog(CloudCodeSetupError.ClientAsmdefMisconfigured);
                return false;
            }

            AsmdefJsonData jsonAsmdefServer = AsmdefJsonData.ParseAssemblyDefinitionAsset(module.CloudAssemblyDefinition);
            if (jsonAsmdefServer == null)
            {
                ShowErrorDialog(CloudCodeSetupError.ModuleAsmdefCorrupted);
                return false;
            }
            if (jsonAsmdefServer.references == null)
            {
                ShowErrorDialog(CloudCodeSetupError.ServerAsmdefMisconfigured);
                return false;
            }

            // Server Asmdef must be Editor Only and have correct API Refs
            // TODO - Ensure the server and Client has Source Gen reference
            var includedPlatforms = jsonAsmdefServer.includePlatforms;
            var editorOnly = includedPlatforms.Length == 1 && includedPlatforms.Contains("Editor");
            var noEngineRefsAndEditorOnly = jsonAsmdefServer.noEngineReferences && editorOnly;
            if (!AsmdefHasRequiredCoreApiRef(jsonAsmdefServer) || !noEngineRefsAndEditorOnly || jsonAsmdefServer.autoReferenced)
            {
                ShowErrorDialog(CloudCodeSetupError.ServerAsmdefMisconfigured);
                return false;
            }

            // Ensure the Client Asmdef has a reference to the server one.
            if (!jsonAsmdefClient.HasAssemblyReferenceAtPath(serverPath))
            {
                ShowErrorDialog(CloudCodeSetupError.ClientAsmdefMisconfigured);
                return false;
            }
            return true;
        }

        void CreateClientAndServerScriptsOnly(CloudCodeModule foundModule, string editActionScriptName)
        {
            // Else, a valid Asmdef and module file exist, create a script at the Server Assembly.
            var serverAsmdefPath = AssetDatabase.GetAssetPath(foundModule.CloudAssemblyDefinition);
            var clientAsmdefPath = AssetDatabase.GetAssetPath(foundModule.ClientAssemblyDefinition);
            var serverDirectory = Path.GetDirectoryName(serverAsmdefPath);
            var clientDirectory = Path.GetDirectoryName(clientAsmdefPath);

            string createdServerPath = null;
            string createdClientPath = null;
            try
            {
                // Create the Scripts
                var fullAssetPathCloud = PathUtils.Join(serverDirectory, $"{editActionScriptName}.cs");
                var sanitizedNameCloud = GetUniqueSanitizedName(fullAssetPathCloud, ".cs");

                var fullAssetPathClient = PathUtils.Join(clientDirectory, $"{editActionScriptName}Client.cs");
                var sanitizedNameClient = GetUniqueSanitizedName(fullAssetPathClient, ".cs");

                AsmdefJsonData cloudJsonAsmdef =
                    AsmdefJsonData.ParseAssemblyDefinitionAsset(foundModule.CloudAssemblyDefinition);
                if (cloudJsonAsmdef == null || string.IsNullOrEmpty(cloudJsonAsmdef.name))
                    throw new Exception(CloudCodeSetupMessages.Get(CloudCodeSetupError.ModuleAsmdefCorrupted).Body);
                var sanitizedNamespace = NamespaceSanitizer.Sanitize(cloudJsonAsmdef.name);

                createdServerPath = CreateCloudCodeScript(serverDirectory, false, k_CloudCodeClientTemplateName,
                    k_CloudCodeCloudTemplateName, sanitizedNameClient, sanitizedNameCloud, sanitizedNamespace);
                createdClientPath = CreateCloudCodeScript(clientDirectory, true, k_CloudCodeClientTemplateName,
                    k_CloudCodeCloudTemplateName, sanitizedNameClient, sanitizedNameCloud, sanitizedNamespace);
            }
            catch (Exception e)
            {
                Debug.LogError($"An error occured while creating a Cloud Code Script {e.Message}");

                // Attempt rollback, ensure none of the assets created persist.
                if (createdServerPath != null && File.Exists(createdServerPath))
                    File.Delete(createdServerPath);

                if (createdClientPath != null && File.Exists(createdClientPath))
                    File.Delete(createdClientPath);

                throw;
            }
            finally
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            CloudCodeAuthoringServices.Instance.GetService<CloudModuleCreationAnalytics>().SendCloudCodeScriptAddedEvent();
        }

        bool AsmdefHasRequiredCoreApiRef(AsmdefJsonData jsonAsmdef)
        {
            return jsonAsmdef.HasAssemblyReferenceAtPath(k_CloudCodeAssemblyApisPath) &&
                jsonAsmdef.HasAssemblyReferenceAtPath(k_CloudCodeAssemblyCorePath);
        }

        void ShowErrorDialog(CloudCodeSetupError error)
        {
            ShowErrorDialog(CloudCodeSetupMessages.Get(error));
        }

        protected virtual void ShowErrorDialog(CloudCodeSetupMessage message)
        {
            CloudCodeDialogWindow.Show(
                CloudCodeSetupMessages.WindowTitle,
                message.Title,
                message.Body,
                documentationUrl: message.DocumentationUrl);
        }

        bool AssemblyNameConflicts(string assemblyName, out string existingAsmdefPath)
        {
            existingAsmdefPath = null;
            if (string.IsNullOrEmpty(assemblyName))
                return false;

            // A full Asmdef query is needed as internal name conflicts are compared
            // against its name, not Asset Name.
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
    }
}
#endif
