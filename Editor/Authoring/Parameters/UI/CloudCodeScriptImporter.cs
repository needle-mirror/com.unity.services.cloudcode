using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Projects;
using Unity.Services.CloudCode.Authoring.Editor.Scripts;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Threading;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Parameters.UI
{
    [CloudCodeImporter]
    class CloudCodeScriptImporter : ScriptedImporter
    {
        const string k_JsScriptAssetIdentifier = "JsScript";
        const string k_SourceAssetIdentifier = "source";

        public List<CloudCodeParameter> Parameters;
        public ParameterSource Source;

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var jsScript = ScriptableObject.CreateInstance<CloudCodeScript>();
            Source = ParameterSource.Editor;

            if (CloudCodeProject.IsInitialized())
            {
                LoadInScriptParameters(ctx);
            }

            jsScript.Model = new Script(ctx.assetPath);
            var body = File.ReadAllText(ctx.assetPath);
            jsScript.Model.Body = body;
            jsScript.Model.Parameters = Parameters?.ToList() ?? new List<CloudCodeParameter>();
            jsScript.name = jsScript.Model.Name.GetNameWithoutExtension();

            var bodyAsset = new TextAsset(body);
            ctx.AddObjectToAsset(k_JsScriptAssetIdentifier, jsScript, CloudCodeResources.Icon);
            ctx.AddObjectToAsset(k_SourceAssetIdentifier, bodyAsset, CloudCodeResources.Icon);
            ctx.SetMainObject(bodyAsset);
        }

        void LoadInScriptParameters(AssetImportContext ctx)
        {
            var loader = CloudCodeAuthoringServices.Instance.GetService<InScriptParameters>();

            var path = ctx.assetPath; //getter must be called on the main thread
            var inScriptParams = Sync.RunInBackgroundThread(() => loader.GetParametersFromPath(path));

            if (inScriptParams != null)
            {
                Parameters = inScriptParams;
                Source = ParameterSource.InScript;
            }
        }
    }
}
