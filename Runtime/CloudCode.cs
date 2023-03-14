using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.Authentication.Internal;
using Unity.Services.CloudCode.Internal;
using Unity.Services.CloudCode.Internal.Apis.CloudCode;
using Unity.Services.CloudCode.Internal.CloudCode;
using Unity.Services.CloudCode.Internal.Http;
using Unity.Services.CloudCode.Internal.Models;
using Unity.Services.Core;
using Unity.Services.Core.Configuration.Internal;
using UnityEngine;

namespace Unity.Services.CloudCode
{
    internal class CloudCodeInternal : ICloudCodeService
    {
        private readonly ICloudCodeApiClient m_ApiClient;
        private readonly ICloudProjectId m_CloudProjectId;
        private readonly IPlayerId m_PlayerId;
        private readonly IAccessToken m_AccessToken;

        internal CloudCodeInternal(ICloudProjectId cloudProjectId, ICloudCodeApiClient cloudCodeApiClient, IPlayerId playerId, IAccessToken accessToken)
        {
            m_CloudProjectId = cloudProjectId;
            m_ApiClient = cloudCodeApiClient;
            m_PlayerId = playerId;
            m_AccessToken = accessToken;
        }

        public async Task<string> CallEndpointAsync(string function, Dictionary<string, object> args)
        {
            var result = await GetRunScriptResponse(function, args);

            var output = result?.Result?.Output.GetAs<object>();
            return output?.ToString();
        }

        public async Task<TResult> CallEndpointAsync<TResult>(string function, Dictionary<string, object> args)
        {
            var result = await GetRunScriptResponse(function, args);

            return DeserializeOutput<TResult>(result);
        }

        public async Task<string> CallModuleEndpointAsync(string module, string function, Dictionary<string, object> args)
        {
            var result = await GetRunModuleScriptResponse(module, function, args);

            var output = result?.Result?.Output.GetAs<object>();
            return output?.ToString();
        }

        public async Task<TResult> CallModuleEndpointAsync<TResult>(string module, string function, Dictionary<string, object> args)
        {
            var result = await GetRunModuleScriptResponse(module, function, args);

            return DeserializeOutput<TResult>(result);
        }

        static TResult DeserializeOutput<TResult>(Response<RunScriptResponse> result)
        {
            return result.Result.Output.GetAs<TResult>();
        }

        async Task<Response<RunScriptResponse>> GetRunScriptResponse(string function, Dictionary<string, object> args)
        {
            ValidateRequiredDependencies();

            try
            {
                return await GetResponseAsync(function, args);
            }
            catch (HttpException<BasicErrorResponse> e)
            {
                throw BuildException(e.Response.IsNetworkError, e.Response.StatusCode, e.ActualError.Code, e.Message, e);
            }
            catch (HttpException<ValidationErrorResponse> e)
            {
                throw BuildException(e.Response.IsNetworkError, e.Response.StatusCode, e.ActualError.Code, e.Message, e);
            }
            catch (HttpException<InvocationErrorResponse> e)
            {
                throw BuildException(e.Response.IsNetworkError, e.Response.StatusCode, e.ActualError.Code, e.Message, e);
            }
            catch (HttpException e)
            {
                throw BuildException(e.Response.IsNetworkError, e.Response.StatusCode, (int)e.Response.StatusCode, e.Message, e);
            }
            catch (Exception e)
            {
                throw new CloudCodeException(CloudCodeExceptionReason.Unknown, CommonErrorCodes.Unknown, e.Message, e);
            }
        }

        async Task<Response<RunScriptResponse>> GetRunModuleScriptResponse(string module, string function, Dictionary<string, object> args)
        {
            ValidateRequiredDependencies();

            try
            {
                return await GetModuleResponseAsync(module, function, args);
            }
            catch (HttpException<BasicErrorResponse> e)
            {
                throw BuildException(e.Response.IsNetworkError, e.Response.StatusCode, e.ActualError.Code, e.Message, e);
            }
            catch (HttpException<ValidationErrorResponse> e)
            {
                throw BuildException(e.Response.IsNetworkError, e.Response.StatusCode, e.ActualError.Code, e.Message, e);
            }
            catch (HttpException<InvocationErrorResponse> e)
            {
                throw BuildException(e.Response.IsNetworkError, e.Response.StatusCode, e.ActualError.Code, e.Message, e);
            }
            catch (HttpException e)
            {
                throw BuildException(e.Response.IsNetworkError, e.Response.StatusCode, (int)e.Response.StatusCode, e.Message, e);
            }
            catch (Exception e)
            {
                throw new CloudCodeException(CloudCodeExceptionReason.Unknown, CommonErrorCodes.Unknown, e.Message, e);
            }
        }

        private CloudCodeException BuildException(bool isNetworkError, long statusCode, int errorCode, string message, HttpException innerException)
        {
            var code = isNetworkError ? CommonErrorCodes.TransportError : errorCode;
            var reason = isNetworkError ? CloudCodeExceptionReason.NoInternetConnection : GetErrorReason(statusCode);

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
            Debug.LogError(cloudCodeException.Message);
            return cloudCodeException;
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

        CloudCodeExceptionReason GetErrorReason(long statusCode)
        {
            switch (statusCode)
            {
                case 400:
                    return CloudCodeExceptionReason.InvalidArgument;
                case 401:
                    return CloudCodeExceptionReason.Unauthorized;
                case 404:
                    return CloudCodeExceptionReason.NotFound;
                case 422:
                    return CloudCodeExceptionReason.ScriptError;
                case 429:
                    return CloudCodeExceptionReason.TooManyRequests;
                case 500:
                case 503:
                    return CloudCodeExceptionReason.ServiceUnavailable;
                default:
                    return CloudCodeExceptionReason.Unknown;
            }
        }

        async Task<Response<RunScriptResponse>> GetResponseAsync(string function, Dictionary<string, object> args)
        {
            var runArgs = new RunScriptArguments(args ?? new Dictionary<string, object>());
            var runScript = new RunScriptRequest(m_CloudProjectId.GetCloudProjectId(), function, runArgs);
            var task = m_ApiClient.RunScriptAsync(runScript);

            return await task;
        }

        async Task<Response<RunScriptResponse>> GetModuleResponseAsync(string module, string function, Dictionary<string, object> args)
        {
            var runArgs = new RunScriptArguments(args ?? new Dictionary<string, object>());
            var runScript = new RunModuleRequest(m_CloudProjectId.GetCloudProjectId(), module, function, runArgs);
            var task = m_ApiClient.RunModuleAsync(runScript);

            return await task;
        }
    }
}
