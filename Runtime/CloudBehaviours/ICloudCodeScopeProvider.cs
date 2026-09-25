using System;
using Unity.Services.CloudCode;
using Unity.Services.CloudCode.Subscriptions;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Publishes the <see cref="CloudCodeScope"/> that cloud behaviour calls are bound to, and the
    /// lifecycle around it.
    /// </summary>
    /// <remarks>
    /// The common shape shared by the player-scoped and session-scoped providers. It describes that
    /// shared lifecycle; generated bindings hold their provider by its concrete type rather than
    /// through this interface.
    /// </remarks>
    public interface ICloudCodeScopeProvider
    {
        /// <summary>
        /// Raised whenever <see cref="Scope"/> changes, including when it is withdrawn. Not raised when
        /// an update resolves to the scope that is already published.
        /// </summary>
        event Action ScopeChanged;

        /// <summary>
        /// Raised after the provider has disabled itself in response to a failure, so it must be
        /// re-enabled to recover.
        /// </summary>
        event Action<CloudBehaviourException> Failed;

        /// <summary>
        /// Raised for each message received on the player's subscription. Suppressed while
        /// <see cref="Scope"/> is <c>null</c>.
        /// </summary>
        event Action<IMessageReceivedEvent> MessageReceived;

        /// <summary>
        /// The scope to bind calls to, or <c>null</c> while none is available.
        /// </summary>
        CloudCodeScope Scope { get; }

        /// <summary>
        /// The Cloud Code service resolved from the same registry this provider derives its scope
        /// from. Callers invoking endpoints on a provider's behalf should use this, so scope and
        /// calls never diverge across registries.
        /// </summary>
        ICloudCodeService CloudCode { get; }

        /// <summary>
        /// Starts tracking. Calling this on an already-enabled provider does nothing.
        /// </summary>
        void Enable();

        /// <summary>
        /// Stops tracking, withdraws <see cref="Scope"/> and releases the underlying connection.
        /// </summary>
        void Disable();
    }
}
