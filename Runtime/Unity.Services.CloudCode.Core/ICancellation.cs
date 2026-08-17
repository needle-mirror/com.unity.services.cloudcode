using System.Threading;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    /// Provides cancellation tokens for Cloud Code request handling and long-running operations.
    /// </summary>
    public interface ICancellation
    {
        /// <summary>
        ///     Cancellation token for requests. Cancelled on request timeout or shutdown.
        /// </summary>
        CancellationToken Token { get; }

        /// <summary>
        ///     Cancellation token for running asynchronous long-running jobs. Cancelled on shutdown.
        /// </summary>
        CancellationToken ShutdownToken { get; }
    }
}
