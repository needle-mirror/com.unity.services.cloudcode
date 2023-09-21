using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules
{
    public interface IModuleZipper
    {
        Task<string> ZipCompilation(string solutionFilePath, string moduleName, CancellationToken cancellationToken);
    }
}
