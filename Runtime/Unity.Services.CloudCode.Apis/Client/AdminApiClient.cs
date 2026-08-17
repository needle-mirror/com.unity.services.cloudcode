using System;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Leaderboards.Admin.Api;
using Unity.Services.CloudSave.Admin.Api;
using Unity.Services.Scheduler.Admin.Api;
using Unity.Services.Triggers.Admin.Api;

namespace Unity.Services.CloudCode.Apis.Admin
{
    /// <summary>
    /// Admin API client providing access to admin APIs for Cloud Code, Cloud Save, Triggers, Scheduler, and Leaderboards.
    /// </summary>
    public class AdminApiClient : IAdminApiClient
    {
        /// <summary>
        /// Gets the Cloud Save Data API.
        /// </summary>
        public ICloudSaveDataApi CloudSaveData { get; }
        /// <summary>
        /// Gets the Cloud Save Files API.
        /// </summary>
        public ICloudSaveFilesApi CloudSaveFiles { get; }
        /// <summary>
        /// Gets the Triggers API.
        /// </summary>
        public ITriggersApi Triggers { get; }
        /// <summary>
        /// Gets the Scheduler API.
        /// </summary>
        public ISchedulerApi Scheduler { get; }
        /// <summary>
        /// Gets the Leaderboards API.
        /// </summary>
        public ILeaderboardsApi Leaderboards { get; }

        static HttpApiClient s_HttpApiClient { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="AdminApiClient"/> class with the specified API instances.
        /// </summary>
        /// <param name="cloudSaveDataApi">The Cloud Save Data API instance.</param>
        /// <param name="cloudSaveFilesApi">The Cloud Save Files API instance.</param>
        /// <param name="triggersApi">The Triggers API instance.</param>
        /// <param name="schedulerApi">The Scheduler API instance.</param>
        /// <param name="leaderboardsApi">The Leaderboards API instance.</param>
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

        /// <summary>
        ///     Creates a new instance of AdminApiClient with all dependencies manually instantiated.
        ///     This method is deprecated. Use <see cref="CloudCodeConfigExtensions.AddAdminApiClient(Unity.Services.CloudCode.Core.ICloudCodeConfig)"/>
        ///     (e.g., <c>config.AddAdminApiClient()</c>) or individual client extension methods in your Setup method instead.
        /// </summary>
        /// <returns>A new <see cref="IAdminApiClient"/> instance.</returns>
        [Obsolete("Use extension methods in ICloudCodeConfig interface instead. Register IAdminApiClient in your Setup method using config.AddAdminApiClient().")]
        public static IAdminApiClient Create()
        {
            return Create(s_HttpApiClient);
        }

        /// <summary>
        ///     Creates a new instance of AdminApiClient with all dependencies manually instantiated using the provided HttpApiClient.
        ///     This method is deprecated. Use <see cref="CloudCodeConfigExtensions.AddAdminApiClient(Unity.Services.CloudCode.Core.ICloudCodeConfig)"/>
        ///     (e.g., <c>config.AddAdminApiClient()</c>) or individual client extension methods in your Setup method instead.
        /// </summary>
        /// <param name="httpApiClient">The HTTP API client to use for requests.</param>
        /// <returns>A new <see cref="IAdminApiClient"/> instance.</returns>
        [Obsolete("Use extension methods in ICloudCodeConfig interface instead. Register IAdminApiClient in your Setup method using config.AddAdminApiClient().")]
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
