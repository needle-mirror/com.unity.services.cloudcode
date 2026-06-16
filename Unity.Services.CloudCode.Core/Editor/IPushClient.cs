using System.Threading.Tasks;
using Unity.Services.CloudCodePush.Model;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    /// The Push Client is used to send push notifications to players or all players within a project.
    /// </summary>
    public interface IPushClient
    {
        /// <summary>
        /// Generate a token for a player that is used by the client to subscribe to push messages for that individual player.
        /// </summary>
        /// <param name="executionContext">The Cloud Code execution context.</param>
        /// <param name="playerId">Optional target playerId, if missing or different from the playerId in the execution context.</param>
        /// <returns>A task eventually representing the response data for generating a token.</returns>
        Task<GenerateTokenReply> GeneratePlayerTokenAsync(IExecutionContext executionContext, string playerId = null);

        /// <summary>
        /// Generate a token for a player that is used by the client to subscribe to push messages for all players within a project.
        /// </summary>
        /// <param name="executionContext">The Cloud Code execution context.</param>
        /// <param name="playerId">Optional target playerId, if missing or different from the playerId in the execution context.</param>
        /// <returns>A task eventually representing the response data for generating a token.</returns>
        Task<GenerateTokenReply> GenerateProjectTokenAsync(IExecutionContext executionContext, string playerId = null);

        /// <summary>
        /// Send a message to an individual player.
        /// </summary>
        /// <param name="executionContext">The Cloud Code execution context.</param>
        /// <param name="message">The message to be sent. Maximum size: 10 kilobytes</param>
        /// <param name="messageType">The user defined message type to be sent. Maximum length: 128 characters</param>
        /// <param name="playerId">Optional target playerId, if missing or different from the playerId in the execution context.</param>
        /// <returns>A task eventually representing the response data for sending a message.</returns>
        Task<SendMessageReply> SendPlayerMessageAsync(IExecutionContext executionContext, string message, string messageType = null, string playerId = null);

        /// <summary>
        /// Send a message to all players in a project.
        /// </summary>
        /// <param name="executionContext">The Cloud Code execution context.</param>
        /// <param name="message">The message to be sent. Maximum size: 10 kilobytes</param>
        /// <param name="messageType">The user defined message type to be sent. Maximum length: 128 characters</param>
        /// <returns>A task eventually representing the response data for sending a message.</returns>
        Task<SendMessageReply> SendProjectMessageAsync(IExecutionContext executionContext, string message, string messageType = null);

        /// <summary>
        /// Force a client to unsubscribe from push notifications for an individual player.
        /// </summary>
        /// <param name="executionContext">The Cloud Code execution context.</param>
        /// <param name="playerId">Optional target playerId, if missing or different from the playerId in the execution context.</param>
        /// <returns>A task eventually representing the response data for unsubscribing a player.</returns>
        Task<ForceUnsubscribeReply> ForceUnsubscribeFromPlayerMessagesAsync(IExecutionContext executionContext, string playerId = null);

        /// <summary>
        /// Force a client to unsubscribe from push notifications for all project wide messages.
        /// </summary>
        /// <param name="executionContext">The Cloud Code execution context.</param>
        /// <param name="playerId">Optional target playerId, if missing or different from the playerId in the execution context.</param>
        /// <returns>A task eventually representing the response data for unsubscribing a player.</returns>
        Task<ForceUnsubscribeReply> ForceUnsubscribeFromProjectMessagesAsync(IExecutionContext executionContext, string playerId = null);
    }
}
