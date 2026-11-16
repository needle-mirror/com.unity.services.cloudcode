#if UNITY_PIPELINE_AVAILABLE && UNITY_EDITOR
using System.IO;
using Unity.Pipeline.Commands;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Cli
{
    /// <summary>
    /// Pipeline CLI commands that author Cloud Code deployment assets:
    /// a JavaScript script (<c>.js</c>) seeded with the package's default template, and a
    /// C# Module Reference (<c>.ccmr</c>) pointing at a module path. Each is written and
    /// imported so the Deployment window picks it up — mirroring the same content the
    /// <c>Assets &gt; Create &gt; Services &gt; Cloud Code ...</c> menu items produce.
    /// Only compiled when the <c>com.unity.pipeline</c> package is present
    /// (gated by the <c>UNITY_PIPELINE_AVAILABLE</c> version define on the asmdef).
    /// </summary>
    static class CloudCodeAuthoringEditorCliCommands
    {
        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Create a Cloud Code JavaScript script (.js) seeded with the default template — the same as Create > Services > Cloud Code Js Script")]
        public static CreatedConfigDto CreateJsScript(
            [CliArg("path", "Project-relative asset path (the Assets/ prefix and extension are optional). Defaults to Assets/new_cloud_code_script.js")] string path = null)
        {
            var assetPath = CliAssetPaths.PrepareAssetPath(path, "new_cloud_code_script", CloudCodeFileExtensions.Preferred());

            var templatePath = Path.Combine(CloudCodePackage.EditorPath, CloudCodeCliConstants.ScriptTemplatePath);
            var content = File.ReadAllText(templatePath);

            return WriteConfig(assetPath, content, "CloudCodeScript");
        }

        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Create a Cloud Code C# Module Reference (.ccmr) pointing at a module path")]
        public static CreatedConfigDto CreateModuleReference(
            [CliArg("path", "Project-relative asset path (the Assets/ prefix and .ccmr extension are optional). Defaults to Assets/new_module_reference.ccmr")] string path = null)
        {
            var assetPath = CliAssetPaths.PrepareAssetPath(path, "new_module_reference", CloudCodeModuleReferenceResources.FileExtension);

            var reference = ScriptableObject.CreateInstance<CloudCodeModuleReference>();
            reference.Name = Path.GetFileName(assetPath);
            reference.ModulePath =
                Path.Combine(
                    PathUtils.GetRelativePath(assetPath, Application.dataPath),
                    Path.GetFileNameWithoutExtension(reference.Name));

            return WriteConfig(assetPath, reference.ToJson(), "CloudCodeModuleReference");
        }

        static CreatedConfigDto WriteConfig(string assetPath, string content, string type)
        {
            File.WriteAllText(Path.GetFullPath(assetPath), content);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return new CreatedConfigDto { Type = type, Path = assetPath };
        }

        /// <summary>Result of creating a config-as-code deployment asset.</summary>
        public class CreatedConfigDto
        {
            public string Type { get; set; }
            public string Path { get; set; }
        }
    }
}
#endif
