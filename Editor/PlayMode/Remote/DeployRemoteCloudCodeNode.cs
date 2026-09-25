using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Multiplayer.PlayMode.Editor;
using UnityEngine;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [Serializable]
    class DeployRemoteCloudCodeNode : ExecutionNode
    {
        [SerializeReference] public NodeOutput<CloudCodeEndpoint> Endpoint;

        public DeployRemoteCloudCodeNode()
        {
            Endpoint = new(this);
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            SetOutput(Endpoint, CloudCodeEndpoint.Remote());
            return Task.CompletedTask;
        }
    }
}
