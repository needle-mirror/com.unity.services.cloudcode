using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Deployment
{
    class CloudCodeDeploymentHandler : ICloudCodeDeploymentHandler
    {
        public static readonly string DuplicateNamesError = "Cannot deploy cloud code scripts with the same name. Please see the Deployment Window for more information.";

        protected readonly ILogger m_Logger;
        readonly ICloudCodeClient m_Client;
        readonly IDeploymentAnalytics m_DeploymentAnalytics;
        readonly List<Task<IScript>> m_DeployTasks;
        readonly List<Task> m_PublishTasks;
        readonly IScriptCache m_ScriptCache;

        protected enum StatusSeverityLevel
        {
            Info,
            Warning,
            Error
        }

        public CloudCodeDeploymentHandler(
            ICloudCodeClient client,
            IDeploymentAnalytics deploymentAnalytics,
            IScriptCache scriptCache,
            ILogger logger)
        {
            m_DeployTasks = new List<Task<IScript>>();
            m_PublishTasks = new List<Task>();
            m_Client = client;
            m_DeploymentAnalytics = deploymentAnalytics;
            m_ScriptCache = scriptCache;
            m_Logger = logger;
        }

        public async Task DeployAsync(IEnumerable<IScript> scripts)
        {
            ClearTasks();

            var scriptsEnumerated = scripts as IReadOnlyList<IScript> ?? scripts.ToList();
            if (!OnPreDeploy(scriptsEnumerated))
            {
                return;
            }

            await DeployAndPublishFiles(scriptsEnumerated);
        }

        async Task DeployAndPublishFiles(IReadOnlyList<IScript> scripts)
        {
            if (!scripts.Any())
                return;

            foreach (var script in scripts)
                UpdateScriptProgress(script, 0f);

            await UpdateLastPublishedDate(scripts);
            await DeployFiles(scripts);
            await PublishFiles();

            m_ScriptCache.Cache(scripts);
        }

        async Task DeployFiles(IReadOnlyList<IScript> scripts)
        {
            foreach (var script in scripts)
            {
                if (!m_ScriptCache.HasItemChanged(script))
                {
                    UpdateScriptStatus(script,
                        DeploymentStatuses.Published,
                        string.Empty,
                        StatusSeverityLevel.Info);
                    UpdateScriptProgress(script, 100f);

                    continue;
                }

                var deploymentTask = DeployFile(script);
                m_DeployTasks.Add(deploymentTask);
            }

            await Task.WhenAll(m_DeployTasks);
        }

        protected virtual bool OnPreDeploy(IReadOnlyList<IScript> scriptsEnumerated)
        {
            return true;
        }

        protected virtual void UpdateScriptProgress(IScript script, float progress)  {}

        protected virtual void UpdateScriptStatus(IScript script, string message, string detail, StatusSeverityLevel level = StatusSeverityLevel.Error) {}

        async Task<IScript> DeployFile(IScript script)
        {
            try
            {
                UpdateScriptStatus(script,
                    DeploymentStatuses.Deploying,
                    string.Empty,
                    StatusSeverityLevel.Info);

                var sendTimer = m_DeploymentAnalytics.BeginDeploySend(GetFileSize(script.Path));
                await m_Client.UploadFromFile(script);
                //Only dispose the timer if the upload was successful
                sendTimer?.Dispose();

                UpdateScriptProgress(script, 50f);
                UpdateScriptStatus(script,
                    DeploymentStatuses.Deployed,
                    string.Empty,
                    StatusSeverityLevel.Info);
            }
            catch (Exception e)
            {
                m_DeploymentAnalytics.SendFailureDeploymentEvent(e.GetType().ToString());
                UpdateScriptStatus(script,
                    DeploymentStatuses.DeployFailed,
                    e.Message,
                    StatusSeverityLevel.Error);
                m_Logger.LogError(e.Message ?? e?.InnerException?.Message);
                return null;
            }

            return script;
        }

        async Task PublishFiles()
        {
            foreach (var activeTask in m_DeployTasks)
            {
                var script = await activeTask;

                if (script == null)
                {
                    continue;
                }

                var publishTask = PublishFile(script);
                m_PublishTasks.Add(publishTask);
            }

            await Task.WhenAll(m_PublishTasks);
        }

        async Task PublishFile(IScript script)
        {
            try
            {
                UpdateScriptStatus(script,
                    DeploymentStatuses.Publishing,
                    string.Empty,
                    StatusSeverityLevel.Info);

                await m_Client.Publish(script.Name);
                m_DeploymentAnalytics.SendSuccessfulPublishEvent();

                UpdateScriptStatus(script,
                    DeploymentStatuses.Published,
                    string.Empty,
                    StatusSeverityLevel.Info);
                UpdateScriptProgress(script, 100f);
            }
            catch (Exception e)
            {
                m_DeploymentAnalytics.SendFailurePublishEvent(e.GetType().ToString());

                UpdateScriptStatus(script,
                    DeploymentStatuses.PublishFailed,
                    e.Message);
                throw;
            }
        }

        async Task UpdateLastPublishedDate(IReadOnlyList<IScript> localScripts)
        {
            var remoteScriptInfos = await m_Client.ListScripts();

            foreach (var scriptInfo in remoteScriptInfos)
            {
                var matchingScript = localScripts.FirstOrDefault(s => s.Name.ToString() == scriptInfo.ScriptName);
                if (matchingScript != null)
                {
                    matchingScript.LastPublishedDate = scriptInfo.LastPublishedDate;
                }
            }

            var removedScripts = GetRemovedScripts(remoteScriptInfos, localScripts);
            removedScripts.ForEach(s => s.LastPublishedDate = null);
        }

        List<IScript> GetRemovedScripts(List<ScriptInfo> remoteScriptInfos, IReadOnlyList<IScript> localScripts)
        {
            var scriptNames = remoteScriptInfos.Select(s => s.ScriptName);
            return localScripts.Where(script => !scriptNames.Contains(script.Name.ToString())).ToList();
        }

        void ClearTasks()
        {
            m_DeployTasks.Clear();
            m_PublishTasks.Clear();
        }

        static int GetFileSize(string filePath)
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            return fileInfo.Exists ? (int)fileInfo.Length : -1;
        }
    }
}
