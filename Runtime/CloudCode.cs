using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.Services.Authentication.Internal;
using Unity.Services.CloudCode.Internal;
using Unity.Services.CloudCode.Internal.Apis.CloudCode;
using Unity.Services.CloudCode.Internal.CloudCode;
using Unity.Services.CloudCode.Internal.Http;
using Unity.Services.CloudCode.Internal.Models;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.CloudCode
{
    internal class CloudCodeInternal : ICloudCodeService
    {
        private readonly string m_ProjectId;
        private readonly ICloudCodeApiClient m_ApiClient;
        private readonly IPlayerId m_PlayerId;
        private readonly IAccessToken m_AccessToken;

        internal CloudCodeInternal(string projectId, ICloudCodeApiClient cloudCodeApiClient, IPlayerId playerId, IAccessToken accessToken)
        {
            m_ProjectId = projectId;
            m_ApiClient = cloudCodeApiClient;
            m_PlayerId = playerId;
            m_AccessToken = accessToken;
        }

        public async Task<string> CallEndpointAsync(string function, Dictionary<string, object> args)
        {
            var result = await GetRunScriptResponse(function, args);

            var output = result?.Result?.Output;
            return output?.ToString();
        }

        public async Task<TResult> CallEndpointAsync<TResult>(string function, Dictionary<string, object> args)
        {
            var result = await GetRunScriptResponse(function, args);

            var output = result.Result.Output;
            if (output is int
                || output is long
                || output is short
                || output is float
                || output is double
                || output is string
                || output is char
                || output is bool)
            {
                return (TResult)output;
            }

            var jobj = (JObject)result.Result.Output;
            return jobj.ToObject<TResult>();
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
            if (String.IsNullOrEmpty(m_ProjectId))
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
            var runArgs = new RunScriptArguments(args);
            var runScript = new RunScriptRequest(m_ProjectId, function, runArgs);
            var task = m_ApiClient.RunScriptAsync(runScript);

            return await task;
        }
    }
}
