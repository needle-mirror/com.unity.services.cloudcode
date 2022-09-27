using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    public class ProjectIdProvider : IProjectIdProvider
    {
        public string ProjectId => CloudProjectSettings.projectId;
    }
}
