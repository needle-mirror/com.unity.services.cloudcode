using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    [ScriptedImporter(1, CloudCodeModuleResources.FileExtension)]
    class CloudCodeModuleImporter : ScriptedImporter
    {
        public void OnValidate()
        {
            hideFlags = HideFlags.HideInInspector;
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var fileContent = File.ReadAllText(ctx.assetPath);

            if (!CloudCodeModule.FromJson(fileContent, out var cloudAssemblyDefinition, out var clientAssemblyDefinition))
            {
                ctx.LogImportError($"Failed to import Cloud Code Module at '{ctx.assetPath}'.");
                return;
            }

            var definition = ScriptableObject.CreateInstance<CloudCodeModule>();
            definition.CloudAssemblyDefinition = cloudAssemblyDefinition;
            definition.ClientAssemblyDefinition = clientAssemblyDefinition;
            ctx.AddObjectToAsset("MainAsset", definition);
            ctx.SetMainObject(definition);
        }
    }
}
