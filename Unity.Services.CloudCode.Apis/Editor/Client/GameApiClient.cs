using System;
using Unity.Services.CloudCode.Api;
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
    public class GameApiClient : IGameApiClient
    {
        public ICloudCodeApi CloudCode { get; }
        public ICloudSaveDataApi CloudSaveData { get; }
        public ICloudSaveFilesApi CloudSaveFiles { get; }
        public IEconomyConfigurationApi EconomyConfiguration { get; }
        public IEconomyCurrenciesApi EconomyCurrencies { get; }
        public IEconomyInventoryApi EconomyInventory { get; }
        public IEconomyPurchasesApi EconomyPurchases { get; }
        public IFriendsMessagingApi FriendsMessagingApi { get; }
        public IFriendsNotificationsApi FriendsNotificationsApi { get; }
        public IFriendsPresenceApi FriendsPresenceApi { get; }
        public IFriendsRelationshipsApi FriendsRelationshipsApi { get; }
        public ILeaderboardsApi Leaderboards { get; }
        public ILobbyApi Lobby { get; }
        public IMatchmakerTicketsApi MatchmakerTickets { get; }
        public IPlayerAuthenticationApi PlayerAuth { get; }
        public IPlayerNamesApi PlayerNamesApi { get; }
        public IRemoteConfigSettingsApi RemoteConfigSettings { get; }
        public ISecretClient SecretManager { get; }

        static HttpApiClient s_HttpApiClient { get; } = new();

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
        ///     This method is deprecated. Use <see cref="Unity.Services.CloudCode.Apis.Extensions.CloudCodeConfigExtensions.AddGameApiClient(ICloudCodeConfig)"/>
        ///     (e.g., <c>config.AddGameApiClient()</c>) or individual client extension methods in your Setup method instead.
        /// </summary>
        [Obsolete("Use extension methods in ICloudCodeConfig interface instead. Register IGameApiClient in your Setup method using config.AddGameApiClient().")]
        public static IGameApiClient Create()
        {
            return Create(s_HttpApiClient);
        }

        /// <summary>
        ///     Creates a new instance of GameApiClient with all dependencies manually instantiated using the provided HttpApiClient.
        ///     This method is deprecated. Use <see cref="Unity.Services.CloudCode.Apis.Extensions.CloudCodeConfigExtensions.AddGameApiClient(ICloudCodeConfig)"/>
        ///     (e.g., <c>config.AddGameApiClient()</c>) or individual client extension methods in your Setup method instead.
        /// </summary>
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
