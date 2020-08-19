using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.GameBackend.CloudCode;
using Unity.GameBackend.CloudCode.Apis.CloudCode;
using Unity.GameBackend.CloudCode.CloudCode;
using Unity.GameBackend.CloudCode.Http;
using Unity.GameBackend.CloudCode.Models;
using UnityEngine;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// Client SDK for Cloud Code.
    /// https://dashboard.unity3d.com/cloud-code
    ///
    /// Streamline your game code in the cloud. Cloud Code shifts your game logic away from your servers, interacting seamlessly with backend services.
    /// </summary>
    public static class CloudCode
    {
        static string s_ProjectId;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void HoldTaskScheduler()
        {
            s_ProjectId = Application.cloudProjectId;
        }
        
        static ICloudCodeApiClient client => UnityServicesCloudCodeService.Instance.CloudCodeApi;

        private static async Task<Response<RunScriptResponse>> GetResponseAsync(string function, object args)
        {
            var dictArgs = new Dictionary<string, object>();

            var fields = args.GetType().GetFields();
            foreach (var field in fields)
            {
                dictArgs[field.Name] = field.GetValue(args);
            }

            var runArgs = new RunScriptArguments(dictArgs);
            var runScript = new RunScriptRequest(s_ProjectId, function, runArgs);
            var task = client.RunScriptAsync(runScript);

            return await task;
        }

        /// <summary>
        /// Calls a Cloud Code function.
        /// </summary>
        /// <param name="function">Cloud Code function to call</param>
        /// <param name="args">Arguments for the cloud code function. Will be serialized to JSON.</param>
        /// <returns>string representation of the return value of the called function. intended to enable custom serializers</returns>
        public static async Task<string> CallEndpointAsync(string function, object args)
        {
            Response<RunScriptResponse> result;
            try
            {
                result = await GetResponseAsync(function, args);
            }
            catch (HttpException<BasicErrorResponse> e)
            {
                if (e.Response.IsNetworkError)
                {
                    throw new CloudCodeException(Core.CommonErrorCodes.TransportError, e.Message, e);
                }
                
                throw new CloudCodeException(e.ActualError.Code, e.Message, e);
            }
            catch (HttpException e)
            {
                if (e.Response.IsNetworkError)
                {
                    throw new CloudCodeException(Core.CommonErrorCodes.TransportError, e.Message, e);
                }
                
                throw new CloudCodeException(Core.CommonErrorCodes.Unknown, e.Message, e);
            }
            catch (Exception e)
            {
                throw new CloudCodeException(Core.CommonErrorCodes.Unknown, e.Message, e);
            }

            object output = result.Result.Output;
            return output.ToString();
        }

        /// <summary>
        /// Calls a Cloud Code function.
        /// </summary>
        /// <param name="function">Cloud Code function to call</param>
        /// <param name="args">Arguments for the cloud code function. Will be serialized to JSON.</param>
        /// <typeparam name="TResult">Serialized from JSON returned by Cloud Code</typeparam>
        /// <returns>serialized output from the called function</returns>
        public static async Task<TResult> CallEndpointAsync<TResult>(string function, object args)
        {
            Response<RunScriptResponse> result;
            try
            {
                result = await GetResponseAsync(function, args);
            }
            catch (HttpException<BasicErrorResponse> e)
            {
                if (e.Response.IsNetworkError)
                {
                    throw new CloudCodeException(Core.CommonErrorCodes.TransportError, e.Message, e);
                }

                throw new CloudCodeException(e.ActualError.Code, e.Message, e);
            }
            catch (HttpException e)
            {
                if (e.Response.IsNetworkError)
                {
                    throw new CloudCodeException(Core.CommonErrorCodes.TransportError, e.Message, e);
                }

                throw new CloudCodeException(Core.CommonErrorCodes.Unknown, e.Message, e);
            }
            catch (Exception e)
            {
                throw new CloudCodeException(Core.CommonErrorCodes.Unknown, e.Message, e);
            }

            object output = result.Result.Output;
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
    }
}
