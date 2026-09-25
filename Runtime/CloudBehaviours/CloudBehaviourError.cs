using System;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Identifies what went wrong when a <see cref="CloudBehaviourError"/> is raised, so each kind of
    /// failure can be handled differently.
    /// </summary>
    public enum CloudBehaviourErrorReason
    {
        /// <summary>Unknown</summary>
        Unknown = 0,

        /// <summary>
        /// A message from the behaviour — a synchronization or a push event — could not be read or
        /// applied, usually because its payload no longer matches this client's generated types after a
        /// redeploy. The message was dropped: synchronized fields are unchanged, no push event or
        /// <c>MessageReceived</c> was raised for it, and the client stays connected for the next one.
        /// </summary>
        MessageError = 1
    }

    /// <summary>
    /// Reports a non-terminal failure — one that did not stop a cloud behaviour client — raised through
    /// its <c>Error</c> event. The client keeps running and keeps its scope.
    /// </summary>
    /// <remarks>
    /// A failure originating in an underlying operation carries it as
    /// <see cref="Exception.InnerException"/> so that operation's stack trace survives.
    /// <see cref="Exception.StackTrace"/> is null on this instance: it is raised through an event and
    /// never thrown, and the runtime records a stack trace only at the throw site.
    /// </remarks>
    public sealed class CloudBehaviourError : Exception
    {
        /// <summary>
        /// The reason the failure was raised, selected from the CloudBehaviourErrorReason enum.
        /// </summary>
        public CloudBehaviourErrorReason Reason { get; }

        /// <summary>
        /// Creates a failure for the given reason. The message is determined by <paramref name="reason"/>,
        /// so it always describes the failure actually being reported.
        /// </summary>
        /// <param name="reason">Why the failure was raised.</param>
        /// <param name="detail">
        /// Optional context appended to the message, for values only the raise site knows.
        /// </param>
        /// <param name="innerException">The exception that caused the failure, when there was one.</param>
        public CloudBehaviourError(
            CloudBehaviourErrorReason reason, string detail = null, Exception innerException = null)
            : base(BuildMessage(reason, detail), innerException)
        {
            Reason = reason;
        }

        static string BuildMessage(CloudBehaviourErrorReason reason, string detail)
        {
            var message = reason switch
            {
                CloudBehaviourErrorReason.MessageError =>
                    "A message from the behaviour could not be read or applied.",
                _ => "An unknown cloud behaviour error occurred.",
            };

            return string.IsNullOrEmpty(detail) ? message : $"{message} {detail}";
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Reason}: {base.ToString()}";
    }
}
