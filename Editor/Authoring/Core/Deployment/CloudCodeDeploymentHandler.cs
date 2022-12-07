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
        protected readonly ILogger m_Logger;
        readonly IPreDeployValidator m_PreDeployValidator;
        readonly ICloudCodeClient m_Client;
        readonly IDeploymentAnalytics m_DeploymentAnalytics;
        readonly List<Task<IScript>> m_UploadTasks;
        readonly List<Task> m_PublishTasks;
        readonly IScriptCache m_ScriptCache;

        internal enum StatusSeverityLevel
        {
            None,
            Info,
            Success,
            Warning,
            Error
        }

        protected CloudCodeDeploymentHandler(
            ICloudCodeClient client,
            IDeploymentAnalytics deploymentAnalytics,
            IScriptCache scriptCache,
            ILogger logger,
            IPreDeployValidator preDeployValidator)
        {
            m_UploadTasks = new List<Task<IScript>>();
            m_PublishTasks = new List<Task>();
            m_Client = client;
            m_DeploymentAnalytics = deploymentAnalytics;
            m_ScriptCache = scriptCache;
            m_Logger = logger;
            m_PreDeployValidator = preDeployValidator;
        }

        public async Task DeployAsync(IEnumerable<IScript> scripts)
        {
            ClearTasks();

            var scriptsEnumerated = scripts as IReadOnlyList<IScript> ?? scripts.ToList();

            var validationInfo = await m_PreDeployValidator.Validate(scriptsEnumerated);

            await DeployAndPublishFiles(validationInfo.ValidScripts);
        }

        async Task DeployAndPublishFiles(IReadOnlyList<IScript> scripts)
        {
            if (!scripts.Any())
                return;

            foreach (var script in scripts)
                UpdateScriptProgress(script, 0f);

            await UpdateLastPublishedDate(scripts);
            await UploadFiles(scripts);
            await PublishFiles();

            m_ScriptCache.Cache(scripts);

            var uploadExceptions = m_UploadTasks
                .Where(t => t.IsFaulted && t.Exception != null)
                .SelectMany(t => t.Exception.InnerExceptions);
            var publishExceptions = m_PublishTasks
                .Where(t => t.IsFaulted && t.Exception != null)
                .SelectMany(t => t.Exception.InnerExceptions);

            var exceptions = uploadExceptions.Concat(publishExceptions).ToList();
            if (exceptions.Any())
            {
                throw new AggregateException(exceptions);
            }
        }

        async Task UploadFiles(IReadOnlyList<IScript> scripts)
        {
            foreach (var script in scripts)
            {
                if (!m_ScriptCache.HasItemChanged(script))
                {
                    UpdateScriptProgress(script, 100f);
                    UpdateScriptStatus(script,
                        "Up to date",
                        string.Empty,
                        StatusSeverityLevel.Success);

                    continue;
                }

                var deploymentTask = UploadFile(script);
                m_UploadTasks.Add(deploymentTask);
            }

            try
            {
                await Task.WhenAll(m_UploadTasks);
            }
            catch
            {
                //we will use the task.Exceptions instead
            }
        }

        protected virtual void UpdateScriptProgress(IScript script, float progress)  {}

        protected virtual void UpdateScriptStatus(IScript script,
            string message,
            string detail,
            StatusSeverityLevel level = StatusSeverityLevel.None) {}

        protected virtual void OnPublishFailed(IScript script, Exception e)
        {
            UpdateScriptStatus(script,
                DeploymentStatuses.PublishFailed,
                e.Message,
                StatusSeverityLevel.Error);
        }

        async Task<IScript> UploadFile(IScript script)
        {
            try
            {
                var sendTimer = m_DeploymentAnalytics.BeginDeploySend(GetFileSize(script.Path));
                await m_Client.UploadFromFile(script);
                //Only dispose the timer if the upload was successful
                sendTimer?.Dispose();

                UpdateScriptProgress(script, 50f);
            }
            catch (Exception e)
            {
                m_DeploymentAnalytics.SendFailureDeploymentEvent(e.GetType().ToString());
                UpdateScriptStatus(script,
                    DeploymentStatuses.DeployFailed,
                    e.Message,
                    StatusSeverityLevel.Error);
                m_Logger.LogError(e.Message ?? e.InnerException?.Message);
                throw;
            }

            return script;
        }

        async Task PublishFiles()
        {
            foreach (var activeTask in m_UploadTasks)
            {
                if (activeTask.IsFaulted)
                {
                    continue;
                }

                var publishTask = PublishFile(activeTask.Result);
                m_PublishTasks.Add(publishTask);
            }

            try
            {
                await Task.WhenAll(m_PublishTasks);
            }
            catch
            {
                //we will use the task.Exceptions instead
            }
        }

        async Task PublishFile(IScript script)
        {
            try
            {
                await m_Client.Publish(script.Name);
                m_DeploymentAnalytics.SendSuccessfulPublishEvent();

                UpdateScriptProgress(script, 100f);
                UpdateScriptStatus(script,
                    "Up to date",
                    string.Empty,
                    StatusSeverityLevel.Success);
            }
            catch (Exception e)
            {
                m_DeploymentAnalytics.SendFailurePublishEvent(e.GetType().ToString());
                OnPublishFailed(script, e);
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

        static List<IScript> GetRemovedScripts(List<ScriptInfo> remoteScriptInfos, IReadOnlyList<IScript> localScripts)
        {
            var scriptNames = remoteScriptInfos.Select(s => s.ScriptName);
            return localScripts.Where(script => !scriptNames.Contains(script.Name.ToString())).ToList();
        }

        void ClearTasks()
        {
            m_UploadTasks.Clear();
            m_PublishTasks.Clear();
        }

        static int GetFileSize(string filePath)
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            return fileInfo.Exists ? (int)fileInfo.Length : -1;
        }
    }
}
