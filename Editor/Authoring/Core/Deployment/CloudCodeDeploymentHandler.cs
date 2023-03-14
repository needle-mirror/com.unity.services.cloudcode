using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.DeploymentTask;
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
        readonly IScriptCache m_ScriptCache;

        internal enum StatusSeverityLevel
        {
            None,
            Info,
            Success,
            Warning,
            Error
        }

        public CloudCodeDeploymentHandler(
            ICloudCodeClient client,
            IDeploymentAnalytics deploymentAnalytics,
            IScriptCache scriptCache,
            ILogger logger,
            IPreDeployValidator preDeployValidator)
        {
            m_Client = client;
            m_DeploymentAnalytics = deploymentAnalytics;
            m_ScriptCache = scriptCache;
            m_Logger = logger;
            m_PreDeployValidator = preDeployValidator;
        }

        public async Task<DeployResult> DeployAsync(IEnumerable<IScript> scripts, bool reconcile = false, bool dryRun = false)
        {
            var scriptsEnumerated = scripts as IReadOnlyList<IScript> ?? scripts.ToList();

            foreach (var script in scriptsEnumerated)
                UpdateScriptProgress(script, 0f);

            var validationInfo = await m_PreDeployValidator.Validate(scriptsEnumerated);

            var validLocalScripts = validationInfo.ValidScripts;

            var remoteScripts = await UpdateLastPublishedDate(validLocalScripts);

            var remoteScriptNames = remoteScripts.Select(s => s.Name).ToHashSet();

            var toCreate = scriptsEnumerated
                .Where(s => !remoteScriptNames.Contains(s.Name))
                .ToList();

            var toUpdate = scriptsEnumerated
                .Where(s => remoteScriptNames.Contains(s.Name))
                .ToList();

            var toDelete = !reconcile
                ? new List<IScript>()
                : remoteScripts
                    .Where(scriptInfo => toUpdate.All(s => !s.Name.Equals(scriptInfo.Name)))
                    .Select(s => s as IScript)
                    .ToList();

            var res = new DeployResult(
                toCreate,
                toUpdate,
                toDelete,
                Array.Empty<IScript>(),
                validationInfo.InvalidScripts.Select(s => s.Key).ToList()
            );

            if (dryRun)
            {
                return res;
            }

            return await DeployFiles(validLocalScripts, toDelete, res, validationInfo);
        }

        async Task<DeployResult> DeployFiles(
            IReadOnlyList<IScript> scriptsToPublish,
            IReadOnlyList<IScript> scriptsToDelete,
            DeployResult dryRunResult,
            ValidationInfo validationInfo)
        {
            UpdateValidationStatus(validationInfo);

            if (!scriptsToPublish.Any())
                return dryRunResult;

            var publishedFiles = await UploadAndPublishWithCache(scriptsToPublish);
            var deletedFiles = await DeleteFiles(scriptsToDelete);

            var deployed = publishedFiles
                .Where(dt => dt.Task.IsCompletedSuccessfully && dt.Task.Exception == null)
                .Select(dt => dt.Script)
                .ToList();

            var failedToDelete = deletedFiles
                .Where(dt => dt.Task.IsFaulted)
                .Select(dt => dt.Script)
                .ToList();

            var failed = publishedFiles
                .Where(dt => dt.Task.IsFaulted)
                .Select(dt => dt.Script)
                .ToList();

            failed.AddRange(failedToDelete);

            var exceptions = publishedFiles
                .Where(dt => dt.Task.IsFaulted && dt.Task.Exception != null)
                .SelectMany(dt => dt.Task.Exception.InnerExceptions)
                .ToList();

            exceptions.AddRange(deletedFiles
                .Where(dt => dt.Task.IsFaulted && dt.Task.Exception != null)
                .SelectMany(dt => dt.Task.Exception.InnerExceptions)
                .ToList());

            var res = new DeployResult(
                dryRunResult.Created.Except(failed).ToList(),
                dryRunResult.Updated.Except(failed).ToList(),
                dryRunResult.Deleted.Except(failedToDelete).ToList(),
                deployed,
                dryRunResult.Failed.Concat(failed).ToList());

            if (exceptions.Any())
            {
                throw new DeploymentException(exceptions, res);
            }

            return res;
        }

        async Task<List<DeployTask>> UploadAndPublish(IReadOnlyList<IScript> scripts)
        {
            var uploadFiles = await UploadFiles(scripts);
            var publishFiles = await PublishFiles(uploadFiles);
            return publishFiles;
        }

        async Task<List<DeployTask>> UploadFiles(IReadOnlyList<IScript> scripts)
        {
            m_Logger.LogVerbose($"Uploading Scripts");
            var uploadTasks = new List<DeployTask>();
            foreach (var script in scripts)
            {
                var deploymentTask = UploadFile(script);
                uploadTasks.Add(new DeployTask
                {
                    Script = script,
                    Task = deploymentTask
                });
            }

            await WaitForTasksWithoutThrowing(uploadTasks.ToList(), "Uploading");
            return uploadTasks;
        }

        async Task<List<DeployTask>> UploadAndPublishWithCache(IReadOnlyList<IScript> scripts)
        {
            var cacheMisses = new List<IScript>();
            foreach (var script in scripts)
            {
                if (!m_ScriptCache.HasItemChanged(script))
                {
                    m_Logger.LogVerbose($"Script {script.Name} was cached and will not be deployed");
                    UpdateScriptProgress(script, 100f);
                    UpdateScriptStatus(script,
                        "Up to date",
                        string.Empty,
                        StatusSeverityLevel.Success);
                }
                else
                {
                    cacheMisses.Add(script);
                }
            }

            var result = await UploadAndPublish(cacheMisses);
            await UpdateLastPublishedDate(scripts);

            foreach (var r in result)
            {
                if (r.Task.IsCompletedSuccessfully)
                {
                    m_ScriptCache.Cache(await r.Task);
                }
            }

            return result;
        }

        protected virtual void UpdateValidationStatus(ValidationInfo validationInfo)
        {
            foreach (var invalidScript in validationInfo.InvalidScripts)
            {
                if (invalidScript.Value is DuplicateScriptException exception)
                {
                    UpdateScriptStatus(
                        invalidScript.Key,
                        DeploymentStatuses.DeployFailed,
                        exception.ShortMessage,
                        StatusSeverityLevel.Error);
                }
            }
        }

        protected virtual void UpdateScriptProgress(IScript script, float progress)  {}

        protected virtual void UpdateScriptStatus(IScript script,
            string message,
            string detail,
            StatusSeverityLevel level = StatusSeverityLevel.None) {}

        void OnPublishFailed(IScript script, Exception e)
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
                m_Logger.LogVerbose($"[Upload] Uploading {script.Name}");
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

        async Task<List<DeployTask>> PublishFiles(List<DeployTask> uploadTasks)
        {
            m_Logger.LogVerbose($"Publishing Scripts");
            var publishTasks = new List<DeployTask>();
            foreach (var activeTask in uploadTasks)
            {
                var publishFile = PublishFile(activeTask.Task);
                publishTasks.Add(new DeployTask()
                {
                    Script = activeTask.Script,
                    Task = publishFile
                });
            }

            await WaitForTasksWithoutThrowing(publishTasks.ToList(), "Publishing");
            return publishTasks;
        }

        async Task<IScript> PublishFile(Task<IScript> uploadTask)
        {
            var script = await uploadTask;
            try
            {
                m_Logger.LogVerbose($"[Publishing] Publishing {script.Name}");
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

            return script;
        }

        async Task<List<DeployTask>> DeleteFiles(IReadOnlyList<IScript> scriptsToDelete)
        {
            var deleteTasks = new List<DeployTask>();
            foreach (var script in scriptsToDelete)
            {
                var deleteFile = DeleteFile(script);
                deleteTasks.Add(new DeployTask()
                {
                    Script = script,
                    Task = deleteFile
                });
            }

            await WaitForTasksWithoutThrowing(deleteTasks.ToList(), "Deleting");
            return deleteTasks;
        }

        async Task<IScript> DeleteFile(IScript scriptToDelete)
        {
            try
            {
                await m_Client.Delete(scriptToDelete.Name);
            }
            catch (Exception e)
            {
                m_Logger.LogError(e.Message ?? e.InnerException?.Message);
                throw;
            }

            return scriptToDelete;
        }

        async Task<List<ScriptInfo>> UpdateLastPublishedDate(IReadOnlyList<IScript> localScripts)
        {
            List<ScriptInfo> remoteScriptInfos;
            try
            {
                m_Logger.LogVerbose($"Updating LastPublishedDate");
                remoteScriptInfos = await m_Client.ListScripts();
            }
            catch (Exception e)
            {
                m_DeploymentAnalytics.SendFailureDeploymentEvent(e.GetType().Name);
                foreach (var script in localScripts)
                {
                    UpdateScriptStatus(
                        script,
                        DeploymentStatuses.DeployFailed,
                        e.Message,
                        StatusSeverityLevel.Error);
                }
                throw;
            }

            foreach (var scriptInfo in remoteScriptInfos)
            {
                var matchingScript = localScripts.FirstOrDefault(s => s.Name.Equals(scriptInfo.Name));
                if (matchingScript != null)
                {
                    matchingScript.LastPublishedDate = scriptInfo.LastPublishedDate;
                }
            }

            var removedScripts = GetRemovedScripts(remoteScriptInfos, localScripts);
            removedScripts.ForEach(s => s.LastPublishedDate = null);
            return remoteScriptInfos;
        }

        static List<IScript> GetRemovedScripts(List<ScriptInfo> remoteScriptInfos, IReadOnlyList<IScript> localScripts)
        {
            var scriptNames = remoteScriptInfos.Select(s => s.Name);
            return localScripts.Where(script => !scriptNames.Contains(script.Name)).ToList();
        }

        async Task WaitForTasksWithoutThrowing(IList<DeployTask> tasks, string step)
        {
            try
            {
                await Task.WhenAll(tasks.Select(t => t.Task).ToArray());
            }
            catch (Exception e)
            {
                m_Logger.LogVerbose($"[{step}] An error occurred on publishing: {e}");
            }
        }

        static int GetFileSize(string filePath)
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            return fileInfo.Exists ? (int)fileInfo.Length : -1;
        }
    }
}
