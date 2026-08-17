using Unity.Services.CloudCode.Api;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudSave.Api;
using Unity.Services.Economy.Api;
using Unity.Services.Friends.Api;
using Unity.Services.Leaderboards.Api;
using Unity.Services.Lobby.Api;
using Unity.Services.Matchmaker.Api;
using Unity.Services.PlayerAuth.Api;
using Unity.Services.PlayerNames.Api;
using Unity.Services.RemoteConfig.Api;

namespace Unity.Services.CloudCode.Apis
{
    /// <summary>
    /// The Game Client only uses the game gateway to achieve game-scale outcomes.
    /// It relies on player authentication to authorize most api calls.
    /// </summary>
    public interface IGameApiClient
    {
        /// <summary>
        /// Cloud Code API (RunModule, RunScript, etc.) with X-Call-Depth header support.
        /// </summary>
        public ICloudCodeApi CloudCode { get; }

        /// <summary>
        /// CloudSave Data
        /// </summary>
        public ICloudSaveDataApi CloudSaveData { get; }

        /// <summary>
        /// CloudSave Files
        /// </summary>
        public ICloudSaveFilesApi CloudSaveFiles { get; }

        /// <summary>
        /// EconomyConfiguration
        /// </summary>
        public IEconomyConfigurationApi EconomyConfiguration { get; }

        /// <summary>
        /// EconomyCurrencies
        /// </summary>
        public IEconomyCurrenciesApi EconomyCurrencies { get; }

        /// <summary>
        /// EconomyInventory
        /// </summary>
        public IEconomyInventoryApi EconomyInventory { get; }

        /// <summary>
        /// EconomyPurchases
        /// </summary>
        public IEconomyPurchasesApi EconomyPurchases { get; }

        /// <summary>
        /// FriendsMessaging
        /// </summary>
        public IFriendsMessagingApi FriendsMessagingApi { get; }

        /// <summary>
        /// FriendsNotifications
        /// </summary>
        public IFriendsNotificationsApi FriendsNotificationsApi { get; }

        /// <summary>
        /// FriendsPresence
        /// </summary>
        public IFriendsPresenceApi FriendsPresenceApi { get; }

        /// <summary>
        /// FriendsRelationships
        /// </summary>
        public IFriendsRelationshipsApi FriendsRelationshipsApi { get; }

        /// <summary>
        /// Leaderboards
        /// </summary>
        public ILeaderboardsApi Leaderboards { get; }

        /// <summary>
        /// Lobby
        /// </summary>
        public ILobbyApi Lobby { get; }

        /// <summary>
        /// MatchmakerTickets
        /// </summary>
        public IMatchmakerTicketsApi MatchmakerTickets { get; }

        /// <summary>
        /// PlayerAuth
        /// </summary>
        public IPlayerAuthenticationApi PlayerAuth { get; }

        /// <summary>
        /// PlayerNames
        /// </summary>
        public IPlayerNamesApi PlayerNamesApi { get; }
        /// <summary>
        /// RemoteConfig
        /// </summary>
        public IRemoteConfigSettingsApi RemoteConfigSettings { get; }
        /// <summary>
        /// SecretManager
        /// </summary>
        public ISecretClient SecretManager { get; }
    }
}
