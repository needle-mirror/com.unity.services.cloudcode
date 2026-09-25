using System;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudBehaviours;
using Unity.Services.CloudCode.Core;

namespace __NAMESPACE__
{
    /// A Cloud Code script's scope determines how its state is persisted.
    /// For more details on available scopes, refer to https://docs.unity.com/en-us/cloud-code/stateful-cloud-code/stateful-cloud-code
    [StateScope(Scope.Player)]
    public class BehaviourModuleCloudTemplate : CloudBehaviour
    {
        /// A public property will be persisted for every scope and are synchronized to the client.
        /// For more details on state serialization, refer to https://docs.unity.com/en-us/cloud-code/stateful-cloud-code/state-serialization
        public int Counter = 0;

        /// Events are sent from cloud to client.
        /// <example >
        /// Add a listener using the client instance.
        /// <code>
        /// client.OnSayHello.AddListener(n => Debug.Log(n));
        /// </code>
        /// </example>
        public event Action<string> OnSayHello;

        public string SayHello(string name)
        {
            Counter++;
            Logger.LogDebug($"Counter: {Counter}");
            OnSayHello?.Invoke(name);
            return $"Hello, {name}!";
        }
    }
}
