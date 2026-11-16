using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Subscriptions;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// Client SDK for Cloud Code.
    /// https://dashboard.unity3d.com/cloud-code
    ///
    /// Streamline your game code in the cloud. Cloud Code shifts your game logic away from your servers, interacting seamlessly with backend services.
    /// </summary>
    public interface ICloudCodeService
    {
        /// <summary>
        /// Calls a Cloud Code function.
        /// </summary>
        /// <param name="function">Cloud Code function to call</param>
        /// <param name="args">Arguments for the cloud code function. Will be serialized to JSON.</param>
        /// <returns>String representation of the return value of the called function. Intended to enable custom serializers.</returns>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        /// <exception cref="CloudCodeRateLimitedException">Thrown if the service returned rate limited error.</exception>
        Task<string> CallEndpointAsync(string function, Dictionary<string, object> args = null);

        /// <summary>
        /// Calls a Cloud Code function.
        /// </summary>
        /// <param name="function">Cloud Code function to call.</param>
        /// <param name="args">Arguments for the cloud code function. Will be serialized to JSON.</param>
        /// <typeparam name="TResult">Serialized from JSON returned by Cloud Code.</typeparam>
        /// <returns>Serialized output from the called function.</returns>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        /// <exception cref="CloudCodeRateLimitedException">Thrown if the service returned rate limited error.</exception>
        Task<TResult> CallEndpointAsync<TResult>(string function, Dictionary<string, object> args = null);

        /// <summary>
        /// Calls a Cloud Code function.
        /// </summary>
        /// <param name="module">Cloud Code Module to call</param>
        /// <param name="function">Cloud Code function to call.</param>
        /// <param name="args">Arguments for the cloud code function. Will be serialized to JSON.</param>
        /// <param name="scope">Optional scope type and ID, which provide statefulness across invocations with the same ID</param>
        /// <returns>String representation of the return value of the called function. Intended to enable custom serializers.</returns>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        /// <exception cref="CloudCodeRateLimitedException">Thrown if the service returned rate limited error.</exception>
        Task<string> CallModuleEndpointAsync(string module, string function, Dictionary<string, object> args = null, CloudCodeScope scope = null);

        /// <summary>
        /// Calls a Cloud Code function.
        /// </summary>
        /// <param name="module">Cloud Code Module to call</param>
        /// <param name="function">Cloud Code function to call.</param>
        /// <param name="args">Arguments for the cloud code function. Will be serialized to JSON.</param>
        /// <param name="scope">Optional scope type and ID, which provide statefulness across invocations with the same ID</param>
        /// <typeparam name="TResult">Serialized from JSON returned by Cloud Code.</typeparam>
        /// <returns>Serialized output from the called function.</returns>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        /// <exception cref="CloudCodeRateLimitedException">Thrown if the service returned rate limited error.</exception>
        Task<TResult> CallModuleEndpointAsync<TResult>(string module, string function, Dictionary<string, object> args = null, CloudCodeScope scope = null);

        /// <summary>
        /// Subscribe to push messages from the Cloud Code service for the currently logged in player.
        /// </summary>
        /// <returns>SubscriptionEvents object that can be used to subscribe or unsubscribe from messages.</returns>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        Task<ISubscriptionEvents> SubscribeToPlayerMessagesAsync();

        /// <summary>
        /// Subscribe to push messages from the Cloud Code service for the currently logged in player.
        /// </summary>
        /// <returns>SubscriptionEvents object that can be used to subscribe or unsubscribe from messages.</returns>
        /// <param name="callbacks">SubscriptionEvents that will be registered.</param>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        Task<ISubscriptionEvents> SubscribeToPlayerMessagesAsync(SubscriptionEventCallbacks callbacks);

        /// <summary>
        /// Subscribe to push messages from the Cloud Code service for all project-wide messages.
        /// </summary>
        /// <returns>SubscriptionEvents object that can be used to subscribe or unsubscribe from messages.</returns>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        Task<ISubscriptionEvents> SubscribeToProjectMessagesAsync();

        /// <summary>
        /// Subscribe to push messages from the Cloud Code service for all project-wide messages.
        /// </summary>
        /// <returns>SubscriptionEvents object that can be used to subscribe or unsubscribe from messages.</returns>
        /// <param name="callbacks">SubscriptionEvents that will be registered.</param>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        Task<ISubscriptionEvents> SubscribeToProjectMessagesAsync(SubscriptionEventCallbacks callbacks);

        /// <summary>
        /// Subscribe to push messages a module sends to a scope with <c>IPushClient.SendScopeMessageAsync</c>.
        /// Subscribers to the same scope share one channel.
        /// </summary>
        /// <remarks>While the scope's channel is open, a later subscriber joins it without calling its
        /// <paramref name="function"/>. The channel calls the function of the subscriber that opened it again on
        /// every reconnect, and a failure there shows as an <c>Error</c> connection state.</remarks>
        /// <param name="module">Cloud Code Module that issues the subscription token.</param>
        /// <param name="function">A function of <paramref name="module"/>, in the scope's class, that returns the
        /// reply of <c>IPushClient.GenerateScopeTokenAsync</c>. It runs in <paramref name="scope"/>, so its access
        /// rules decide who can open the scope's channel.</param>
        /// <param name="scope">The scope whose messages to receive.</param>
        /// <returns>SubscriptionEvents object that can be used to subscribe or unsubscribe from messages.</returns>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="module"/> or <paramref name="function"/> is empty, or <paramref name="scope"/> has no id.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the Wire package, which subscriptions need, is not installed.</exception>
        Task<ISubscriptionEvents> SubscribeToScopeMessagesAsync(string module, string function, CloudCodeScope scope);

        /// <summary>
        /// Subscribe to push messages a module sends to a scope with <c>IPushClient.SendScopeMessageAsync</c>.
        /// Subscribers to the same scope share one channel.
        /// </summary>
        /// <remarks>While the scope's channel is open, a later subscriber joins it without calling its
        /// <paramref name="function"/>. The channel calls the function of the subscriber that opened it again on
        /// every reconnect, and a failure there shows as an <c>Error</c> connection state.</remarks>
        /// <param name="module">Cloud Code Module that issues the subscription token.</param>
        /// <param name="function">A function of <paramref name="module"/>, in the scope's class, that returns the
        /// reply of <c>IPushClient.GenerateScopeTokenAsync</c>. It runs in <paramref name="scope"/>, so its access
        /// rules decide who can open the scope's channel.</param>
        /// <param name="scope">The scope whose messages to receive.</param>
        /// <param name="callbacks">SubscriptionEvents that will be registered.</param>
        /// <returns>SubscriptionEvents object that can be used to subscribe or unsubscribe from messages.</returns>
        /// <exception cref="CloudCodeException">Thrown if request is unsuccessful.</exception>
        /// <exception cref="System.ArgumentException">Thrown when <paramref name="module"/> or <paramref name="function"/> is empty, or <paramref name="scope"/> has no id.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when the Wire package, which subscriptions need, is not installed.</exception>
        Task<ISubscriptionEvents> SubscribeToScopeMessagesAsync(string module, string function, CloudCodeScope scope,
            SubscriptionEventCallbacks callbacks);
    }

    /// <summary>
    /// Provides access to the Cloud Code methods
    /// </summary>
    public sealed class CloudCodeService
    {
        /// <summary>
        /// The instance of the Cloud Code service
        /// </summary>
        public static ICloudCodeService Instance { get; internal set; }
    }
}
