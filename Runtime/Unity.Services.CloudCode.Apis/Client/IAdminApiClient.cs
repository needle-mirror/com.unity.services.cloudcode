using Unity.Services.CloudSave.Admin.Api;
using Unity.Services.Leaderboards.Admin.Api;
using Unity.Services.Scheduler.Admin.Api;
using Unity.Services.Triggers.Admin.Api;

namespace Unity.Services.CloudCode.Apis
{
    /// <summary>
    /// The Admin Client only uses the services gateway to achieve admin outcomes.
    /// It relies on service account authentication to authorize most api calls.
    /// </summary>
    public interface IAdminApiClient
    {
        /// <summary>
        /// CloudSave Data
        /// </summary>
        public ICloudSaveDataApi CloudSaveData { get; }


        /// <summary>
        /// CloudSave Files
        /// </summary>
        public ICloudSaveFilesApi CloudSaveFiles { get; }

        /// <summary>
        /// Triggers
        /// </summary>
        public ITriggersApi Triggers { get; }

        /// <summary>
        /// Scheduler
        /// </summary>
        public ISchedulerApi Scheduler { get; }

        /// <summary>
        /// Leaderboards
        /// </summary>
        public ILeaderboardsApi Leaderboards { get; }
    }
}
