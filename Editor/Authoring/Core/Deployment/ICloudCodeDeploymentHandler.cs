using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Deployment
{
    interface ICloudCodeDeploymentHandler
    {
        Task DeployAsync(IEnumerable<IScript> scripts);
    }
}
