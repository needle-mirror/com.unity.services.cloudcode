using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Unity.Services.Authentication.Internal;
using Unity.Services.CloudCode.Internal;
using Unity.Services.CloudCode.Internal.Apis.CloudCode;
using Unity.Services.CloudCode.Internal.CloudCode;
using Unity.Services.CloudCode.Internal.Http;
using Unity.Services.CloudCode.Internal.Models;
using Unity.Services.CloudCode.Subscriptions;
using Unity.Services.Core;
using Unity.Services.Core.Configuration.Internal;
using Unity.Services.Wire.Internal;
using UnityEngine;

namespace Unity.Services.CloudCode
{
    internal class CloudCodeInternal : ICloudCodeService
    {
        private readonly ICloudCodeApiClient m_ApiClient;
        private readonly ICloudProjectId m_CloudProjectId;
        private readonly IPlayerId m_PlayerId;
        private readonly IAccessToken m_AccessToken;
        readonly IWire m_Wire;
        readonly SubscriptionProvider m_SubscriptionProvider;
        readonly bool m_TargetsLocalServer;

        internal CloudCodeInternal(IWire wire, ICloudProjectId cloudProjectId, ICloudCodeApiClient cloudCodeApiClient, IPlayerId playerId, IAccessToken accessToken, bool targetsLocalServer = false)
        {
            m_TargetsLocalServer = targetsLocalServer;
            m_CloudProjectId = cloudProjectId;
            m_ApiClient = cloudCodeApiClient;
            m_PlayerId = playerId;
            m_AccessToken = accessToken;
            m_Wire = wire;
            m_SubscriptionProvider = new SubscriptionProvider(wire, cloudCodeApiClient, cloudProjectId, playerId);
        }

        public async Task<string> CallEndpointAsync(string function, Dictionary<string, object> args = null)
        {
            var result = await GetRunScriptResponse(function, args, null);
            var output = result?.Result?.Output.GetAs<object>();
            return output?.ToString();
        }

        public async Task<TResult> CallEndpointAsync<TResult>(string function, Dictionary<string, object> args = null)
        {
            var result = await GetRunScriptResponse(function, args, null);
            return DeserializeOutput<TResult>(result);
        }

        public async Task<string> CallModuleEndpointAsync(string module, string function, Dictionary<string, object> args = null, CloudCodeScope scope = null)
        {
            var result = await GetRunModuleScriptResponse(module, function, args, scope, null);
            var output = result?.Result?.Output.GetAs<object>();
            return output?.ToString();
        }

        public async Task<TResult> CallModuleEndpointAsync<TResult>(string module, string function, Dictionary<string, object> args = null, CloudCodeScope scope = null)
        {
            var result = await GetRunModuleScriptResponse(module, function, args, scope, null);
            return DeserializeOutput<TResult>(result);
        }


        public Task<ISubscriptionEvents> SubscribeToPlayerMessagesAsync()
        {
            return SubscribeToPlayerMessagesAsync(new SubscriptionEventCallbacks());
        }

        public async Task<ISubscriptionEvents> SubscribeToPlayerMessagesAsync(SubscriptionEventCallbacks callbacks)
        {
            ValidateWireDependency();
            ValidateRequiredDependencies();
            return await m_SubscriptionProvider.SubscribeAsync(callbacks, TokenProvider.TokenProviderMode.Player);
        }

        public Task<ISubscriptionEvents> SubscribeToProjectMessagesAsync()
        {
            return SubscribeToProjectMessagesAsync(new SubscriptionEventCallbacks());
        }

        public async Task<ISubscriptionEvents> SubscribeToProjectMessagesAsync(SubscriptionEventCallbacks callbacks)
        {
            ValidateWireDependency();
            ValidateRequiredDependencies();
            return await m_SubscriptionProvider.SubscribeAsync(callbacks, TokenProvider.TokenProviderMode.Project);
        }

        static readonly DeserializationSettings k_OutputSettings = new DeserializationSettings
        {
            MissingMemberHandling = MissingMemberHandling.Ignore
        };

        static TResult DeserializeOutput<TResult>(Response<RunScriptResponse> result)
        {
            return DeserializeOutput<TResult>(result.Result?.Output);
        }

        static TResult DeserializeOutput<TResult>(Response<RunModuleResponse> result)
        {
            return DeserializeOutput<TResult>(result.Result?.Output);
        }

        static TResult DeserializeOutput<TResult>(IDeserializable output)
        {
            // A function returning null still has an output; only an empty or "null" body has none.
            if (output == null)
            {
                throw new CloudCodeException(CloudCodeExceptionReason.DeserializationFailed, CommonErrorCodes.Unknown,
                    $"The Cloud Code response had no result to deserialize into {typeof(TResult).Name}.", null);
            }

            try
            {
                return output.GetAs<TResult>(k_OutputSettings);
            }
            catch (DeserializationException e)
            {
                throw new CloudCodeException(CloudCodeExceptionReason.DeserializationFailed, CommonErrorCodes.Unknown,
                    $"The Cloud Code result could not be deserialized into {typeof(TResult).Name}: {e.Message}", e);
            }
        }

        async Task<Response<RunScriptResponse>> GetRunScriptResponse(string function, Dictionary<string, object> args, string version)
        {
            ValidateRequiredDependencies();
            return await SendAsync(() => GetResponseAsync(function, args, version));
        }

        async Task<Response<RunModuleResponse>> GetRunModuleScriptResponse(string module, string function, Dictionary<string, object> args, CloudCodeScope scope, string version)
        {
            ValidateRequiredDependencies();
            return await SendAsync(() => GetModuleResponseAsync(module, function, args, scope, version), module);
        }

        async Task<T> SendAsync<T>(Func<Task<T>> request, string module = null)
        {
            try
            {
                return await request();
            }
            catch (HttpException<BasicErrorResponse> e)
            {
                throw BuildException(e, e.ActualError?.Code, module);
            }
            catch (HttpException<ValidationErrorResponse> e)
            {
                throw BuildException(e, e.ActualError?.Code, module);
            }
            catch (HttpException<InvocationErrorResponse> e)
            {
                throw BuildException(e, e.ActualError?.Code, module);
            }
            catch (HttpException e) when (e.Response != null)
            {
                // A status the client has no model for (e.g. 403) arrives untyped, with its body unread.
                var fallback = ToBasicHttpException(e.Response);
                throw BuildException(fallback, fallback.ActualError?.Code, module);
            }
            catch (ResponseDeserializationException e) when (e.response != null && (e.response.IsHttpError || e.response.IsNetworkError))
            {
                var fallback = ToBasicHttpException(e.response);
                throw BuildException(fallback, fallback.ActualError?.Code, module);
            }
            catch (ResponseDeserializationException e)
            {
                throw new CloudCodeException(CloudCodeExceptionReason.DeserializationFailed, CommonErrorCodes.Unknown, e.Message, e);
            }
            catch (Exception e)
            {
                throw new CloudCodeException(CloudCodeExceptionReason.Unknown, CommonErrorCodes.Unknown, e.Message, e);
            }
        }

        CloudCodeException BuildException(HttpException e, int? bodyErrorCode, string module)
        {
            var cloudCodeException = CreateException(e.Response.IsNetworkError, e.Response.StatusCode,
                ErrorCodeOf(e.Response, bodyErrorCode), e.Message, e);
            Debug.LogError(cloudCodeException.Message);
#if UNITY_EDITOR
            if (module != null
                && m_TargetsLocalServer
                && cloudCodeException.Reason == CloudCodeExceptionReason.NotFound)
            {
                Debug.LogWarning(
                    $"The local Cloud Code server may be running an older build of module '{module}'. Changes " +
                    "made in play mode are not deployed to it: exit play mode, wait for \"Local Cloud Code server " +
                    "reloaded\" in the Console, then enter play mode again.");
            }
#endif
            return cloudCodeException;
        }

        // Sent with a 422, so it must be checked before the status maps to ScriptError.
        const int ScopeStateIncompatibleErrorCode = 9052;


        /// <summary>
        /// The body's error code, or the HTTP status when there is no body or it carries no code.
        /// </summary>
        internal static int ErrorCodeOf(HttpClientResponse response, int? bodyErrorCode)
        {
            return bodyErrorCode.GetValueOrDefault() != 0 ? bodyErrorCode.Value : (int)response.StatusCode;
        }

        /// <summary>
        /// Rebuilds an error response the client could not parse (an unknown problem type, a non-JSON body) as a
        /// basic error, keeping whatever basic fields the body does carry.
        /// </summary>
        internal static HttpException<BasicErrorResponse> ToBasicHttpException(HttpClientResponse response)
        {
            BasicErrorResponse body = null;
            try
            {
                if (response.Data != null && response.Data.Length > 0)
                {
                    body = (BasicErrorResponse)IsolatedJsonConvert.DeserializeObject(
                        Encoding.UTF8.GetString(response.Data), typeof(BasicErrorResponse));
                }
            }
            catch (Exception)
            {
                // Not JSON at all: the status code is all there is to go on.
            }

            return new HttpException<BasicErrorResponse>(response, body);
        }

        internal static CloudCodeException CreateException(bool isNetworkError, long statusCode, int errorCode, string message, HttpException innerException)
        {
            var code = isNetworkError ? CommonErrorCodes.TransportError : errorCode;
            var reason = isNetworkError ? CloudCodeExceptionReason.NoInternetConnection : GetErrorReason(statusCode, errorCode);

            CloudCodeException cloudCodeException;
            if (statusCode == 429)
            {
                var retryAfter = 60;
                if (innerException.Response.Headers != null &&
                    innerException.Response.Headers.TryGetValue("Retry-After", out var retryAfterString))
                {
                    Int32.TryParse(retryAfterString, out retryAfter);
                }
                cloudCodeException = new CloudCodeRateLimitedException(reason, code, message, innerException, retryAfter);
            }
            else
            {
                cloudCodeException = new CloudCodeException(reason, code, message, innerException);
            }
            return cloudCodeException;
        }

        void ValidateWireDependency()
        {
            if (m_Wire == null)
            {
                throw new InvalidOperationException(
                    "Cannot subscribe to Cloud Code messages without the wire SDK dependency.");
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

        static CloudCodeExceptionReason GetErrorReason(long statusCode, int errorCode)
        {
            if (errorCode == ScopeStateIncompatibleErrorCode)
            {
                return CloudCodeExceptionReason.ScopeStateIncompatible;
            }


            switch (statusCode)
            {
                case 400:
                    return CloudCodeExceptionReason.InvalidArgument;
                case 401:
                    return CloudCodeExceptionReason.Unauthorized;
                case 403:
                    return CloudCodeExceptionReason.Forbidden;
                case 404:
                    return CloudCodeExceptionReason.NotFound;
                case 422:
                    return CloudCodeExceptionReason.ScriptError;
                case 429:
                    return CloudCodeExceptionReason.TooManyRequests;
                case 500:
                case 502:
                case 503:
                case 504:
                    return CloudCodeExceptionReason.ServiceUnavailable;
                default:
                    return CloudCodeExceptionReason.Unknown;
            }
        }

        async Task<Response<RunScriptResponse>> GetResponseAsync(string function, Dictionary<string, object> args, string version)
        {
            var runArgs = new RunScriptArguments(args ?? new Dictionary<string, object>(), NullIfBlank(version));
            var runScript = new RunScriptRequest(m_CloudProjectId.GetCloudProjectId(), function, runArgs);
            var task = m_ApiClient.RunScriptAsync(runScript);

            return await task;
        }

        async Task<Response<RunModuleResponse>> GetModuleResponseAsync(string module, string function, Dictionary<string, object> args, CloudCodeScope scope, string version)
        {
            var runArgs = new RunModuleArguments(args ?? new Dictionary<string, object>(), scope == null ? null : new RunModuleArgumentsScope(scope.ToInternalType(), scope.Id), NullIfBlank(version));
            var runScript = new RunModuleRequest(m_CloudProjectId.GetCloudProjectId(), module, function, runArgs);
            var task = m_ApiClient.RunModuleAsync(runScript);

            return await task;
        }

        static string NullIfBlank(string value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
