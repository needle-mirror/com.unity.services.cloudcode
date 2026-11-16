#nullable enable
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Apis.Triggers
{
    /// <summary>
    /// The Triggers Event Client is used to publish custom events to the Triggers service, which can then run other
    /// actions in response to those events.
    /// </summary>
    /// <remarks>
    /// These are service events that are used by the Triggers service. They never reach players. To send a message to players, use
    /// <see cref="IPushClient"/>. Raising a C# event on a Cloud Behaviour also pushes to that behaviour's players.
    /// To manage trigger configurations, use the Triggers admin API instead.
    /// Cloud Code provides this client, so inject it without registering it in your module setup.
    /// </remarks>
    public interface ITriggersEventClient
    {
        /// <summary>
        /// Publish a custom event to the Triggers service.
        /// </summary>
        /// <remarks>
        /// Pass the short event name only. Cloud Code adds its own namespace and version to form the full event type,
        /// and sets the event's source. Cloud Code also attaches some event context automatically, notably the scope the
        /// function's module runs in (a player, a multiplayer session, or none), and the caller's release and variant tags.
        /// </remarks>
        /// <param name="executionContext">The Cloud Code execution context.</param>
        /// <param name="eventName">The short name of the event, such as <c>match-ended</c>, without a namespace.</param>
        /// <param name="payload">
        /// The event payload data that is passed in the trigger. Filters can also read it. Can be null for an event with no data. Treat the keys as
        /// a contract with existing triggers; add keys freely, but to remove or rename one, publish a new event name.
        /// </param>
        /// <returns>
        /// A task eventually representing the ID of the published event. The ID means Cloud Code accepted the event,
        /// not that Triggers has received it, as delivery happens asynchronously.
        /// </returns>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="eventName"/> is not a valid event name.</exception>
        Task<string> PublishEventAsync(IExecutionContext executionContext, string eventName, Dictionary<string, object>? payload = null);
    }
}
