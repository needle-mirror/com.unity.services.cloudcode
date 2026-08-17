#if UNITY_6000_5_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.IO;
using Unity.Services.CloudCode.Editor.Shared.Crypto;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    /// <summary>
    /// Produces a content fingerprint for a Cloud Code Module from its source files, so a module's
    /// current state can be compared against the state captured at its last successful deployment.
    /// </summary>
    interface IModuleContentHasher
    {
        /// <summary>
        /// Returns a stable hash of the module's tracked source files across its cloud and client
        /// assembly directories, or null if those directories cannot be resolved or the files cannot
        /// be read.
        /// </summary>
        Task<string> ComputeHashAsync(CloudCodeModule module);
    }

    class ModuleContentHasher : IModuleContentHasher
    {
        internal static readonly string[] TrackedExtensions = { ".cs", ".asmdef" };

        // NUL separators keep the per-file (path, content) framing unambiguous, since neither can
        // contain a NUL.
        const char k_Separator = '\0';

        readonly IFileSystem m_FileSystem;

        public ModuleContentHasher(IFileSystem fileSystem)
        {
            m_FileSystem = fileSystem;
        }

        /// <inheritdoc/>
        public Task<string> ComputeHashAsync(CloudCodeModule module)
        {
            // Directory resolution reads the AssetDatabase, which is main-thread only, so it must happen
            // here on the calling (main) thread before any work is pushed off-thread.
            var cloudDirectory = module.GetCloudAssemblyDirectory();
            var clientDirectory = module.GetClientAssemblyDirectory();

            // A module with unresolvable assembly directories is corrupt; report "cannot hash" (null) and
            // let the caller surface an actionable error.
            if (string.IsNullOrEmpty(cloudDirectory) || string.IsNullOrEmpty(clientDirectory))
                return Task.FromResult<string>(null);

            var directories = new[] { cloudDirectory, clientDirectory };

            // File enumeration and reads are pure file-system work; run them off the main thread so a large
            // module does not stall the Editor while hashing.
            return Task.Run(() => ComputeHashAsync(directories));
        }

        /// <summary>
        /// Hashes the tracked source files found under the given directories, sorted by relative path so
        /// the result is independent of file-system enumeration order.
        /// </summary>
        internal async Task<string> ComputeHashAsync(IReadOnlyList<string> directories)
        {
            var entries = new List<(string key, string content)>();

            try
            {
                foreach (var directory in directories)
                {
                    if (string.IsNullOrEmpty(directory) || !m_FileSystem.DirectoryExists(directory))
                        continue;

                    foreach (var path in m_FileSystem.DirectoryGetFiles(directory, "*", SearchOption.AllDirectories))
                    {
                        if (!IsTrackedExtension(path))
                            continue;

                        var content = await m_FileSystem.ReadAllText(path);
                        entries.Add((PathUtils.GetRelativePath(directory, path), content));
                    }
                }
            }
            catch (Exception)
            {
                // I/O failure while enumerating or reading module sources; treat as "cannot hash".
                return null;
            }

            var builder = new StringBuilder();
            foreach (var entry in entries
                .OrderBy(entry => entry.key, StringComparer.Ordinal)
                .ThenBy(entry => entry.content, StringComparer.Ordinal))
            {
                builder.Append(entry.key).Append(k_Separator).Append(entry.content).Append(k_Separator);
            }

            return Hash.SHA1(builder.ToString());
        }

        static bool IsTrackedExtension(string path)
        {
            return TrackedExtensions.Any(ext => path.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        }
    }
}
#endif
