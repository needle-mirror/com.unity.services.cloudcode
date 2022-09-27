using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Clients;
using UnityEditor;
using CoreAccessTokens = Unity.Services.Core.Editor.AccessTokens;

namespace Unity.Services.CloudCode.Authoring.Editor.AdminApi.Authentication
{
    class AccessTokens : IAccessTokens
    {
        static readonly CoreAccessTokens k_AccessTokens = new CoreAccessTokens();

        public string GenesisAccessToken => CloudProjectSettings.accessToken;

        public Task<string> GetServicesGatewayTokenAsync()
        {
            return k_AccessTokens.GetServicesGatewayTokenAsync();
        }
    }
}
