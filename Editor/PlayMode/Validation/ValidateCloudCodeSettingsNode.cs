using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Multiplayer.PlayMode.Editor;
using UnityEngine;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [Serializable]
    class ValidateCloudCodeSettingsNode : ExecutionNode
    {
        [SerializeReference] public NodeInput<CloudCodeSettings> Settings;

        public ValidateCloudCodeSettingsNode()
        {
            Settings = new(this);
        }

        protected override Task ExecuteAsync(CancellationToken cancellationToken)
        {
            var settings = GetInput(Settings);
            if (!settings.DeployCloudCode)
            {
                return Task.CompletedTask;
            }

            ValidateDeployTarget(settings.DeployTarget);
            return Task.CompletedTask;
        }

        static void ValidateDeployTarget(CloudCodeDeployTarget target)
        {
            if (!Enum.IsDefined(typeof(CloudCodeDeployTarget), target))
            {
                throw new InvalidOperationException(
                    $"Cloud Code: the deploy target '{(int)target}' is not supported. Choose Local or Remote in the scenario's Cloud Code settings.");
            }
        }
    }
}
