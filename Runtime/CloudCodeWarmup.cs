using System;
using System.Threading.Tasks;
using Unity.Services.Authentication.Internal;
using Unity.Services.CloudCode.Internal;
using Unity.Services.CloudCode.Internal.Apis.CloudCode;
using Unity.Services.CloudCode.Internal.CloudCode;
using Unity.Services.CloudCode.Internal.ErrorMitigation;
using Unity.Services.CloudCode.Internal.Http;
using Unity.Services.CloudCode.Internal.Models;
using Unity.Services.Core;
using Unity.Services.Core.Configuration.Internal;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// Makes one request to the Cloud Code warm route with the HTTP client's own retries turned off.
    /// <see cref="ModuleHostWarmup"/> owns the retry policy, backoff and cancellation, so every failure has to
    /// surface here as it happens rather than being absorbed by the transport.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="ModuleHostWarmup"/> because that class is public and constructed by user code,
    /// often before services exist, while this one needs the API client, project id, player id and access
    /// token, all internal components that only exist once Core has initialized. <see cref="CloudCodeInitializer"/>
    /// builds one alongside the service and registers it under <see cref="ICloudCodeWarmup"/>; the warm-up
    /// resolves it from the registry at sign-in. Separate from <see cref="CloudCodeInternal"/> so the service
    /// class carries no warm-specific code and its public contract stays free of a member that ignores the
    /// client's retry configuration.
    /// </remarks>
    internal sealed class CloudCodeWarmup : ICloudCodeWarmup
    {
        readonly ICloudCodeApiClient m_ApiClient;
        readonly ICloudProjectId m_CloudProjectId;
        readonly IPlayerId m_PlayerId;
        readonly IAccessToken m_AccessToken;

        internal CloudCodeWarmup(ICloudProjectId cloudProjectId, ICloudCodeApiClient apiClient, IPlayerId playerId,
                                 IAccessToken accessToken)
        {
            m_CloudProjectId = cloudProjectId;
            m_ApiClient = apiClient;
            m_PlayerId = playerId;
            m_AccessToken = accessToken;
        }

        public async Task WarmInvokerAsync()
        {
            ValidateRequiredDependencies();

            try
            {
                var noStatusRetries = new StatusCodePolicyConfig();
                noStatusRetries.Clear();
                var attemptConfiguration = new Configuration(null, null, 0, null,
                    new RetryPolicyConfig { MaxRetries = 0 }, noStatusRetries);

                await m_ApiClient.WarmInvokerAsync(
                    new WarmInvokerRequest(m_CloudProjectId.GetCloudProjectId()), attemptConfiguration);
            }
            catch (HttpException<BasicErrorResponse> e)
            {
                throw CloudCodeInternal.CreateException(e.Response.IsNetworkError, e.Response.StatusCode,
                    e.ActualError?.Code ?? (int)e.Response.StatusCode, e.Message, e);
            }
            catch (HttpException e)
            {
                throw CloudCodeInternal.CreateException(e.Response.IsNetworkError, e.Response.StatusCode,
                    (int)e.Response.StatusCode, e.Message, e);
            }
            catch (Exception e)
            {
                throw new CloudCodeException(CloudCodeExceptionReason.Unknown, CommonErrorCodes.Unknown, e.Message, e);
            }
        }

        void ValidateRequiredDependencies()
        {
            if (String.IsNullOrEmpty(m_CloudProjectId.GetCloudProjectId()))
            {
                throw new CloudCodeException(CloudCodeExceptionReason.ProjectIdMissing, CommonErrorCodes.Unknown,
                    "Project ID is missing - make sure the project is correctly linked to your game and try again.", null);
            }

            if (String.IsNullOrEmpty(m_PlayerId.PlayerId))
            {
                throw new CloudCodeException(CloudCodeExceptionReason.PlayerIdMissing, CommonErrorCodes.Unknown,
                    "Player ID is missing - ensure you are signed in through the Authentication SDK and try again.", null);
            }

            if (String.IsNullOrEmpty(m_AccessToken.AccessToken))
            {
                throw new CloudCodeException(CloudCodeExceptionReason.AccessTokenMissing, CommonErrorCodes.InvalidToken,
                    "Access token is missing - ensure you are signed in through the Authentication SDK and try again.", null);
            }
        }
    }
}
