using System.Threading.Tasks;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// Readies the Cloud Code invoker for the linked project so the first module call is not delayed
    /// by provisioning. Registered in the services registry beside <see cref="ICloudCodeService"/>
    /// and resolved by <see cref="ModuleHostWarmup"/>, which owns retries, backoff and cancellation.
    /// </summary>
    /// <remarks>
    /// Internal on purpose. Every member of <see cref="ICloudCodeService"/> follows the client's retry
    /// configuration, and this call must not: it disables those retries so that each provisioning failure
    /// reaches <see cref="ModuleHostWarmup"/> as it happens. Putting it on the public interface would make it the
    /// one member that behaves differently from its siblings, and a caller using it directly would see a 503
    /// where every other call would have quietly retried. Keeping it behind an internal registry key gives
    /// <see cref="ModuleHostWarmup"/>, which is public, a way to reach the transport without exposing the
    /// single-attempt call to users.
    /// </remarks>
    internal interface ICloudCodeWarmup
    {
        /// <summary>
        /// Makes one request to the warm route. The service may hold the request while an invoker
        /// provisions, and answers as soon as one is running. Cheap once the invoker is warm, so
        /// callers may repeat it.
        /// </summary>
        /// <returns>A task that completes when an invoker has answered.</returns>
        /// <exception cref="CloudCodeException">
        /// The invoker is not ready or the request was rejected. <see cref="CloudCodeExceptionReason.ServiceUnavailable"/>
        /// and <see cref="CloudCodeExceptionReason.ScriptError"/> mean it is still provisioning and the call can be retried;
        /// <see cref="CloudCodeExceptionReason.PlayerIdMissing"/> and <see cref="CloudCodeExceptionReason.AccessTokenMissing"/>
        /// mean no player is signed in.
        /// </exception>
        /// <exception cref="CloudCodeRateLimitedException">The service returned a rate-limit error.</exception>
        Task WarmInvokerAsync();
    }
}
