using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Multiplayer.PlayMode.Editor;
using UnityEngine;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [Serializable]
    class ResetCloudCodeEndpointNode : ExecutionNode
    {
        [SerializeReference] public NodeInput<CloudCodeEndpoint> Endpoint;
        [SerializeReference] public NodeInput<bool> StartedByScenario;

        public ResetCloudCodeEndpointNode()
        {
            Endpoint = new(this);
            StartedByScenario = new(this);
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            GetInput(Endpoint);
            GetInput(StartedByScenario);
            return Task.CompletedTask;
        }
    }
}
