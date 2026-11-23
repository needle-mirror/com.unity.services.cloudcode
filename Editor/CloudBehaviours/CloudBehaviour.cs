using System.Threading.Tasks;

#if UNITY_EDITOR
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
    public abstract class CloudBehaviour
    {
#if UNITY_EDITOR
        [CloudCodeIgnoreProperty]
        public ILogger Logger { get; internal set; }
        [CloudCodeIgnoreProperty]
        public IExecutionContext Context { get; internal set; }
        [CloudCodeIgnoreProperty]
        public IPushClient Push { get; internal set; }
        [CloudCodeIgnoreProperty]
        public ICloudSynchronizer Synchronizer { get; internal set; }

        /// <summary>
        /// This determines whether changes are synchronized to the players in the scope automatically at the end of each operation
        /// </summary>
        /// <remarks>
        /// After an endpoint, a member is pushed only if what it would receive (synced fields and <c>[SyncView]</c> projections)
        /// changed or an event was raised, so a read-only endpoint pushes nothing. This builds each member's payload before and after the call,
        /// one extra serialization per member. <c>SynchronizeAsync</c>, and an endpoint that turns this on, push to every member.
        /// A client that may have missed a push can recover the current state with <c>Hydrate()</c> on its generated client.
        /// Each member's push carries its full synced state, including <c>[SyncView]</c> projections, in one message. The whole
        /// message, including a small envelope naming the scope and behaviour, can be at most 51,200 bytes (50 KiB) of UTF-8;
        /// a larger one isn't delivered.
        /// A push that fails during this automatic synchronization, including one that's too large, is logged as an error rather than thrown.
        /// A synced member that throws before the call is logged as a warning and every member is pushed after it; a <c>[SyncView]</c> that throws is logged and only its member is pushed.
        /// Saved with the behaviour's state: setting it to false stops synchronization for every later call in the scope,
        /// from any player, until an endpoint sets it back to true.
        /// </remarks>
        public bool AutoSynchronize = true;

        /// <summary>
        /// Sends a push message to every session member, or for a player scope to its owner.
        /// </summary>
        /// <param name="messageType">The message type</param>
        /// <param name="messageObject">The message object which will be serialized to json</param>
        /// <returns></returns>
        /// <remarks>
        /// The serialized message can be at most 51,200 bytes (50 KiB) of UTF-8. The push service rejects a larger one,
        /// which throws <see cref="CloudBehaviourException"/>.
        /// </remarks>
        /// <exception cref="CloudBehaviourException">
        /// A push to one or more players failed. Every recipient is tried first, so the others received the message. If it
        /// isn't caught, the call fails and its state changes are discarded.
        /// </exception>
        public async Task SendMessageAsync(string messageType, object messageObject)
        {
            var message = JsonConvert.SerializeObject(messageObject);
            var failures = new Dictionary<string, string>();
            foreach (var player in PushRecipients())
            {
                try
                {
                    var reply = await Push.SendPlayerMessageAsync(Context, message, messageType, player);
                    if (!string.IsNullOrEmpty(reply?.Error))
                    {
                        failures[player] = reply.Error;
                    }
                }
                catch (System.Exception e)
                {
                    failures[player] = $"{e.GetType().Name}: {e.Message}";
                }
            }

            if (failures.Count > 0)
            {
                throw new CloudBehaviourException(messageType, failures);
            }
        }

        // A player scope's only client is its owner, whose player ID is the ScopeId.
        IReadOnlyList<string> PushRecipients()
        {
            var players = Context?.Session?.PlayerIds;
            if (players != null)
            {
                // Each player is pushed to once.
                return players.Distinct().ToList();
            }

            var player = IsPlayerScoped() ? Context?.ScopeId : Context?.PlayerId;
            return player != null ? new[] { player } : System.Array.Empty<string>();
        }

        bool IsPlayerScoped() => GetType().GetCustomAttribute<StateScopeAttribute>()?.StateScope == Scope.Player;

        /// <summary>
        /// Sends a push message to all players in the scope to synchronize the values.
        /// Use when disabling AutoSynchronize for manual control.
        /// </summary>
        /// <returns>The task for the synchronization</returns>
        /// <exception cref="CloudBehaviourException">
        /// A push to one or more players failed, or, for a behaviour that publishes to its scope's channel, the push to
        /// the scope failed and <see cref="CloudBehaviourException.FailedPlayers"/> holds the session ID. Every recipient is
        /// tried first, so the others received the new state. If
        /// it's caught and <see cref="AutoSynchronize"/> is on, the automatic synchronization at the end of the call tries
        /// the failed players again. If it isn't caught, the call fails and its state changes are discarded, while the
        /// players who received it keep it.
        /// </exception>
        public Task SynchronizeAsync()
        {
            return Synchronizer?.SynchronizeAsync()
                ?? throw new System.InvalidOperationException(
                "No ICloudSynchronizer is attached to this behaviour. "
                + "This is required for notification of changes to synchronized values.");
        }

#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Attaches the framework services to a <see cref="CloudBehaviour"/>.
    /// </summary>
    /// <remarks>
    /// Supports generated code only and is not part of the public API: its shape can change in any release.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class CloudBehaviourServices
    {
        /// <summary>Sets <see cref="CloudBehaviour.Logger"/>.</summary>
        /// <param name="behaviour">The behaviour to update.</param>
        /// <param name="logger">The logger.</param>
        public static void SetLogger(CloudBehaviour behaviour, ILogger logger) => behaviour.Logger = logger;

        /// <summary>Sets <see cref="CloudBehaviour.Context"/>.</summary>
        /// <param name="behaviour">The behaviour to update.</param>
        /// <param name="context">The current execution context.</param>
        public static void SetContext(CloudBehaviour behaviour, IExecutionContext context) => behaviour.Context = context;

        /// <summary>Sets <see cref="CloudBehaviour.Push"/>.</summary>
        /// <param name="behaviour">The behaviour to update.</param>
        /// <param name="push">The push client.</param>
        public static void SetPush(CloudBehaviour behaviour, IPushClient push) => behaviour.Push = push;

        /// <summary>Sets <see cref="CloudBehaviour.Synchronizer"/>.</summary>
        /// <param name="behaviour">The behaviour to update.</param>
        /// <param name="synchronizer">The synchronizer.</param>
        public static void SetSynchronizer(CloudBehaviour behaviour, ICloudSynchronizer synchronizer) => behaviour.Synchronizer = synchronizer;
    }

    /// <summary>
    /// Identifies what went wrong when a <see cref="CloudBehaviourException"/> is thrown, so module code can handle
    /// each kind of failure differently.
    /// </summary>
    public enum CloudBehaviourExceptionReason
    {
        /// <summary>Unknown</summary>
        Unknown = 0,

        /// <summary>
        /// A push from <see cref="CloudBehaviour.SendMessageAsync"/> or <see cref="CloudBehaviour.SynchronizeAsync"/>
        /// failed for one or more players. <see cref="CloudBehaviourException.FailedPlayers"/> lists them.
        /// </summary>
        PushFailed = 1
    }

    /// <summary>
    /// Thrown to module code when a Cloud Behaviour operation fails, such as a push from
    /// <see cref="CloudBehaviour.SendMessageAsync"/> or <see cref="CloudBehaviour.SynchronizeAsync"/>.
    /// </summary>
    /// <remarks>
    /// For <see cref="CloudBehaviourExceptionReason.PushFailed"/>, every recipient is tried before this is thrown, so
    /// players not in <see cref="FailedPlayers"/> received the push.
    /// </remarks>
    public sealed class CloudBehaviourException : System.Exception
    {
        /// <summary>What went wrong.</summary>
        public CloudBehaviourExceptionReason Reason { get; }

        /// <summary>The type of the push that failed, for <see cref="CloudBehaviourExceptionReason.PushFailed"/>.</summary>
        public string MessageType { get; }

        /// <summary>
        /// The players that didn't receive the push, each with the reason, for
        /// <see cref="CloudBehaviourExceptionReason.PushFailed"/>. A behaviour that publishes to its scope's channel
        /// (a session behaviour without <c>[SyncView]</c> projections) sends one push for the whole session, so a
        /// failed <see cref="CloudBehaviour.SynchronizeAsync"/> has a single entry, keyed by the session ID rather
        /// than a player ID.
        /// </summary>
        public IReadOnlyDictionary<string, string> FailedPlayers { get; }

        /// <summary>Initializes a <see cref="CloudBehaviourExceptionReason.PushFailed"/> exception for the given players.</summary>
        /// <param name="messageType">The type of the push that failed.</param>
        /// <param name="failedPlayers">The players that didn't receive the push, each with the reason.</param>
        public CloudBehaviourException(string messageType, IReadOnlyDictionary<string, string> failedPlayers)
            : base(Describe(messageType, failedPlayers))
        {
            Reason = CloudBehaviourExceptionReason.PushFailed;
            MessageType = messageType;
            FailedPlayers = failedPlayers;
        }

        static string Describe(string messageType, IReadOnlyDictionary<string, string> failedPlayers)
        {
            var players = new List<string>();
            foreach (var failure in failedPlayers)
            {
                players.Add($"{failure.Key} ({failure.Value})");
            }
            return $"Failed to push {messageType} to {string.Join(", ", players)}";
        }
    }
#endif

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
