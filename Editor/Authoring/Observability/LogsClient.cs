using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Client.Http;
using Unity.Services.CloudCode.Authoring.Editor.AdminApi;
using Unity.Services.CloudCode.Editor.Shared.Clients;
using Unity.Services.Core.Editor;
using Unity.Services.Core.Editor.Environments;
using UnityEngine.Networking;

#if DEPLOYMENT_API_AVAILABLE_V1_1
using IProjectID = Unity.Services.DeploymentApi.Editor.IProjectIdentifierProvider;
#else
using IProjectID = Unity.Services.CloudCode.Authoring.Editor.Deployment.IProjectIdentifierProvider;
#endif

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    class LogsClient : ILogsClient
    {
        const string k_ProductionHost = "https://services.unity.com";
        const string k_StagingHost = "https://staging.services.unity.com";
        const int k_RequestTimeoutSeconds = 30;

        const string k_NoProjectMessage =
            "No Unity project is linked. Link a project in Project Settings > Services to query Cloud Code logs.";
        const string k_NoEnvironmentMessage =
            "No environment is selected. Select an environment in Project Settings > Services to query Cloud Code logs.";

        readonly IAccessTokens m_TokenProvider;
        readonly IProjectID m_ProjectIdProvider;
        readonly IEnvironmentsApi m_EnvironmentsApi;
        readonly IHttpClient m_HttpClient;

        public LogsClient(
            IAccessTokens tokenProvider,
            IProjectID projectIdProvider,
            IEnvironmentsApi environmentsApi,
            IHttpClient httpClient)
        {
            m_TokenProvider = tokenProvider;
            m_ProjectIdProvider = projectIdProvider;
            m_EnvironmentsApi = environmentsApi;
            m_HttpClient = httpClient;
        }

        public async Task<LogsResponse> GetLogsAsync(LogsQuery query, CancellationToken cancellationToken = default)
        {
            var target = GetTarget();

            if (!target.IsReady)
            {
                throw new LogsUnavailableException(target.UnavailableReason);
            }

            var token = await m_TokenProvider.GetServicesGatewayTokenAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var headers = new Dictionary<string, string>(
                new AdminApiHeaders<LogsClient>(token).ToDictionary());
            headers["Accept"] = "application/json";

            var response = await m_HttpClient.MakeRequestAsync(
                "GET",
                BuildUrl(target, query),
                null,
                headers,
                k_RequestTimeoutSeconds,
                null,
                null);

            cancellationToken.ThrowIfCancellationRequested();

            if (response.IsHttpError || response.IsNetworkError)
            {
                throw new LogsRequestException(response, TryReadProblemJson(response));
            }

            return ResponseHandler.TryDeserializeResponse<LogsResponse>(response) ?? new LogsResponse();
        }

        internal LogsTarget GetTarget()
        {
            var projectId = m_ProjectIdProvider.ProjectId;
            var environmentId = m_EnvironmentsApi.ActiveEnvironmentId;

            if (string.IsNullOrEmpty(projectId))
            {
                return new LogsTarget(null, null, k_NoProjectMessage);
            }

            if (environmentId == null)
            {
                return new LogsTarget(projectId, null, k_NoEnvironmentMessage);
            }

            return new LogsTarget(projectId, environmentId.Value.ToString(), null);
        }

        internal static string BuildUrl(LogsTarget target, LogsQuery query)
        {
            var host = CloudEnvironmentConfigProvider.IsStaging() ? k_StagingHost : k_ProductionHost;
            var path =
                $"/api/observability/v1/projects/{target.ProjectId}/environments/{target.EnvironmentId}/logs";

            var queryParams = new List<string>();
            AddParam(queryParams, "offset", query.NormalizedOffset.ToString());
            AddParam(queryParams, "limit", query.NormalizedLimit.ToString());
            AddParam(queryParams, "from", query.From);
            AddParam(queryParams, "to", query.To);
            AddParam(queryParams, "query", query.BuildFilterExpression());

            return $"{host}{path}?{string.Join("&", queryParams)}";
        }

        static void AddParam(ICollection<string> queryParams, string key, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            queryParams.Add($"{UnityWebRequest.EscapeURL(key)}={UnityWebRequest.EscapeURL(value)}");
        }

        static ProblemJson TryReadProblemJson(HttpClientResponse response)
        {
            try
            {
                return ResponseHandler.TryDeserializeResponse<ProblemJson>(response);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
