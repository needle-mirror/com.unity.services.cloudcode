using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Projects
{
    interface INodeJsRunner
    {
        Task<string> ExecNodeJs(IEnumerable<string> arguments = default, CancellationToken cancellationToken = default);
    }
}
