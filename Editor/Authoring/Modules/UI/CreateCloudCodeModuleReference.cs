using System.IO;
using Unity.Services.CloudCode.Authoring.Editor.Analytics;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using UnityEditor;
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
    class CreateCloudCodeModuleReference : BaseClass
    {
        const string k_DefaultReferenceName = "new_module_reference";

        [MenuItem("Assets/Create/Services/Cloud Code C# Module Reference", false, 81)]
        public static void CreateModuleReferenceFile()
        {
            var filePath = k_DefaultReferenceName + CloudCodeModuleReferenceResources.FileExtension;

#if UNITY_6000_4_OR_NEWER
            UnityEngine.EntityId instanceId = new UnityEngine.EntityId();
#else
            int instanceId = 0;
#endif

            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(
                instanceId,
                CreateInstance<CreateCloudCodeModuleReference>(),
                filePath,
                null,
                null);
        }

        public override void Action(ActionIdentifier instanceId, string pathName, string resourceFile)
        {
            var reference = CreateInstance<CloudCodeModuleReference>();
            reference.Name =  Path.GetFileName(pathName);
            reference.ModulePath =
                Path.Combine(
                    PathUtils.GetRelativePath(pathName, Application.dataPath),
                    Path.GetFileNameWithoutExtension(reference.Name));
            File.WriteAllText(pathName, reference.ToJson());

            CloudCodeAuthoringServices.Instance.GetService<CloudModuleCreationAnalytics>().SendReferenceCreatedEvent();

            AssetDatabase.Refresh();
        }
    }
}
