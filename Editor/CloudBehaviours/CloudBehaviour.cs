using System.Threading.Tasks;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;
#endif

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Base class for Cloud Code module classes.
    /// </summary>
    public class CloudBehaviour
    {
#if UNITY_EDITOR
        [CloudCodeIgnoreProperty]
        public ILogger Logger { get; set; }
        [CloudCodeIgnoreProperty]
        public IExecutionContext Context { get; set; }
        [CloudCodeIgnoreProperty]
        public IPushClient Push { get; set; }
        [CloudCodeIgnoreProperty]
        public ICloudSynchronizer Synchronizer { get; set; }

        /// <summary>
        /// This determines is data is synchronized to all players in the scope automatically at the end of each operation
        /// </summary>
        public bool AutoSynchronize = true;

        /// <summary>
        /// Sends a push message to every session member, or for a player scope to its owner.
        /// </summary>
        /// <param name="messageType">The message type</param>
        /// <param name="messageObject">The message object which will be serialized to json</param>
        /// <returns></returns>
        public async Task SendMessageAsync(string messageType, object messageObject)
        {
            var message = JsonConvert.SerializeObject(messageObject);
            foreach (var player in PushRecipients())
            {
                await Push.SendPlayerMessageAsync(Context, message, messageType, player);
            }
        }

        // A player scope's only client is its owner, whose player ID is the ScopeId.
        IReadOnlyList<string> PushRecipients()
        {
            var player = IsPlayerScoped() ? Context?.ScopeId : Context?.PlayerId;
            return Context?.Session?.PlayerIds ?? (player != null ? new[] { player } : System.Array.Empty<string>());
        }

        bool IsPlayerScoped() => GetType().GetCustomAttribute<StateScopeAttribute>()?.StateScope == Scope.Player;

        /// <summary>
        /// Sends a push message to all players in the scope to synchronize the values.
        /// Use when disabling AutoSynchronize for manual control.
        /// </summary>
        /// <returns>The task for the synchronization</returns>
        public Task SynchronizeAsync()
        {
            return Synchronizer?.SynchronizeAsync()
                ?? throw new System.InvalidOperationException(
                "No ICloudSynchronizer is attached to this behaviour. "
                + "This is required for notification of changes to synchronized values.");
        }

#endif
    }

    /// <summary>
    /// Synchronizes a <see cref="CloudBehaviour"/>'s state to all players in scope.
    /// </summary>
    public interface ICloudSynchronizer
    {
        /// <summary>
        /// Sends a push message to all players in the scope to synchronize the values.
        /// </summary>
        /// <returns>The task for the synchronization.</returns>
        public Task SynchronizeAsync();
    }
}
