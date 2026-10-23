using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Multiplayer.PlayMode.Editor;
using UnityEditor;
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

            // The endpoint the main editor itself resolves: the local server writes both prefs when it starts,
            // with the port it was launched on, and clears the pid when it stops.
            var message = new CloudCodeEndpointMessage(
                EditorPrefs.GetInt(CloudCodeEndpointMessage.k_ServerPidPref, CloudCodeEndpointMessage.k_NoServerPid),
                EditorPrefs.GetInt(CloudCodeEndpointMessage.k_ServerPortPref, CloudCodeLocalArgument.k_DefaultPort));

            // Null when Multiplayer Play Mode is disabled, in which case there are no clones to notify.
            // A clone launched later in the run reads the prefs at launch, after the main editor saves them to disk.
            // Ordering relies on this going out ahead of the PlayMessage, which is sent on the same channel.
            EditorContexts.MainEditorContext?.MessagingService.Broadcast(message);
            return Task.CompletedTask;
        }
    }
}
