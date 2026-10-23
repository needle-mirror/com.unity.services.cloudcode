using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    interface ILogsClient
    {
        Task<LogsResponse> GetLogsAsync(LogsQuery query, CancellationToken cancellationToken = default);
    }
}
