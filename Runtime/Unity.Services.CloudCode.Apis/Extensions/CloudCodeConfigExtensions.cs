#nullable enable
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Unity.Services.CloudCode.Api;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;
using Unity.Services.CloudSave.Api;
using Unity.Services.Economy.Api;
using Unity.Services.Friends.Api;
using Unity.Services.Leaderboards.Api;
using Unity.Services.Lobby.Api;
using Unity.Services.Matchmaker.Api;
using Unity.Services.PlayerAuth.Api;
using Unity.Services.PlayerNames.Api;
using Unity.Services.RemoteConfig.Api;

namespace Unity.Services.CloudCode.Apis.Extensions
{
    /// <summary>
    /// Extension methods for ICloudCodeConfig to register service dependencies.
    /// </summary>
    public static partial class CloudCodeConfigExtensions
    {
        /// <summary>
        /// Configuration key for the local secrets file path.
        /// </summary>
        private const string SecretsFilePathKey = "com.unity.services.cloudcode.secret.path";

        /// <summary>
        /// Configuration key for the runtime type.
        /// </summary>
        private const string RuntimeKey = "com.unity.services.cloudcode.runtime";

        /// <summary>
        /// Configuration value for the debugger runtime.
        /// </summary>
        private const string DebuggerRuntime = "debugger";

        /// <summary>
        /// Configuration key for the Cloud Code API base path. When set, the Cloud Code API client (RunModule, RunScript, etc.)
        /// uses this value instead of the generated default, e.g. for per-environment or local gateway URLs.
        /// </summary>
        public const string CloudCodeApiBasePathKey = "CloudCode:Api:BasePath";

        /// <summary>
        /// Registers the appropriate SecretClient implementation.
        /// If a "com.unity.services.cloudcode.secret.path" configuration value is set, uses FileSystemSecretClient for local development.
        /// Otherwise, uses the production SecretClient that fetches secrets from Unity Secret Manager.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddSecretClient(this ICloudCodeConfig config)
        {
            var secretsFilePath = config.GetString(SecretsFilePathKey);

            if (!string.IsNullOrEmpty(secretsFilePath))
            {
                config.Dependencies.TryAddSingleton<ISecretClient>(new FileSystemSecretClient(secretsFilePath));
            }
            else
            {
                config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
                config.Dependencies.TryAddScoped<ISecretClient, SecretClient>();
            }

            return config;
        }

        /// <summary>
        /// Registers CloudSave Data API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddCloudSaveDataClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ICloudSaveDataApi, CloudSaveDataApi>();
            return config;
        }

        /// <summary>
        /// Registers CloudSave Files API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddCloudSaveFilesClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ICloudSaveFilesApi, CloudSaveFilesApi>();
            return config;
        }

        /// <summary>
        /// Registers Economy Configuration API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddEconomyConfigurationClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IEconomyConfigurationApi, EconomyConfigurationApi>();
            return config;
        }

        /// <summary>
        /// Registers Economy Currencies API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddEconomyCurrenciesClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IEconomyCurrenciesApi, EconomyCurrenciesApi>();
            return config;
        }

        /// <summary>
        /// Registers Economy Inventory API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddEconomyInventoryClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IEconomyInventoryApi, EconomyInventoryApi>();
            return config;
        }

        /// <summary>
        /// Registers Economy Purchases API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddEconomyPurchasesClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IEconomyPurchasesApi, EconomyPurchasesApi>();
            return config;
        }

        /// <summary>
        /// Registers Friends Messaging API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddFriendsMessagingClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IFriendsMessagingApi, FriendsMessagingApi>();
            return config;
        }

        /// <summary>
        /// Registers Friends Notifications API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddFriendsNotificationsClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IFriendsNotificationsApi, FriendsNotificationsApi>();
            return config;
        }

        /// <summary>
        /// Registers Friends Presence API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddFriendsPresenceClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IFriendsPresenceApi, FriendsPresenceApi>();
            return config;
        }

        /// <summary>
        /// Registers Friends Relationships API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddFriendsRelationshipsClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IFriendsRelationshipsApi, FriendsRelationshipsApi>();
            return config;
        }

        /// <summary>
        /// Registers Leaderboards API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddLeaderboardsClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ILeaderboardsApi, LeaderboardsApi>();
            return config;
        }

        /// <summary>
        /// Registers Lobby API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddLobbyClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<ILobbyApi, LobbyApi>();
            return config;
        }

        /// <summary>
        /// Registers Cloud Code API client (RunModule, RunScript, etc.) with X-Call-Depth header support.
        /// Resolves the base path from config key or CLOUD_CODE_API_BASE_PATH environment variable.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddCloudCodeClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            var basePath = GetCloudCodeApiBasePath(config);
            if (!string.IsNullOrWhiteSpace(basePath))
            {
                config.Dependencies.AddScoped<ICloudCodeApi>(sp =>
                    new CloudCodeApi(
                        sp.GetRequiredService<IApiClient>(),
                        new ApiConfiguration { BasePath = basePath?.Trim() }));
            }
            else
            {
                config.Dependencies.TryAddScoped<ICloudCodeApi, CloudCodeApi>();
            }
            return config;
        }

        private static string? GetCloudCodeApiBasePath(ICloudCodeConfig config)
        {
            var fromConfig = config.GetString(CloudCodeApiBasePathKey);
            if (!string.IsNullOrWhiteSpace(fromConfig))
                return fromConfig.Trim();

            var explicitBasePath = Environment.GetEnvironmentVariable("CLOUD_CODE_API_BASE_PATH");
            if (!string.IsNullOrWhiteSpace(explicitBasePath))
                return explicitBasePath.Trim();

            return null;
        }

        /// <summary>
        /// Registers Matchmaker API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddMatchmakerClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IMatchmakerTicketsApi, MatchmakerTicketsApi>();
            return config;
        }

        /// <summary>
        /// Registers Player Authentication API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddPlayerAuthClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IPlayerAuthenticationApi, PlayerAuthenticationApi>();
            return config;
        }

        /// <summary>
        /// Registers Player Names API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddPlayerNamesClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IPlayerNamesApi, PlayerNamesApi>();
            return config;
        }

        /// <summary>
        /// Registers Remote Config API client.
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddRemoteConfigClient(this ICloudCodeConfig config)
        {
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();
            config.Dependencies.TryAddScoped<IRemoteConfigSettingsApi, RemoteConfigSettingsApi>();
            return config;
        }

        /// <summary>
        /// Registers IGameApiClient and all its dependencies.
        /// Registers HttpApiClient as a singleton if not already registered.
        /// Registers all API clients (CloudSave, Economy, Friends, Leaderboards, Lobby, Matchmaker, PlayerAuth, PlayerNames, RemoteConfig) as scoped.
        /// Registers ISecretClient if not already registered (using basic SecretClient implementation).
        /// </summary>
        /// <param name="config">The Cloud Code configuration.</param>
        /// <returns>The configuration for chaining.</returns>
        public static ICloudCodeConfig AddGameApiClient(this ICloudCodeConfig config)
        {
            // Register HttpApiClient as singleton if not already registered
            config.Dependencies.TryAddSingleton<IApiClient, HttpApiClient>();

            // Register all API clients
            config.AddCloudCodeClient()
                .AddCloudSaveDataClient()
                .AddCloudSaveFilesClient()
                .AddEconomyConfigurationClient()
                .AddEconomyCurrenciesClient()
                .AddEconomyInventoryClient()
                .AddEconomyPurchasesClient()
                .AddFriendsMessagingClient()
                .AddFriendsNotificationsClient()
                .AddFriendsPresenceClient()
                .AddFriendsRelationshipsClient()
                .AddLeaderboardsClient()
                .AddLobbyClient()
                .AddMatchmakerClient()
                .AddPlayerAuthClient()
                .AddPlayerNamesClient()
                .AddRemoteConfigClient()
                .AddSecretClient();

            // Register GameApiClient as scoped
            config.Dependencies.TryAddScoped<IGameApiClient, GameApiClient>();

            return config;
        }
    }
}
