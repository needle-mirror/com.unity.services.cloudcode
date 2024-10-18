using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    class ProjectIdentifierProvider : IProjectIdentifierProvider
    {
        public string ProjectId => Deployments.Instance?.ProjectIdProvider?.ProjectId ?? string.Empty;
    }
}
