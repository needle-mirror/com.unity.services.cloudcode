// WARNING: Auto generated code by Starbuck2. Modifications will be lost!
using System;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Chrono;

namespace Unity.Services.CloudCode.Authoring.Editor.Shared.Clients
{
    class GatewayTokenProvider : IGatewayTokenProvider
    {
        static readonly TimeSpan k_RefreshGracePeriod = TimeSpan.FromMinutes(30);

        readonly IAccessTokens m_AccessTokens;
        readonly ICurrentTime m_CurrentTime;

        string m_GatewayToken;
        DateTime m_NextRefreshTime;
        string m_LastKnownAccessToken;

        public GatewayTokenProvider(IAccessTokens accessTokens, ICurrentTime currentTime)
        {
            m_AccessTokens = accessTokens;
            m_CurrentTime = currentTime;
        }

        public async Task<string> FetchGatewayToken()
        {
            if (m_AccessTokens.GenesisAccessToken != m_LastKnownAccessToken || m_CurrentTime.Now >= m_NextRefreshTime)
            {
                if (!string.IsNullOrEmpty(m_AccessTokens.GenesisAccessToken))
                {
                    m_GatewayToken = await m_AccessTokens.GetServicesGatewayTokenAsync();
                    m_NextRefreshTime = GetNextRefreshTime(m_GatewayToken);
                }
                else
                {
                    m_GatewayToken = null;
                    m_NextRefreshTime = default;
                }
                m_LastKnownAccessToken = m_AccessTokens.GenesisAccessToken;
            }

            return m_GatewayToken;
        }

        static DateTime GetNextRefreshTime(string gatewayToken)
        {
            var jwt = JsonWebToken.Decode(gatewayToken);
            return jwt.Expiration - k_RefreshGracePeriod;
        }
    }
}
