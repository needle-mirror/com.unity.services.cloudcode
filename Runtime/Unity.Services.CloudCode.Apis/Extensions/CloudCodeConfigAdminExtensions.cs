#nullable enable
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unity.Services.CloudCode.Apis.Admin;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Admin.Api;
using Unity.Services.Leaderboards.Admin.Api;
using Unity.Services.Scheduler.Admin.Api;
using Unity.Services.Triggers.Admin.Api;

namespace Unity.Services.CloudCode.Apis.Extensions
{
    // Admin API registrations live in their own file: as sharing the other file's usings
    // would silently register the game-side types instead.
    public static partial class CloudCodeConfigExtensions
    {
        /// <summary>
        /// Registers CloudSave Data Admin API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddCloudSaveDataAdminClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ICloudSaveDataApi, CloudSaveDataApi>();
            return config;
        }

        /// <summary>
        /// Registers CloudSave Files Admin API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddCloudSaveFilesAdminClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ICloudSaveFilesApi, CloudSaveFilesApi>();
            return config;
        }

        /// <summary>
        /// Registers Triggers Admin API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddTriggersAdminClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ITriggersApi, TriggersApi>();
            return config;
        }

        /// <summary>
        /// Registers Scheduler Admin API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddSchedulerAdminClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ISchedulerApi, SchedulerApi>();
            return config;
        }

        /// <summary>
        /// Registers Leaderboards Admin API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddLeaderboardsAdminClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ILeaderboardsApi, LeaderboardsApi>();
            return config;
        }

        /// <summary>
        /// Registers IAdminApiClient and all its dependencies.
        /// Registers HttpApiClient as a singleton if not already registered.
        /// Registers all admin API clients (CloudSave Data, CloudSave Files, Triggers, Scheduler, Leaderboards) as scoped.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddAdminApiClient(this ICloudCodeConfig config)
        {
            // Register HttpApiClient as singleton if not already registered
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();

            // Register all admin API clients
            config.AddCloudSaveDataAdminClient()
                .AddCloudSaveFilesAdminClient()
                .AddTriggersAdminClient()
                .AddSchedulerAdminClient()
                .AddLeaderboardsAdminClient();

            // Register AdminApiClient as scoped
            config.Dependencies.TryAddScoped<IAdminApiClient, AdminApiClient>();

            return config;
        }
    }
}
