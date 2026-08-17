using System;
using Unity.Services.CloudCode.Api;
using Unity.Services.CloudCode.Apis.Extensions;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Api;
using Unity.Services.Economy.Api;
using Unity.Services.Lobby.Api;
using Unity.Services.Matchmaker.Api;
using Unity.Services.PlayerAuth.Api;
using Unity.Services.RemoteConfig.Api;
using Unity.Services.CloudCode.Shared;
using Unity.Services.Friends.Api;
using Unity.Services.Leaderboards.Api;
using Unity.Services.PlayerNames.Api;

namespace Unity.Services.CloudCode.Apis
{
    /// <summary>
    /// Game API client providing access to Cloud Code and all related game service APIs.
    /// </summary>
    public class GameApiClient : IGameApiClient
    {
        /// <summary>
        /// Gets the Cloud Code API.
        /// </summary>
        public ICloudCodeApi CloudCode { get; }
        /// <summary>
        /// Gets the Cloud Save Data API.
        /// </summary>
        public ICloudSaveDataApi CloudSaveData { get; }
        /// <summary>
        /// Gets the Cloud Save Files API.
        /// </summary>
        public ICloudSaveFilesApi CloudSaveFiles { get; }
        /// <summary>
        /// Gets the Economy Configuration API.
        /// </summary>
        public IEconomyConfigurationApi EconomyConfiguration { get; }
        /// <summary>
        /// Gets the Economy Currencies API.
        /// </summary>
        public IEconomyCurrenciesApi EconomyCurrencies { get; }
        /// <summary>
        /// Gets the Economy Inventory API.
        /// </summary>
        public IEconomyInventoryApi EconomyInventory { get; }
        /// <summary>
        /// Gets the Economy Purchases API.
        /// </summary>
        public IEconomyPurchasesApi EconomyPurchases { get; }
        /// <summary>
        /// Gets the Friends Messaging API.
        /// </summary>
        public IFriendsMessagingApi FriendsMessagingApi { get; }
        /// <summary>
        /// Gets the Friends Notifications API.
        /// </summary>
        public IFriendsNotificationsApi FriendsNotificationsApi { get; }
        /// <summary>
        /// Gets the Friends Presence API.
        /// </summary>
        public IFriendsPresenceApi FriendsPresenceApi { get; }
        /// <summary>
        /// Gets the Friends Relationships API.
        /// </summary>
        public IFriendsRelationshipsApi FriendsRelationshipsApi { get; }
        /// <summary>
        /// Gets the Leaderboards API.
        /// </summary>
        public ILeaderboardsApi Leaderboards { get; }
        /// <summary>
        /// Gets the Lobby API.
        /// </summary>
        public ILobbyApi Lobby { get; }
        /// <summary>
        /// Gets the Matchmaker Tickets API.
        /// </summary>
        public IMatchmakerTicketsApi MatchmakerTickets { get; }
        /// <summary>
        /// Gets the Player Authentication API.
        /// </summary>
        public IPlayerAuthenticationApi PlayerAuth { get; }
        /// <summary>
        /// Gets the Player Names API.
        /// </summary>
        public IPlayerNamesApi PlayerNamesApi { get; }
        /// <summary>
        /// Gets the Remote Config Settings API.
        /// </summary>
        public IRemoteConfigSettingsApi RemoteConfigSettings { get; }
        /// <summary>
        /// Gets the Secret Manager client.
        /// </summary>
        public ISecretClient SecretManager { get; }

        static HttpApiClient s_HttpApiClient { get; } = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="GameApiClient"/> class with the specified API instances.
        /// </summary>
        /// <param name="cloudCodeApi">The Cloud Code API instance.</param>
        /// <param name="cloudSaveDataApi">The Cloud Save Data API instance.</param>
        /// <param name="cloudSaveFilesApi">The Cloud Save Files API instance.</param>
        /// <param name="economyConfigurationApi">The Economy Configuration API instance.</param>
        /// <param name="economyCurrenciesApi">The Economy Currencies API instance.</param>
        /// <param name="economyInventoryApi">The Economy Inventory API instance.</param>
        /// <param name="economyPurchasesApi">The Economy Purchases API instance.</param>
        /// <param name="friendsMessagingApi">The Friends Messaging API instance.</param>
        /// <param name="friendsNotificationsApi">The Friends Notifications API instance.</param>
        /// <param name="friendsPresenceApi">The Friends Presence API instance.</param>
        /// <param name="friendsRelationshipsApi">The Friends Relationships API instance.</param>
        /// <param name="leaderboardsApi">The Leaderboards API instance.</param>
        /// <param name="lobbyApi">The Lobby API instance.</param>
        /// <param name="matchmakerTicketsApi">The Matchmaker Tickets API instance.</param>
        /// <param name="playerAuthApi">The Player Authentication API instance.</param>
        /// <param name="playerNamesApi">The Player Names API instance.</param>
        /// <param name="remoteConfigSettingsApi">The Remote Config Settings API instance.</param>
        /// <param name="secretClient">The Secret Client instance.</param>
        public GameApiClient(
            ICloudCodeApi cloudCodeApi,
            ICloudSaveDataApi cloudSaveDataApi,
            ICloudSaveFilesApi cloudSaveFilesApi,
            IEconomyConfigurationApi economyConfigurationApi,
            IEconomyCurrenciesApi economyCurrenciesApi,
            IEconomyInventoryApi economyInventoryApi,
            IEconomyPurchasesApi economyPurchasesApi,
            IFriendsMessagingApi friendsMessagingApi,
            IFriendsNotificationsApi friendsNotificationsApi,
            IFriendsPresenceApi friendsPresenceApi,
            IFriendsRelationshipsApi friendsRelationshipsApi,
            ILeaderboardsApi leaderboardsApi,
            ILobbyApi lobbyApi,
            IMatchmakerTicketsApi matchmakerTicketsApi,
            IPlayerAuthenticationApi playerAuthApi,
            IPlayerNamesApi playerNamesApi,
            IRemoteConfigSettingsApi remoteConfigSettingsApi,
            ISecretClient secretClient)
        {
            CloudCode = cloudCodeApi;
            CloudSaveData = cloudSaveDataApi;
            CloudSaveFiles = cloudSaveFilesApi;
            EconomyConfiguration = economyConfigurationApi;
            EconomyCurrencies = economyCurrenciesApi;
            EconomyInventory = economyInventoryApi;
            EconomyPurchases = economyPurchasesApi;
            FriendsMessagingApi = friendsMessagingApi;
            FriendsNotificationsApi = friendsNotificationsApi;
            FriendsPresenceApi = friendsPresenceApi;
            FriendsRelationshipsApi = friendsRelationshipsApi;
            Leaderboards = leaderboardsApi;
            Lobby = lobbyApi;
            MatchmakerTickets = matchmakerTicketsApi;
            PlayerAuth = playerAuthApi;
            PlayerNamesApi = playerNamesApi;
            RemoteConfigSettings = remoteConfigSettingsApi;
            SecretManager = secretClient;
        }

        /// <summary>
        ///     Creates a new instance of GameApiClient with all dependencies manually instantiated.
        ///     This method is deprecated. Use <see cref="CloudCodeConfigExtensions.AddGameApiClient(ICloudCodeConfig)"/>
        ///     (e.g., <c>config.AddGameApiClient()</c>) or individual client extension methods in your Setup method instead.
        /// </summary>
        /// <returns>A new <see cref="IGameApiClient"/> instance.</returns>
        [Obsolete("Use extension methods in ICloudCodeConfig interface instead. Register IGameApiClient in your Setup method using config.AddGameApiClient().")]
        public static IGameApiClient Create()
        {
            return Create(s_HttpApiClient);
        }

        /// <summary>
        ///     Creates a new instance of GameApiClient with all dependencies manually instantiated using the provided HttpApiClient.
        ///     This method is deprecated. Use <see cref="CloudCodeConfigExtensions.AddGameApiClient(ICloudCodeConfig)"/>
        ///     (e.g., <c>config.AddGameApiClient()</c>) or individual client extension methods in your Setup method instead.
        /// </summary>
        /// <param name="httpApiClient">The HTTP API client to use for requests.</param>
        /// <returns>A new <see cref="IGameApiClient"/> instance.</returns>
        [Obsolete("Use extension methods in ICloudCodeConfig interface instead. Register IGameApiClient in your Setup method using config.AddGameApiClient().")]
        public static IGameApiClient Create(HttpApiClient httpApiClient)
        {
            return new GameApiClient(
                new CloudCodeApi(httpApiClient),
                new CloudSaveDataApi(httpApiClient),
                new CloudSaveFilesApi(httpApiClient),
                new EconomyConfigurationApi(httpApiClient),
                new EconomyCurrenciesApi(httpApiClient),
                new EconomyInventoryApi(httpApiClient),
                new EconomyPurchasesApi(httpApiClient),
                new FriendsMessagingApi(httpApiClient),
                new FriendsNotificationsApi(httpApiClient),
                new FriendsPresenceApi(httpApiClient),
                new FriendsRelationshipsApi(httpApiClient),
                new LeaderboardsApi(httpApiClient),
                new LobbyApi(httpApiClient),
                new MatchmakerTicketsApi(httpApiClient),
                new PlayerAuthenticationApi(httpApiClient),
                new PlayerNamesApi(httpApiClient),
                new RemoteConfigSettingsApi(httpApiClient),
                new SecretClient(httpApiClient));
        }
    }
}
