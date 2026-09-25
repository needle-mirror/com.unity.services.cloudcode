using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Multiplayer.PlayMode.Editor;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.DeploymentApi.Editor;
using UnityEngine;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [Serializable]
    class StartLocalCloudCodeServerNode : ExecutionNode
    {
        [SerializeReference] public NodeOutput<CloudCodeEndpoint> Endpoint;
        [SerializeReference] public NodeOutput<bool> StartedByScenario;

        public StartLocalCloudCodeServerNode()
        {
            Endpoint = new(this);
            StartedByScenario = new(this);
        }

        protected override async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var services = CloudCodeAuthoringServices.Instance;
            var server = services.GetService<ICloudCodeLocalServer>();
            var deployer = services.GetService<LocalAutoDeployer>();

            var status = await WaitUntilSettled(server, cancellationToken);
            if (status == LocalCloudCodeServerStatus.Idle)
            {
                // Records that this run started the server, for the endpoint nodes downstream.
                SetOutput(StartedByScenario, true);
                await StartServer(server, cancellationToken);
                LocalServerOwnership.Claim(server);
            }
            else
            {
                SetOutput(StartedByScenario, false);
                await deployer.DeployChangedModulesAsync(cancellationToken);
            }

            // The server can stop while the deploy is awaited, from the toolbar or a failed health check.
            ThrowIfServerNotStarted(server);
            ThrowIfModulesFailed(deployer.GetModulesInErrorState());
            SetOutput(Endpoint, CloudCodeEndpoint.Local(server.GetPort(), server.GetServerPid()));
        }

        static async Task<LocalCloudCodeServerStatus> WaitUntilSettled(ICloudCodeLocalServer server, CancellationToken cancellationToken)
        {
            var settled = new TaskCompletionSource<LocalCloudCodeServerStatus>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnStatusChanged(object sender, LocalCloudCodeServerStatus status)
            {
                if (IsSettled(status))
                    settled.TrySetResult(status);
            }

            server.OnServerStatusChanged += OnStatusChanged;
            try
            {
                // Read after subscribing, so a transition between the read and the subscription is not missed.
                var current = server.GetCurrentServerStatus();
                if (IsSettled(current))
                    return current;

                using (cancellationToken.Register(() => settled.TrySetCanceled(cancellationToken)))
                {
                    return await settled.Task;
                }
            }
            finally
            {
                server.OnServerStatusChanged -= OnStatusChanged;
            }
        }

        static bool IsSettled(LocalCloudCodeServerStatus status)
        {
            return status is LocalCloudCodeServerStatus.Idle or LocalCloudCodeServerStatus.Started;
        }

        static async Task StartServer(ICloudCodeLocalServer server, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // The server swallows cancellation of its own start. Stopping it is how the toolbar cancels
            // an in-progress start, and the registration ends with the start so a later cancel is left
            // to Cleanup. The registration comes after the start so that a token already cancelled stops
            // this start rather than firing against the still-idle server. The stop only takes effect
            // between startup checks, so the wait is cancellable on its own and the start finishes
            // unwinding in the background.
            var start = server.StartCompilationAndService();
            using (cancellationToken.Register(() => _ = server.StopCompilationAndService()))
            {
                await LocalAutoDeployer.WaitCancellable(start, cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (server.GetCurrentServerStatus() != LocalCloudCodeServerStatus.Started)
            {
                var cause = server.GetLastServerFailure() ?? "No failure was recorded, check the Console for details.";
                throw new InvalidOperationException($"Cloud Code: the local server failed to start. {cause}");
            }
        }

        static void ThrowIfServerNotStarted(ICloudCodeLocalServer server)
        {
            var status = server.GetCurrentServerStatus();
            if (status == LocalCloudCodeServerStatus.Started)
                return;

            var cause = server.GetLastServerFailure();
            throw new InvalidOperationException(
                $"Cloud Code: the local server stopped while the scenario was preparing (status {status}).{(cause == null ? "" : " " + cause)}");
        }

        static void ThrowIfModulesFailed(IReadOnlyList<IDeploymentItem> failed)
        {
            if (failed.Count == 0)
                return;

            var names = string.Join(", ", failed.Select(item => $"'{item.Name}' ({GetErrorMessage(item)})"));
            throw new InvalidOperationException(
                $"Cloud Code: {names} failed to deploy to the local server. Fix the errors reported in the Deployment window and enter Play Mode again.");
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
    }
}
