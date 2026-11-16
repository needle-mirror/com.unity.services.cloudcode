#if UNITY_PIPELINE_AVAILABLE && UNITY_EDITOR
#if UNITY_6000_7_OR_NEWER
using System;
using Unity.Pipeline.Commands;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Modules.UI;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using UnityEngine;

namespace Unity.Services.CloudCode.Cli
{
    /// <summary>
    /// Pipeline CLI commands that author a native C# Cloud Code Module (<c>.ccmu</c>) and its scripts.
    /// Creation is two steps, unlike the <c>Assets &gt; Create &gt; Services &gt; Cloud Code Module Script</c>
    /// menu, which does both at once: <c>services.cloudcode create_module</c> makes the module folder,
    /// its Cloud + Client assembly definitions and the module asset, then
    /// <c>services.cloudcode create_module_script</c> puts the first script in it. Both reuse
    /// the existing <see cref="CreateCloudCodeModule"/> logic the menu drives.
    /// Only compiled when the <c>com.unity.pipeline</c> package is present, on the same Unity
    /// versions that gate <see cref="CreateCloudCodeModule"/>.
    /// </summary>
    /// <remarks>
    /// Creating the assemblies and scripts triggers a script recompilation / domain reload: this
    /// command returns the module path before that completes, so a caller that needs the module
    /// compiled (e.g. before deploying) should poll <c>script recompile_status</c> afterwards.
    /// </remarks>
    static class CloudCodeModuleEditorCliCommands
    {
        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Create an empty Cloud Code C# module (.ccmu) with its Cloud + Client assembly definitions. " +
            "Add its first script with services.cloudcode create_module_script")]
        public static CreatedModuleDto CreateModule(
            [CliArg("module-name", "The module name — also the folder and .ccmu file name. Letters, digits or underscores, up to 50 characters, starting with a letter", Required = true)] string moduleName,
            [CliArg("path", "Project-relative parent folder (the Assets/ prefix is optional). Defaults to Assets. The module is created in a <module-name> subfolder.")] string path = null,
            [CliArg("cloud-assembly", "Name for the Cloud (server) assembly definition — this is the deployed module name. Same rules as module-name (optional, defaults to <module-name>)")] string cloudAssembly = null,
            [CliArg("client-assembly", "Name for the Client assembly definition. Can't contain / ? < > \\ : * | \" (optional, defaults to <module-name>Client)")] string clientAssembly = null)
        {
            moduleName = moduleName?.Trim();
            if (string.IsNullOrEmpty(moduleName) || ModuleNameSanitizer.Sanitize(moduleName) != moduleName)
                throw new ArgumentException("module-name is required and must contain only letters, digits or underscores, up to 50 characters, starting with a letter.");

            // Same defaults the creation window proposes. The cloud assembly is the module name because
            // the deployed module name is the cloud assembly name, and the scripts land in
            // <cloudAssembly>.Cloud / .Client, so a script named after the assembly would collide with the
            // leading namespace segment.
            cloudAssembly = string.IsNullOrWhiteSpace(cloudAssembly) ? moduleName : cloudAssembly.Trim();
            if (ModuleNameSanitizer.Sanitize(cloudAssembly) != cloudAssembly)
                throw new ArgumentException("cloud-assembly must contain only letters, digits or underscores, up to 50 characters, starting with a letter.");
            clientAssembly = string.IsNullOrWhiteSpace(clientAssembly) ? $"{moduleName}Client" : clientAssembly.Trim();
            if (AssemblyNameSanitizer.Sanitize(clientAssembly) != clientAssembly)
                throw new ArgumentException("client-assembly can't contain / ? < > \\ : * | \".");

            var parentFolder = CliAssetPaths.PrepareFolder(path);

            var creator = ScriptableObject.CreateInstance<HeadlessModuleCreator>();
            try
            {
                var ok = creator.CreateNewCloudCodeModule(
                    parentFolder, moduleName, null, null, cloudAssembly, clientAssembly, createScripts: false);

                if (!ok)
                    throw new InvalidOperationException(
                        creator.Error ?? "Cloud Code module creation failed. See the Unity console for details.");

                return new CreatedModuleDto
                {
                    Type = nameof(CloudCodeModule),
                    Path = $"{parentFolder}/{moduleName}/{moduleName}{CloudCodeModuleResources.FileExtension}",
                    ModuleName = moduleName,
                    CloudAssembly = cloudAssembly,
                    ClientAssembly = clientAssembly
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creator);
            }
        }

        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Create a native cloud/client script pair in an existing Cloud Code module, named by the " +
            "project-relative path to its .ccmu asset (e.g. Assets/MyModule/MyModule.ccmu)")]
        public static AddedModuleScriptDto CreateModuleScript(
            [CliArg("script-name", "Class name for the Cloud (server) script — the client script is named <script-name>Client", Required = true)] string scriptName,
            [CliArg("module-path", "Project-relative path to the existing module's .ccmu asset, e.g. Assets/MyModule/MyModule.ccmu. Not a path for the new script: that is derived from the module.", Required = true)] string modulePath)
        {
            scriptName = CliAssetPaths.SanitizeName(scriptName);
            if (string.IsNullOrEmpty(scriptName))
                throw new ArgumentException("script-name is required and must contain characters other than / ? < > \\ : * | \" . -.");

            if (string.IsNullOrWhiteSpace(modulePath))
                throw new ArgumentException("module-path is required.");

            var module = CreateCloudCodeModule.ResolveModule(modulePath);

            var creator = ScriptableObject.CreateInstance<HeadlessModuleCreator>();
            try
            {
                if (!creator.TryAddScriptToModule(module, scriptName, out var created))
                    throw new InvalidOperationException(
                        creator.Error ?? "Adding a Cloud Code module script failed. See the Unity console for details.");

                return new AddedModuleScriptDto
                {
                    Type = "CloudCodeModuleScript",
                    Path = created.CloudScript,
                    ClientPath = created.ClientScript
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(creator);
            }
        }

        /// <summary>
        /// Runs the module authoring flows without any interactive dialogs: the base class reports
        /// improper-setup / failure conditions through <see cref="ShowErrorDialog"/>, which we
        /// override to capture the message so the CLI command can surface it as an error instead
        /// of popping an editor window.
        /// </summary>
        class HeadlessModuleCreator : CreateCloudCodeModule
        {
            public string Error { get; private set; }

            protected override void ShowErrorDialog(CloudCodeSetupMessage message)
            {
                Error = string.IsNullOrEmpty(message.Title)
                    ? message.Body
                    : $"{message.Title} {message.Body}";
            }
        }

        /// <summary>Result of creating a native Cloud Code module.</summary>
        public class CreatedModuleDto
        {
            public string Type { get; set; }
            public string Path { get; set; }
            public string ModuleName { get; set; }
            public string CloudAssembly { get; set; }
            public string ClientAssembly { get; set; }
        }

        /// <summary>Result of adding a script pair to an existing native Cloud Code module.</summary>
        public class AddedModuleScriptDto
        {
            public string Type { get; set; }
            public string Path { get; set; }
            public string ClientPath { get; set; }
        }
    }
}
#endif
#endif
