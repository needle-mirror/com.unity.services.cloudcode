using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Multiplayer.PlayMode.Editor;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;
using UnityEngine;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;
using Logger = Unity.Services.CloudCode.Editor.Shared.Logging.Logger;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [Serializable]
    class DeployRemoteCloudCodeNode : ExecutionNode
    {
        const float k_PlannedProgress = 0.1f;
        const float k_UploadedProgress = 0.5f;

        [SerializeReference] public NodeOutput<CloudCodeEndpoint> Endpoint;

        public DeployRemoteCloudCodeNode()
        {
            Endpoint = new(this);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var services = CloudCodeAuthoringServices.Instance;
            cancellationToken.ThrowIfCancellationRequested();

            var environments = services.GetService<IEnvironmentsApi>();
            var validation = await environments.ValidateEnvironmentAsync();
            if (validation.Failed)
                throw new InvalidOperationException(ValidationFailureMessage(validation));
            cancellationToken.ThrowIfCancellationRequested();

            var environmentName = EnvironmentDisplayName(environments);

            // A running server would take the deploy and point every instance at localhost.
            await StopLocalServer(services.GetService<ICloudCodeLocalServer>());

            RemoteDeployPlan plan;
            try
            {
                // A local redeploy outlives the server and builds into the same directories as this deploy.
                var autoDeployer = services.GetService<LocalAutoDeployer>();
                await autoDeployer.WaitForInFlightDeployAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                plan = await PlanAsync(services, environmentName, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                SetProgress(k_PlannedProgress);

                if (plan.HasModulesToDeploy)
                {
                    await DeployModules(services, environmentName, plan, cancellationToken);
                    plan.DeployFinishedUtc = DateTime.UtcNow;
                    cancellationToken.ThrowIfCancellationRequested();
                    SetProgress(k_UploadedProgress);
                }
                else if (plan.HasModules)
                {
                    Logger.LogVerbose(NothingToDeployMessage(environmentName));
                }

                ThrowIfModulesFailed(autoDeployer.GetModulesInErrorState(), environmentName);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                CloudCodeDeploymentRecord.Record(
                    CloudCodeDeployTarget.Remote, environmentName, CloudCodeDeploymentOutcome.Failed, e.Message);
                throw;
            }

            RecordOutcome(environmentName, plan);
            await WaitForPropagation(services, environmentName, plan, cancellationToken);
            SetOutput(Endpoint, CloudCodeEndpoint.Remote());
        }

        static Task StopLocalServer(ICloudCodeLocalServer server)
        {
            if (server.GetCurrentServerStatus() == LocalCloudCodeServerStatus.Idle)
                return Task.CompletedTask;

            LocalServerOwnership.Release();
            return server.StopCompilationAndService();
        }

        static Task<RemoteDeployPlan> PlanAsync(
            CloudCodeAuthoringServices services, string environmentName, CancellationToken cancellationToken)
        {
            var moduleReferences = services.GetService<CloudCodeModuleReferenceCollection>().ToList();
            var modules = services.GetService<CloudCodeModuleCollection>().ToList();
            if (moduleReferences.Count + modules.Count == 0)
                return Task.FromResult(new RemoteDeployPlan());

            var staleness = services.GetService<RemoteModuleStaleness>();
            var failed = new HashSet<IDeploymentItem>(services.GetService<LocalAutoDeployer>().GetModulesInErrorState());
            return PlanAsync(moduleReferences, modules, staleness.EvaluateAsync, staleness.EvaluateAsync, failed,
                environmentName, cancellationToken);
        }

        internal static async Task<RemoteDeployPlan> PlanAsync(
            List<CloudCodeModuleReference> moduleReferences,
            List<CloudCodeModule> modules,
            Func<CloudCodeModuleReference, Task<StalenessVerdict>> evaluateModuleReference,
            Func<CloudCodeModule, Task<StalenessVerdict>> evaluateModule,
            ISet<IDeploymentItem> inErrorState,
            string environmentName,
            CancellationToken cancellationToken)
        {
            var plan = new RemoteDeployPlan { HasModules = moduleReferences.Count + modules.Count > 0 };
            try
            {
                foreach (var moduleReference in moduleReferences)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var verdict = await evaluateModuleReference(moduleReference);
                    if (NeedsDeploy(verdict, inErrorState.Contains(moduleReference)))
                        plan.ModuleReferences.Add(moduleReference);
                }

                foreach (var module in modules)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var verdict = await evaluateModule(module);
                    if (NeedsDeploy(verdict, inErrorState.Contains(module)))
                        plan.Modules.Add(module);
                }
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                Logger.LogVerbose(PlanningFailedMessage(environmentName, e.Message));
                plan.ModuleReferences = moduleReferences;
                plan.Modules = modules;
            }

            return plan;
        }

        internal static bool NeedsDeploy(StalenessVerdict verdict, bool inErrorState)
        {
            // Only a deploy clears an error state left by an earlier attempt.
            return !verdict.IsUpToDate || inErrorState;
        }

        static async Task DeployModules(
            CloudCodeAuthoringServices services, string environmentName, RemoteDeployPlan plan, CancellationToken cancellationToken)
        {
            try
            {
                if (plan.ModuleReferences.Count > 0)
                {
                    var references = services.GetService<CloudCodeModuleReferenceDeployCommand>();
                    await Run(references.ExecuteAsync(plan.ModuleReferences, cancellationToken), cancellationToken);
                }

                // The module command fails on the API compatibility level even when given no modules.
                if (plan.Modules.Count > 0)
                {
                    var command = services.GetService<CloudCodeModuleDeployCommand>();
                    await Run(command.ExecuteAsync(plan.Modules, cancellationToken), cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                var failed = services.GetService<LocalAutoDeployer>().GetModulesInErrorState();
                throw DeployFailure(environmentName, failed, e);
            }
        }

        // The module upload ignores cancellation, so a cancelled run leaves it to finish in the background.
        static Task Run(Task deploy, CancellationToken cancellationToken)
        {
            _ = LocalAutoDeployer.ObserveFailure(deploy);
            return LocalAutoDeployer.WaitCancellable(deploy, cancellationToken);
        }

        static void RecordOutcome(string environmentName, RemoteDeployPlan plan)
        {
            if (!plan.HasModules)
            {
                CloudCodeDeploymentRecord.Record(
                    CloudCodeDeployTarget.Remote, environmentName, CloudCodeDeploymentOutcome.NoModules);
                return;
            }

            if (plan.DeployFinishedUtc != null
                || !CloudCodeDeploymentRecord.HasSucceeded(CloudCodeDeployTarget.Remote, environmentName))
            {
                CloudCodeDeploymentRecord.Record(
                    CloudCodeDeployTarget.Remote, environmentName, CloudCodeDeploymentOutcome.Succeeded);
            }
        }

        async Task WaitForPropagation(
            CloudCodeAuthoringServices services, string environmentName, RemoteDeployPlan plan, CancellationToken cancellationToken)
        {
            if (!plan.HasModules)
                return;

            var anchor = Latest(plan.DeployFinishedUtc, LatestRecordedUploadUtc(services));
            if (anchor == null)
                return;

            var wait = new RemoteDeployPropagationWait();
            var remaining = RemoteDeployPropagationWait.Remaining(anchor.Value, wait.UtcNow());
            if (remaining <= TimeSpan.Zero)
                return;

            Logger.Log(PropagationWaitMessage(environmentName, remaining));
            var start = Progress;
            await wait.WaitAsync(anchor.Value, waited => SetProgress(WaitProgress(start, waited)), cancellationToken);
        }

        internal static float WaitProgress(float start, float waited) => start + (1f - start) * waited;

        static DateTime? LatestRecordedUploadUtc(CloudCodeAuthoringServices services)
        {
            var store = services.GetService<ILastSuccessfulDeploymentStore>();
            DateTime? latest = null;
            foreach (var moduleReference in services.GetService<CloudCodeModuleReferenceCollection>())
                latest = Latest(latest, store.GetRemoteUploadCompletedUtc(moduleReference));
            foreach (var module in services.GetService<CloudCodeModuleCollection>())
                latest = Latest(latest, store.GetRemoteUploadCompletedUtc(module));
            return latest;
        }

        internal static DateTime? Latest(DateTime? first, DateTime? second)
        {
            if (first == null)
                return second;
            if (second == null)
                return first;

            return first > second ? first : second;
        }

        internal static string ValidationFailureMessage(ValidationResult result)
        {
            return $"Cloud Code: {result.ErrorMessage}";
        }

        internal static string EnvironmentDisplayName(IEnvironmentsApi environments)
        {
            var id = environments.ActiveEnvironmentId;
            if (!EnvironmentIds.IsSet(id))
                return null;

            var name = environments.ActiveEnvironmentName;
            return string.IsNullOrEmpty(name) ? id.ToString() : name;
        }

        internal static string NothingToDeployMessage(string environmentName)
        {
            return $"Cloud Code: every module is up to date in environment '{environmentName}', nothing to deploy.";
        }

        internal static string PlanningFailedMessage(string environmentName, string cause)
        {
            return $"Cloud Code: could not tell which modules changed since their last deploy to environment '{environmentName}' ({cause}), deploying all of them.";
        }

        internal static string PropagationWaitMessage(string environmentName, TimeSpan remaining)
        {
            var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
            return $"Cloud Code: waiting {seconds} s for the modules deployed to environment '{environmentName}' to propagate before instances launch.";
        }

        internal static void ThrowIfModulesFailed(IReadOnlyList<IDeploymentItem> failed, string environmentName)
        {
            if (failed.Count == 0)
                return;

            throw new InvalidOperationException(
                $"Cloud Code: {FailedItemList(failed)} failed to deploy to environment '{environmentName}'. " +
                "Fix the errors reported in the Deployment window and enter Play Mode again.");
        }

        internal static InvalidOperationException DeployFailure(
            string environmentName, IReadOnlyList<IDeploymentItem> failed, Exception cause)
        {
            if (failed.Count == 0)
            {
                return new InvalidOperationException(
                    $"Cloud Code: deploying modules to environment '{environmentName}' failed. {cause.Message}", cause);
            }

            return new InvalidOperationException(
                $"Cloud Code: {FailedItemList(failed)} failed to deploy to environment '{environmentName}'. " +
                "Fix the errors reported in the Deployment window and enter Play Mode again.", cause);
        }

        static string FailedItemList(IReadOnlyList<IDeploymentItem> failed)
        {
            return string.Join(", ", failed.Select(item => $"'{item.Name}' ({GetErrorMessage(item)})"));
        }

        static string GetErrorMessage(IDeploymentItem item)
        {
            foreach (var state in item.States)
            {
                if (state.Level == SeverityLevel.Error)
                    return state.Description;
            }

            return item.Status.Message;
        }

        internal class RemoteDeployPlan
        {
            public List<CloudCodeModuleReference> ModuleReferences = new List<CloudCodeModuleReference>();
            public List<CloudCodeModule> Modules = new List<CloudCodeModule>();
            public bool HasModules;
            public DateTime? DeployFinishedUtc;

            public bool HasModulesToDeploy => ModuleReferences.Count > 0 || Modules.Count > 0;
        }
    }
}
