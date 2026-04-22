using Unity.Services.CloudCode.Editor.Shared.Clients;
using Unity.Services.Core.Editor;
using Unity.Services.Core.Editor.OrganizationHandler;
using UnityEditor;

namespace Unity.Services.CloudCode.Settings
{
    internal struct CloudCodeIdentifier : IEditorGameServiceIdentifier
    {
        public string GetKey() => "Cloud Code";
    }

    internal class CloudCodeEditorGameService : IEditorGameService
    {
        public string Name => "Cloud Code";
        public IEditorGameServiceIdentifier Identifier => k_Identifier;
        public bool RequiresCoppaCompliance => false;
        public bool HasDashboard => true;
        public IEditorGameServiceEnabler Enabler => null;

        static readonly CloudCodeIdentifier k_Identifier = new CloudCodeIdentifier();

        public string GetFormattedDashboardUrl()
        {
            var host = CloudEnvironmentConfigProvider.IsStaging()
                ? "https://staging.dashboard.unity3d.com"
                : "https://dashboard.unity3d.com";
            return $"{host}/organizations/{OrganizationProvider.Organization.Key}/projects/{CloudProjectSettings.projectId}/cloud-code/overview";
        }
    }
}
