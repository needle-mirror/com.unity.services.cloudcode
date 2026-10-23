using System.Linq;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Package
{
    class PackageVersionProvider : IPackageVersionProvider
    {
        public async Task<string> GetPackageVersionAsync(string packageName)
        {
            var listRequest = UnityEditor.PackageManager.Client.List();
            while (!listRequest.IsCompleted)
            {
                await Task.Yield();
            }

            if (listRequest.Error != null)
            {
                throw new PackageManagerRequestException(
                    $"Package Manager request failed: {listRequest.Error.errorCode} {listRequest.Error.message}");
            }

            var packages = listRequest.Result;

            return packages.FirstOrDefault(p => p.name == packageName)?.version;
        }
    }
}
