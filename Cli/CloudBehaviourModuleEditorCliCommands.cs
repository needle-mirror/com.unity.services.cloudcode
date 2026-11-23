#if UNITY_PIPELINE_AVAILABLE && UNITY_EDITOR
#if UNITY_6000_7_OR_NEWER
using System;
using Unity.Pipeline.Commands;
using Unity.Services.CloudCode.Authoring.Editor.Modules.UI;
using UnityEngine;

namespace Unity.Services.CloudCode.Cli
{
    /// <summary>
    /// Pipeline CLI command that authors a Cloud Behaviour inside an existing Cloud Code module: the
    /// <c>CloudBehaviour</c>-derived cloud script and its <c>[CloudBehaviour(typeof(...))]</c> client
    /// bindings, the same pair the <c>Assets &gt; Create &gt; Services &gt; Cloud Behaviour Script</c>
    /// menu adds to a module that already exists. Create the module first with
    /// <c>services.cloudcode create_module</c>.
    /// Only compiled when the <c>com.unity.pipeline</c> package is present AND Cloud Behaviours are
    /// enabled (the same defines that gate <see cref="CreateCloudBehaviourModule"/>).
    /// </summary>
    /// <remarks>
    /// Creating the scripts triggers a script recompilation / domain reload: this command returns the
    /// script paths before that completes, so a caller that needs the module compiled (e.g. before
    /// deploying) should poll <c>script recompile_status</c> afterwards.
    /// </remarks>
    static class CloudBehaviourModuleEditorCliCommands
    {
        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Create a Cloud Behaviour (cloud script plus its client bindings) in an existing Cloud Code " +
            "module, named by the project-relative path to its .ccmu asset. Adds the Cloud Behaviour " +
            "assembly references to the module if it does not have them yet",
            Name = "create_cloudbehaviour")]
        public static CreatedCloudBehaviourDto CreateCloudBehaviour(
            [CliArg("behaviour-name", "Class name for the CloudBehaviour cloud script — the client bindings are named <behaviour-name>Client", Required = true)] string behaviourName,
            [CliArg("module-path", "Project-relative path to the existing module's .ccmu asset, e.g. Assets/MyModule/MyModule.ccmu. Not a path for the new script: that is derived from the module.", Required = true)] string modulePath)
        {
            behaviourName = CliAssetPaths.SanitizeName(behaviourName);
            if (string.IsNullOrEmpty(behaviourName))
                throw new ArgumentException("behaviour-name is required and must contain characters other than / ? < > \\ : * | \" . -.");

            if (string.IsNullOrWhiteSpace(modulePath))
                throw new ArgumentException("module-path is required.");

            var module = CreateCloudCodeModule.ResolveModule(modulePath);

            var creator = ScriptableObject.CreateInstance<HeadlessCloudBehaviourCreator>();
            try
            {
                if (!creator.TryAddCloudBehaviourToModule(module, behaviourName, out var created))
                    throw new InvalidOperationException(
                        creator.Error ?? "Cloud Behaviour creation failed. See the Unity console for details.");

                return new CreatedCloudBehaviourDto
                {
                    Type = "CloudBehaviour",
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
        /// Runs the Cloud Behaviour creation flow without any interactive dialogs:
        /// <see cref="CreateCloudBehaviourModule"/> reports failures through
        /// <see cref="CreateCloudBehaviourModule.ShowErrorDialog"/>, which we override to capture the
        /// message so the CLI command can surface it as an error instead of popping an editor window.
        /// </summary>
        class HeadlessCloudBehaviourCreator : CreateCloudBehaviourModule
        {
            public string Error { get; private set; }

            protected override void ShowErrorDialog(CloudBehaviourSetupMessage message)
            {
                Error = string.IsNullOrEmpty(message.Title)
                    ? message.Body
                    : $"{message.Title} {message.Body}";
            }
        }

        /// <summary>Result of creating a Cloud Behaviour in a module.</summary>
        public class CreatedCloudBehaviourDto
        {
            public string Type { get; set; }
            public string Path { get; set; }
            public string ClientPath { get; set; }
        }
    }
}
#endif
#endif
