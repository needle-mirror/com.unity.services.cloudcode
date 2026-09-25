using System;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Identifies what went wrong when a <see cref="CloudBehaviourException"/> is raised, so each kind of
    /// failure can be handled differently.
    /// </summary>
    public enum CloudBehaviourExceptionReason
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
        InvokerUnavailable = 5
    }

    /// <summary>
    /// Reports a failure raised through the <c>Failed</c> event of a scope provider or its player
    /// connection, or the <c>Fatal</c> event of a generated client, rather than thrown.
    /// </summary>
    /// <remarks>
    /// A failure originating in an underlying operation carries it as
    /// <see cref="Exception.InnerException"/> so that operation's stack trace survives.
    /// <see cref="Exception.StackTrace"/> is null on this instance: it is raised through an event and
    /// never thrown, and the runtime records a stack trace only at the throw site.
    /// </remarks>
    public sealed class CloudBehaviourException : Exception
    {
        /// <summary>
        /// The reason the failure was raised, selected from the CloudBehaviourExceptionReason enum.
        /// </summary>
        public CloudBehaviourExceptionReason Reason { get; }

        /// <summary>
        /// Creates a failure for the given reason. The message is determined by <paramref name="reason"/>,
        /// so it always describes the failure actually being reported.
        /// </summary>
        /// <param name="reason">Why the failure was raised.</param>
        /// <param name="detail">
        /// Optional context appended to the message, for values only the raise site knows.
        /// </param>
        /// <param name="innerException">The exception that caused the failure, when there was one.</param>
        public CloudBehaviourException(
            CloudBehaviourExceptionReason reason, string detail = null, Exception innerException = null)
            : base(BuildMessage(reason, detail), innerException)
        {
            Reason = reason;
        }

        static string BuildMessage(CloudBehaviourExceptionReason reason, string detail)
        {
            var message = reason switch
            {
                CloudBehaviourExceptionReason.SubscriptionError =>
                    "The player's message subscription could not be opened.",
                CloudBehaviourExceptionReason.Kicked =>
                    "The subscription channel was closed by the service.",
                CloudBehaviourExceptionReason.AmbiguousSessions =>
                    "More than one active session was found; only one is supported.",
                CloudBehaviourExceptionReason.HydrationFailedOnConnect =>
                    "The current server state could not be fetched when the scope resolved.",
                CloudBehaviourExceptionReason.InvokerUnavailable =>
                    "The project's Cloud Code invoker could not be started when the scope resolved.",
                _ => "An unknown cloud behaviour failure occurred.",
            };

            return string.IsNullOrEmpty(detail) ? message : $"{message} {detail}";
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Reason}: {base.ToString()}";
    }
}
