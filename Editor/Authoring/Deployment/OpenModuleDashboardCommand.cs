using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration;
using Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    class OpenModuleDashboardCommand : Command
    {
        readonly IDashboardUrlResolver m_DashboardUrlResolver;
        readonly IModuleProjectRetriever m_ProjectRetriever;
        public override string Name => L10n.Tr("Open in Dashboard");

        public OpenModuleDashboardCommand(IDashboardUrlResolver dashboardUrlResolver, IModuleProjectRetriever projectRetriever)
        {
            m_DashboardUrlResolver = dashboardUrlResolver;
            m_ProjectRetriever = projectRetriever;
        }

        public override async Task ExecuteAsync(IEnumerable<IDeploymentItem> items, CancellationToken cancellationToken = default)
        {
            var moduleNames = items.Select(x => GetModuleName(x, m_ProjectRetriever));

            foreach (var name in moduleNames)
            {
                Application.OpenURL(await m_DashboardUrlResolver.CloudCodeModule(name));
            }
        }

        internal static string GetModuleName(IDeploymentItem item, IModuleProjectRetriever projectRetriever)
        {
            switch (item)
            {
#if UNITY_6000_5_OR_NEWER
                case CloudCodeModule ccmu:
                    return CloudCodeModuleDeployCommand.GetDeployModuleName(ccmu);
#endif
                case CloudCodeModuleReference ccmr:
                    return CloudCodeModuleReferenceDeployCommand.GetDeployModuleName(ccmr, projectRetriever);
                default:
                    return Path.GetFileNameWithoutExtension(item.Name);
            }
        }
    }
}
