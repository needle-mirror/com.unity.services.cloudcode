using System.ComponentModel;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// Creates the <see cref="CloudCodeException"/> a generated Cloud Behaviour client throws.
    /// </summary>
    /// <remarks>
    /// Supports generated code only and is not part of the public API: its shape can change in any release.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class CloudBehaviourFailures
    {
        /// <summary>
        /// The failure for a call whose result returned after the client's scope changed.
        /// </summary>
        /// <param name="detail">Context appended to the message, such as the client and endpoint names.</param>
        /// <returns>An exception with reason <see cref="CloudCodeExceptionReason.ScopeChangedDuringCall"/>.</returns>
        public static CloudCodeException ScopeChangedDuringCall(string detail) =>
            new CloudCodeException(CloudCodeExceptionReason.ScopeChangedDuringCall, CommonErrorCodes.Unknown,
                "The call ran on the server, but the client's scope changed before its result returned. The result was not applied. "
                + "The call took effect in the previous scope's server state; retrying it would run it against the current scope. "
                + detail, null);
    }
}
