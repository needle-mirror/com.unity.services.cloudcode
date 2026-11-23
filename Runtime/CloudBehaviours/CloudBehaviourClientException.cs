using System;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Identifies what went wrong when a <see cref="CloudBehaviourClientException"/> is raised, so each kind of
    /// failure can be handled differently.
    /// </summary>
    public enum CloudBehaviourClientExceptionReason
    {
        /// <summary>Unknown</summary>
        Unknown = 0,

        /// <summary>
        /// The player's message subscription could not be opened, so no messages will arrive and no
        /// scope is published. Re-enable the connection or provider to retry.
        /// </summary>
        SubscriptionError = 1,

        /// <summary>
        /// The subscription channel was closed by the service. Nothing reopens it on its own, so
        /// re-enable the connection or provider to open a new one.
        /// </summary>
        Kicked = 2,

        /// <summary>
        /// The multiplayer service reported more than one active session while
        /// <c>MultiplayerScopeProvider</c> was enabled without being told which to follow, and it cannot
        /// choose between them. Enable it with a specific session, session type, observer or
        /// <c>MultiplayerSession</c> asset instead.
        /// </summary>
        AmbiguousSessions = 3,

        /// <summary>
        /// A generated client could not fetch the current server state when its scope resolved, so it
        /// stopped before announcing the scope. Its synchronized fields still hold their previous or
        /// default values. Call <c>EnableClient()</c>, from outside the handler, to try again.
        /// </summary>
        HydrationFailedOnConnect = 4,

        /// <summary>
        /// The project's Cloud Code invoker could not be started when a generated client's scope
        /// resolved, so no call could run and the client stopped before hydrating. Call
        /// <c>EnableClient()</c>, from outside the handler, to ask for another warm-up.
        /// </summary>
        InvokerUnavailable = 5,

        /// <summary>
        /// A message from the behaviour — a synchronization or a push event — could not be read or
        /// applied, usually because its payload no longer matches this client's generated types after a
        /// redeploy. The message was dropped: synchronized fields are unchanged, no push event or
        /// <c>MessageReceived</c> was raised for it, and the client stays connected for the next one.
        /// </summary>
        MessageError = 6,

        /// <summary>
        /// A generated client's subscription reconnected, but the client could not fetch the server state
        /// that changed while it was down. Raised through <c>Error</c>: the client stays connected, and its
        /// synchronized fields keep their earlier values until the next message, call or <c>Hydrate()</c>.
        /// </summary>
        HydrationFailedOnReconnect = 7
    }

    /// <summary>
    /// Reports a Cloud Behaviour failure through an event rather than by throwing. The event says whether the
    /// client stopped: <c>Failed</c> on a scope provider or its player connection and <c>Fatal</c> on a generated
    /// client mean it did; <c>Error</c> on a generated client means it carries on.
    /// </summary>
    /// <remarks>
    /// A failure originating in an underlying operation carries it as
    /// <see cref="Exception.InnerException"/> so that operation's stack trace survives.
    /// <see cref="Exception.StackTrace"/> is null on this instance: it is raised through an event and
    /// never thrown, and the runtime records a stack trace only at the throw site.
    /// A call that fails throws <see cref="Unity.Services.CloudCode.CloudCodeException"/> instead.
    /// </remarks>
    public sealed class CloudBehaviourClientException : Exception
    {
        /// <summary>
        /// The reason the failure was raised, selected from the CloudBehaviourClientExceptionReason enum.
        /// </summary>
        public CloudBehaviourClientExceptionReason Reason { get; }

        /// <summary>
        /// Creates a failure for the given reason. The message is determined by <paramref name="reason"/>,
        /// so it always describes the failure actually being reported.
        /// </summary>
        /// <param name="reason">Why the failure was raised.</param>
        /// <param name="detail">
        /// Optional context appended to the message, for values only the raise site knows.
        /// </param>
        /// <param name="innerException">The exception that caused the failure, when there was one.</param>
        public CloudBehaviourClientException(
            CloudBehaviourClientExceptionReason reason, string detail = null, Exception innerException = null)
            : base(BuildMessage(reason, detail), innerException)
        {
            Reason = reason;
        }

        static string BuildMessage(CloudBehaviourClientExceptionReason reason, string detail)
        {
            var message = reason switch
            {
                CloudBehaviourClientExceptionReason.SubscriptionError =>
                    "The player's message subscription could not be opened.",
                CloudBehaviourClientExceptionReason.Kicked =>
                    "The subscription channel was closed by the service.",
                CloudBehaviourClientExceptionReason.AmbiguousSessions =>
                    "More than one active session was found; only one is supported.",
                CloudBehaviourClientExceptionReason.HydrationFailedOnConnect =>
                    "The current server state could not be fetched when the scope resolved.",
                CloudBehaviourClientExceptionReason.InvokerUnavailable =>
                    "The project's Cloud Code invoker could not be started when the scope resolved.",
                CloudBehaviourClientExceptionReason.MessageError =>
                    "A message from the behaviour could not be read or applied.",
                CloudBehaviourClientExceptionReason.HydrationFailedOnReconnect =>
                    "The current server state could not be fetched after the subscription reconnected.",
                _ => "An unknown cloud behaviour failure occurred.",
            };

            return string.IsNullOrEmpty(detail) ? message : $"{message} {detail}";
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Reason}: {base.ToString()}";
    }
}
