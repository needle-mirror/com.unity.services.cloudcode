using Unity.Services.CloudCode.Shared;
using Unity.Services.Leaderboards.Admin.Api;
using Unity.Services.CloudSave.Admin.Api;
using Unity.Services.Scheduler.Admin.Api;
using Unity.Services.Triggers.Admin.Api;

namespace Unity.Services.CloudCode.Apis.Admin
{
    public class AdminApiClient : IAdminApiClient
    {
        public ICloudSaveDataApi CloudSaveData { get; }
        public ICloudSaveFilesApi CloudSaveFiles { get; }
        public ITriggersApi Triggers { get; }
        public ISchedulerApi Scheduler { get; }
        public ILeaderboardsApi Leaderboards { get; }

        static HttpApiClient s_HttpApiClient { get; } = new();

        public AdminApiClient(
            ICloudSaveDataApi cloudSaveDataApi,
            ICloudSaveFilesApi cloudSaveFilesApi,
            ITriggersApi triggersApi,
            ISchedulerApi schedulerApi,
            ILeaderboardsApi leaderboardsApi
        )
        {
            CloudSaveData = cloudSaveDataApi;
            CloudSaveFiles = cloudSaveFilesApi;
            Triggers = triggersApi;
            Scheduler = schedulerApi;
            Leaderboards = leaderboardsApi;
        }

        public static IAdminApiClient Create()
        {
            return Create(s_HttpApiClient);
        }

        public static IAdminApiClient Create(HttpApiClient httpApiClient)
        {
            return new AdminApiClient(
                new CloudSaveDataApi(httpApiClient),
                new CloudSaveFilesApi(httpApiClient),
                new TriggersApi(httpApiClient),
                new SchedulerApi(httpApiClient),
                new LeaderboardsApi(httpApiClient)
            );
        }
    }
}