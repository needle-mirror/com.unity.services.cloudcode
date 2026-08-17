using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Dotnet
{
    interface IDotnetRunner
    {
        Task<bool> IsDotnetAvailable();
        Task<string> ExecuteDotnetAsync(
            IEnumerable<string> arguments = default,
            CancellationToken cancellationToken = default);

        Task<List<SemVersion>> GetAvailableCoreRuntimes(
            CancellationToken ct = default);

        /// <summary>
        /// Versions of a single shared framework reported by <c>dotnet --list-runtimes</c>, for
        /// example <c>Microsoft.NETCore.App</c> or <c>Microsoft.AspNetCore.App</c>.
        /// </summary>
        Task<List<SemVersion>> GetAvailableRuntimes(
            string frameworkName,
            CancellationToken ct = default);
    }
}
