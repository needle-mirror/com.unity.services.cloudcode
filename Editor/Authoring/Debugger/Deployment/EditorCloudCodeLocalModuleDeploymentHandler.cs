using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.IO;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment
{
    class EditorCloudCodeLocalModuleDeploymentHandler
    {
        const Environment.SpecialFolder k_LocalApplicationDataDir = Environment.SpecialFolder.LocalApplicationData;
        readonly IEnvironmentsApi m_EnvironmentsApi;
        readonly IFileSystem m_FileSystem;

        internal EditorCloudCodeLocalModuleDeploymentHandler(
            IEnvironmentsApi environmentsApi,
            IFileSystem fileSystem)
        {
            m_FileSystem = fileSystem;
            m_EnvironmentsApi = environmentsApi;
        }

        internal async Task<string> DeployAsync(List<IScript> compiled, CancellationToken cancellationToken)
        {
            var envId = m_EnvironmentsApi.ActiveEnvironmentId;
            if (envId == null)
            {
                throw new EnvironmentNotFoundException("No active environment selected.");
            }

            var moduleDestinationDir = Path.Combine(Environment.GetFolderPath(k_LocalApplicationDataDir),
                "UnityCloudCode",
                "Modules");

            foreach (var module in compiled.ToList())
            {
                await CopyToTempFolder(module.Path, Path.Combine(moduleDestinationDir, envId.ToString()), cancellationToken);

                // Do not continue if a cancellation was requested
                cancellationToken.ThrowIfCancellationRequested();
            }

            return moduleDestinationDir;
        }

        async Task CopyToTempFolder(string sourceFilePath, string destinationPath, CancellationToken cancellationToken)
        {
            try
            {
                var destinationFileName = Path.Combine(destinationPath, Path.GetFileName(sourceFilePath));

                // Create the output directory if it doesn't exist
                if (!m_FileSystem.FileExists(destinationFileName))
                    await m_FileSystem.CreateDirectory(destinationPath);

                // change the file extension from .ccm to .zip for the new file
                destinationFileName = Path.ChangeExtension(destinationFileName, "zip");
                await m_FileSystem.Copy(sourceFilePath, destinationFileName, true, cancellationToken);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to generate modules: {e.Message}");
            }
        }

        internal void UpdateDeployStatus(IEnumerable<CloudCodeModuleReference> ccmrs, string message,
            string detail = null, SeverityLevel severity = SeverityLevel.None, bool shouldLog = true)
        {
            var deployStatus = new DeploymentStatus(message, detail, severity);
            foreach (var ccmr in ccmrs)
            {
                if (shouldLog)
                    ccmr.UpdateLogStatus(deployStatus);
                else
                    ccmr.Status = deployStatus;
            }
        }

        internal void SetDeployStatusWithState(IEnumerable<CloudCodeModuleReference> ccmrs, string message,
            string detail = null, SeverityLevel severity = SeverityLevel.None)
        {
            var state = new AssetState(message, detail, severity);
            foreach (var ccmr in ccmrs)
            {
                ccmr.States.Clear();
                ccmr.States.Add(state);
            }
        }

        internal void ClearDeploymentStatus(IEnumerable<CloudCodeModuleReference> ccmrs)
        {
            foreach (var ccmr in ccmrs)
            {
                ccmr.Progress = 0f;
                ccmr.States.Clear();
                ccmr.ClearLogStatus();
            }
        }
    }
}
