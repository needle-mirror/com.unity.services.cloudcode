#if UNITY_6000_5_OR_NEWER
using System.Collections.Generic;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    /// <summary>
    /// Identifies an improper-setup condition encountered while creating a Cloud Behaviour module
    /// or adding a script to an existing one. Each value maps to a single catalog entry.
    /// </summary>
    enum CloudBehaviourSetupError
    {
        ServerAsmdefMisconfigured,
        ClientAsmdefMisconfigured,
        ModuleAsmdefCorrupted,
        ScriptRequiresCloudAsmdef,
        ScriptAddedInClientDir,
        MultipleAsmdefs,
        AsmdefNotPartOfModule,
    }

    /// <summary>
    /// User-facing copy for a single improper-setup dialog.
    /// </summary>
    readonly struct CloudBehaviourSetupMessage
    {
        /// <summary>Bold heading shown at the top of the dialog body (the window title is shared).</summary>
        public readonly string Title;
        /// <summary>Dialog body text shown below the heading.</summary>
        public readonly string Body;
        /// <summary>Optional documentation link shown in the dialog body.</summary>
        public readonly string DocumentationUrl;

        public CloudBehaviourSetupMessage(string title, string body, string documentationUrl = null)
        {
            Title = title;
            Body = body;
            DocumentationUrl = documentationUrl;
        }
    }

    /// <summary>
    /// Single source of truth for all Cloud Behaviour module creation dialog copy. Centralizing the
    /// strings here keeps them reviewable in one place and lets tooling enumerate every message.
    /// </summary>
    static class CloudBehaviourSetupMessages
    {
        internal static readonly string WindowTitle = L10n.Tr("Cloud Behaviour Creation Error");

        // Titles group the conditions by what the user actually needs to do about them.
        static readonly string k_TitlePlacement = L10n.Tr("Can't add script here.");
        static readonly string k_TitleMisconfigured = L10n.Tr("Cloud Behaviour module is misconfigured.");
        static readonly string k_TitleCreationFailed = L10n.Tr("Couldn't create Cloud Behaviour module.");

        const string k_ModulesDocumentationUrl =
            "https://docs.unity3d.com/Packages/com.unity.services.cloudcode@2.8/manual/Authoring/cloud_code_modules.html";

        static readonly Dictionary<CloudBehaviourSetupError, CloudBehaviourSetupMessage> k_Messages =
            new Dictionary<CloudBehaviourSetupError, CloudBehaviourSetupMessage>
        {
            [CloudBehaviourSetupError.ScriptRequiresCloudAsmdef] = new CloudBehaviourSetupMessage(
                k_TitlePlacement,
                L10n.Tr(
                    "This folder doesn't contain an Assembly Definition (.asmdef). A Cloud Behaviour " +
                    "script must be in the same folder as its Assembly Definition.\n\n" +
                    "Select a module's Cloud folder, or create a new Cloud Behaviour module."),
                k_ModulesDocumentationUrl),

            [CloudBehaviourSetupError.MultipleAsmdefs] = new CloudBehaviourSetupMessage(
                k_TitlePlacement,
                L10n.Tr(
                    "The target module for your script is unclear because this folder contains more " +
                    "than one Assembly Definition (.asmdef). A module's Cloud folder should contain " +
                    "exactly one server Assembly Definition.\n\n" +
                    "Remove the extra Assembly Definition files, or select the module's Cloud folder instead."),
                k_ModulesDocumentationUrl),

            [CloudBehaviourSetupError.ScriptAddedInClientDir] = new CloudBehaviourSetupMessage(
                k_TitlePlacement,
                L10n.Tr(
                    "This is the module's Client folder. Cloud Behaviour scripts are authored in its " +
                    "Cloud folder, which also contains its Assembly Definition (.asmdef).\n\n" +
                    "Select the module's Cloud folder and try again."),
                k_ModulesDocumentationUrl),

            [CloudBehaviourSetupError.AsmdefNotPartOfModule] = new CloudBehaviourSetupMessage(
                k_TitlePlacement,
                L10n.Tr(
                    "The Assembly Definition (.asmdef) in this folder isn't part of a Cloud Behaviour module.\n\n" +
                    "Add scripts inside an existing module's Cloud folder, or create a new Cloud Behaviour module in an empty folder."),
                k_ModulesDocumentationUrl),

            [CloudBehaviourSetupError.ServerAsmdefMisconfigured] = new CloudBehaviourSetupMessage(
                k_TitleMisconfigured,
                L10n.Tr(
                    "This Cloud Behaviour module's server Assembly Definition (.asmdef) is missing or misconfigured. " +
                    "It must be Editor-only, have no engine references, not be auto-referenced, and " +
                    "reference the Cloud Code Core, Cloud Code APIs, and Cloud Behaviours assemblies.\n\n" +
                    "Correct the server Assembly Definition, or recreate the module."),
                k_ModulesDocumentationUrl),

            [CloudBehaviourSetupError.ClientAsmdefMisconfigured] = new CloudBehaviourSetupMessage(
                k_TitleMisconfigured,
                L10n.Tr(
                    "This Cloud Behaviour module's client Assembly Definition (.asmdef) is missing, or " +
                    "doesn't reference its server Assembly Definition.\n\n" +
                    "Correct the client Assembly Definition, or recreate the module."),
                k_ModulesDocumentationUrl),

            [CloudBehaviourSetupError.ModuleAsmdefCorrupted] = new CloudBehaviourSetupMessage(
                k_TitleMisconfigured,
                L10n.Tr(
                    "The Assembly Definition (.asmdef) in this folder couldn't be read, and may be malformed.\n\n" +
                    "Correct or recreate the Assembly Definition, or create a new Cloud Behaviour module."),
                k_ModulesDocumentationUrl),
        };

        /// <summary>
        /// Returns the catalog entry for the given improper-setup condition.
        /// </summary>
        internal static CloudBehaviourSetupMessage Get(CloudBehaviourSetupError error) => k_Messages[error];

        /// <summary>
        /// Builds the dialog shown when module creation fails partway through. The title already states
        /// the failure, so the body is just the underlying detail (for example, an assembly name conflict).
        /// </summary>
        internal static CloudBehaviourSetupMessage CreationFailure(string detail) =>
            new CloudBehaviourSetupMessage(k_TitleCreationFailed, detail);

        // Parameterized copy used as creation-failure exception messages (surfaced via CreationFailure).

        internal static string AssemblyNameConflict(string assemblyName, string existingPath) =>
            string.Format(
                L10n.Tr("An Assembly Definition named '{0}' already exists at {1}."),
                assemblyName, existingPath);

        internal static string ModuleAlreadyExists(string existingPath) =>
            string.Format(
                L10n.Tr("A Cloud Behaviour module already exists at {0}."),
                existingPath);

        internal static readonly string AssemblyMissingReferences = L10n.Tr(
            "Asmdef Template corrupted - Missing Core + API Assembly references.");
    }
}
#endif
