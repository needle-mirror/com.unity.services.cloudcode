using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Multiplayer.PlayMode.Editor;
using UnityEngine;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [Serializable]
    class PublishCloudCodeEndpointNode : ExecutionNode
    {
        [SerializeReference] public NodeInput<CloudCodeEndpoint> Endpoint;

        public PublishCloudCodeEndpointNode()
        {
            Endpoint = new(this);
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            GetInput(Endpoint);
            return Task.CompletedTask;
        }
    }
}
