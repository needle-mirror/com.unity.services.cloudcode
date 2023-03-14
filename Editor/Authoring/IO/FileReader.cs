using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.IO
{
    class FileReader : IFileReader
    {
        public Task<string> ReadAllTextAsync(string filePath, CancellationToken cancellationToken)
        {
            return File.ReadAllTextAsync(filePath, cancellationToken);
        }
    }
}
