#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Core.IO;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;

using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment
{
    class EditorCloudCodeLocalModuleDeploymentHandler
    {
        const Environment.SpecialFolder k_LocalApplicationDataDir = Environment.SpecialFolder.LocalApplicationData;
        // The server serves a module from the folder named by the caller's environment, so modules here
        // are reachable only by a caller with none: local-only mode starts without any, see LocalServerMode.
        internal const string k_LocalOnlyModulesFolder = "local-only";
        readonly IEnvironmentsApi m_EnvironmentsApi;
        readonly IFileSystem m_FileSystem;
        readonly ILogger m_logger;
        readonly IDeploymentAnalytics m_DeploymentAnalytics;
        readonly Func<bool> m_IsPlaying;

        public EditorCloudCodeLocalModuleDeploymentHandler(
            IEnvironmentsApi environmentsApi,
            IFileSystem fileSystem,
            ILogger logger,
            IDeploymentAnalytics deploymentAnalytics)
            : this(environmentsApi, fileSystem, logger, deploymentAnalytics, () => EditorApplication.isPlaying)
        {
        }

        internal EditorCloudCodeLocalModuleDeploymentHandler(
            IEnvironmentsApi environmentsApi,
            IFileSystem fileSystem,
            ILogger logger,
            IDeploymentAnalytics deploymentAnalytics,
            Func<bool> isPlaying)
        {
            m_FileSystem = fileSystem;
            m_EnvironmentsApi = environmentsApi;
            m_logger = logger;
            m_DeploymentAnalytics = deploymentAnalytics;
            m_IsPlaying = isPlaying;
        }

        internal static string GetModuleDestinationDir()
        {
            return Path.Combine(Environment.GetFolderPath(k_LocalApplicationDataDir),
                "UnityCloudCode",
                "Modules");
        }

        internal static string GetModuleDestinationDir(Guid? environmentId)
        {
            return Path.Combine(GetModuleDestinationDir(),
                EnvironmentIds.IsSet(environmentId) ? environmentId.Value.ToString() : k_LocalOnlyModulesFolder);
        }

        internal async Task<string> DeployAsync(Dictionary<IModuleItem, IScript> deploymentItems,
            DeploymentAssetKind assetKind,
            DeploymentOrigin origin,
            CancellationToken cancellationToken)
        {
            ThrowIfPlaying(deploymentItems.Keys);

            try
            {
                var envId = m_EnvironmentsApi.ActiveEnvironmentId;
                if (!EnvironmentIds.IsSet(envId) && !LocalServerMode.LocalOnly)
                {
                    throw new EnvironmentNotFoundException("No active environment selected.");
                }

                var moduleDestinationDir = GetModuleDestinationDir();
                var environmentDir = GetModuleDestinationDir(envId);

                foreach (var deploymentItem in deploymentItems)
                {
                    var module = deploymentItem.Key;
                    var scripts = deploymentItem.Value;
                    UpdateDeployStatus(module, "Deploying...", severity: SeverityLevel.Info);

                    var sendTimer = origin == DeploymentOrigin.Manual
                        ? m_DeploymentAnalytics.BeginDeploySend(
                        GetFileSize(scripts.Path),
                        assetKind,
                        DeploymentTarget.Local,
                        scripts.Name.ToString())
                        : null;

                    await CopyToTempFolder(scripts.Path, environmentDir, cancellationToken);

                    // Only dispose the timer if the copy succeeded.
                    sendTimer?.Dispose();

                    UpdateDeployStatus(module, "Deployed Successfully", severity: SeverityLevel.Info);
                    module.Progress = 100f;
                    module.UpdateLogStatus(DeploymentStatusOwnership.DeployedToLocalServer);
                    SetDeployStatusWithState(module, "Deployed to Local Server", severity: SeverityLevel.Info);

                    // Do not continue if a cancellation was requested
                    cancellationToken.ThrowIfCancellationRequested();
                }

                return moduleDestinationDir;
            }
            catch (Exception e)
            {
                if (origin == DeploymentOrigin.Manual)
                {
                    m_DeploymentAnalytics.SendFailureDeploymentEvent(
                        e.GetType().ToString(),
                        AnalyticsErrorCode.FromException(e),
                        AnalyticsErrorData.FromException(e),
                        assetKind,
                        DeploymentTarget.Local,
                        deploymentItems.Values.Select(script => script.Name.ToString()).ToList());
                }

                throw;
            }
        }

        // Unity cannot rebuild module assemblies in play mode; Deploy-on-Play runs after isPlaying is reset.
        internal void ThrowIfPlaying(IEnumerable<IModuleItem> items)
        {
            if (!m_IsPlaying())
                return;

            const string refusal = "Not deployed to the local server: exit play mode and deploy again.";
            UpdateDeployStatuses(items, refusal, severity: SeverityLevel.Error);
            throw new InvalidOperationException(refusal);
        }

        static int GetFileSize(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Exists ? (int)fileInfo.Length : -1;
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
                // Keep the inner exception: the analytics error code is mapped from it.
                throw new Exception($"Failed to generate modules: {e.Message}", e);
            }
        }

        internal void UpdateDeployStatus(IModuleItem deploymentItem, string message,
            string detail = null, SeverityLevel severity = SeverityLevel.Info, bool shouldLog = true,
            bool logToConsole = true)
        {
            var deployStatus = new DeploymentStatus(message, detail, severity);
            if (shouldLog)
                deploymentItem.UpdateLogStatus(deployStatus);
            else
                deploymentItem.Status = deployStatus;

            // For deployment errors, ensure they also show up in the console - unless the caller has
            // already reported the underlying diagnostics there itself.
            if (severity == SeverityLevel.Error && logToConsole)
                m_logger.LogError($"{message} - {detail}");
        }

        internal void SetDeployStatusWithState(IModuleItem deploymentItem, string message,
            string detail = null, SeverityLevel severity = SeverityLevel.Info)
        {
            var state = new AssetState(message, detail, severity);
            deploymentItem.States.Clear();
            deploymentItem.States.Add(state);
        }

        internal void UpdateDeployStatuses(IEnumerable<IModuleItem> deploymentItems, string message,
            string detail = null, SeverityLevel severity = SeverityLevel.Info, bool shouldLog = true,
            bool logToConsole = true)
        {
            var deploymentList = deploymentItems.ToList();
            foreach (var deploymentItem in deploymentList)
                UpdateDeployStatus(deploymentItem, message, detail, severity, shouldLog, logToConsole);
        }

        internal void SetDeployStatusesWithState(IEnumerable<IModuleItem> deploymentItems, string message,
            string detail = null, SeverityLevel severity = SeverityLevel.Info)
        {
            var deploymentList = deploymentItems.ToList();
            foreach (var deploymentItem in deploymentList)
                SetDeployStatusWithState(deploymentItem, message, detail, severity);
        }

        internal void ClearDeploymentStatuses(IEnumerable<IModuleItem> deploymentItems)
        {
            foreach (var deploymentItem in deploymentItems)
            {
                deploymentItem.Progress = 0f;
                deploymentItem.States.Clear();
                deploymentItem.ClearLogStatus();
            }
        }
    }
}
#endif
