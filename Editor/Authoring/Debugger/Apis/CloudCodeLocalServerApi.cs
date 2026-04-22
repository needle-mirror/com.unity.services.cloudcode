#if UNITY_SERVICES_CLOUDCODE_EXPERIMENTAL
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Client;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Editor.Shared.Logging;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger.Apis
{
    interface ICloudCodeLocalServerApi
    {
        Task<CloudCodeLocalHealthCheckResponse> HealthCheck(CancellationToken cancellationToken);
        Task<CloudCodeLocalShutdownResponse> Shutdown(CancellationToken cancellationToken);
    }

    class CloudCodeLocalServerApi : ICloudCodeLocalServerApi
    {
        private const int k_NumberOfRetries = 3;
        private const int k_DefaultTimeoutSec = 30;
        private const int k_DefaultRetrySec = 1;

        public Configuration Configuration => m_Configuration;
        private Configuration m_Configuration;
        private HttpClient m_HttpClient;
        readonly ILogger m_Logger;

        internal CloudCodeLocalServerApi(string endpoint, ILogger logger)
        {
            m_Logger = logger;
            m_Configuration = new Configuration(endpoint, k_DefaultTimeoutSec, k_NumberOfRetries, null);
            m_HttpClient = new HttpClient()
            {
                Timeout = TimeSpan.FromSeconds(m_Configuration.RequestTimeout ?? k_DefaultTimeoutSec),
            };
        }

        public async Task<CloudCodeLocalHealthCheckResponse> HealthCheck(CancellationToken cancellationToken)
        {
            return await GetAsync<CloudCodeLocalHealthCheckResponse, CloudCodeLocalHealthCheckRequest>(
                new CloudCodeLocalHealthCheckRequest(), cancellationToken);
        }

        public async Task<CloudCodeLocalShutdownResponse> Shutdown(CancellationToken cancellationToken)
        {
            return await PostAsync<CloudCodeLocalShutdownResponse, CloudCodeLocalShutdownRequest>(
                new CloudCodeLocalShutdownRequest(), cancellationToken);
        }

        private async Task<T1> GetAsync<T1, T2>(T2 request, CancellationToken cancellationToken)
            where T1 : LocalCloudCodeResponseBase
            where T2 : LocalCloudCodeRequestBase
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    var response = await m_HttpClient.GetAsync(request.ConstructUrl(Configuration.BasePath),
                        cancellationToken);
                    if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        m_Logger.LogVerbose(jsonResponse);
                        return JsonConvert.DeserializeObject<T1>(jsonResponse);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    throw new HttpRequestException($"GetAsync failed with HttpStatusCode: {response.StatusCode}");
                }
                // Can also be thrown before status code is received
                catch (HttpRequestException reqEx)
                {
                    if (retryCount < Configuration.NumberOfRetries)
                    {
                        retryCount++;

                        await Task.Delay(TimeSpan.FromSeconds(k_DefaultRetrySec), cancellationToken);
                        continue;
                    }
                    throw new HttpRequestException($"GetAsync failed with HttpRequestException: {reqEx}");
                }
            }
        }

        private async Task<T1> PostAsync<T1, T2>(T2 request, CancellationToken cancellationToken)
            where T1 : LocalCloudCodeResponseBase
            where T2 : LocalCloudCodeRequestBase
        {
            int retryCount = 0;
            while (true)
            {
                try
                {
                    var postData = JsonConvert.SerializeObject(request);
                    StringContent content = new StringContent(postData, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await m_HttpClient.PostAsync(request.ConstructUrl(Configuration.BasePath), content, cancellationToken);

                    // If successful, return the response.
                    if (response.StatusCode is HttpStatusCode.Accepted or HttpStatusCode.OK)
                    {
                        string jsonResponse = await response.Content.ReadAsStringAsync();
                        m_Logger.LogVerbose(jsonResponse);
                        return JsonConvert.DeserializeObject<T1>(jsonResponse);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    throw new HttpRequestException($"GetAsync failed with HttpStatusCode: {response.StatusCode}");
                }
                // Can also be thrown before status code is received
                catch (HttpRequestException reqEx)
                {
                    if (retryCount < Configuration.NumberOfRetries)
                    {
                        retryCount++;

                        await Task.Delay(TimeSpan.FromSeconds(k_DefaultRetrySec), cancellationToken);
                        continue;
                    }
                    throw new HttpRequestException($"GetAsync failed with HttpRequestException: {reqEx}");
                }
            }
        }
    }
}
#endif
