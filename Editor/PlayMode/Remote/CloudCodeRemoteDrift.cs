using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Deployment;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    interface ICloudCodeRemoteDrift
    {
        IReadOnlyList<string> ChangedModules { get; }
        event Action Changed;

        // Raised when a deploy records a new baseline for a module, the store's Changed event.
        event Action BaselineChanged;

        void Check();
    }

    // The modules changed or added since their last deploy to the remote environment, shared by the status
    // section and its title bar. Edits made in an external IDE raise no event, so the section asks for a check.
    class CloudCodeRemoteDrift : ICloudCodeRemoteDrift
    {
        static CloudCodeRemoteDrift s_Instance;

        internal static CloudCodeRemoteDrift Instance => s_Instance ??= new CloudCodeRemoteDrift(
            () => CloudCodeAuthoringServices.Instance.GetService<PlayModeStaleDeploymentWarner>()
                .CollectStaleAsync(includeNeverDeployed: true),
            CloudCodeAuthoringServices.Instance.GetService<ILastSuccessfulDeploymentStore>());

        readonly Func<Task<List<(string Name, PlayModeStaleDeploymentWarner.StaleReason Reason)>>> m_Collect;
        readonly ILastSuccessfulDeploymentStore m_DeploymentStore;
        Task m_InFlight;
        bool m_CheckAgain;

        public IReadOnlyList<string> ChangedModules { get; private set; } = Array.Empty<string>();
        public event Action Changed;

        public event Action BaselineChanged
        {
            add
            {
                if (m_DeploymentStore != null)
                    m_DeploymentStore.Changed += value;
            }
            remove
            {
                if (m_DeploymentStore != null)
                    m_DeploymentStore.Changed -= value;
            }
        }

        internal CloudCodeRemoteDrift(
            Func<Task<List<(string Name, PlayModeStaleDeploymentWarner.StaleReason Reason)>>> collect,
            ILastSuccessfulDeploymentStore deploymentStore = null)
        {
            m_Collect = collect;
            m_DeploymentStore = deploymentStore;
        }

        public void Check()
        {
            // A check that is already running may have read the files before the change that asked for this one.
            if (m_InFlight is { IsCompleted: false })
            {
                m_CheckAgain = true;
                return;
            }

            m_InFlight = Run();
        }

        internal Task WaitForCheck() => m_InFlight ?? Task.CompletedTask;

        async Task Run()
        {
            do
            {
                m_CheckAgain = false;
                IReadOnlyList<string> changed;
                try
                {
                    var stale = await m_Collect();
                    changed = stale.Select(item => item.Name).Distinct().OrderBy(name => name, StringComparer.Ordinal).ToList();
                }
                catch (Exception)
                {
                    // Best effort, like the Play Mode warning that shares the check.
                    changed = Array.Empty<string>();
                }

                if (!changed.SequenceEqual(ChangedModules))
                {
                    ChangedModules = changed;
                    Changed?.Invoke();
                }
            }
            while (m_CheckAgain);
        }
    }
}
