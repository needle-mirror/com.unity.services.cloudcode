using System.Runtime.Serialization;
using System.Threading.Tasks;
using Unity.Services.Wire.Internal;
using UnityEngine.Scripting;

namespace Unity.Services.CloudCode.Subscriptions
{
    /// <summary>
    /// Gets a scope channel token by invoking a module function in that scope. The invoker checks the player's
    /// access to the scope before the function runs, so only a player allowed in the scope is minted a token.
    /// </summary>
    class ScopeTokenProvider : IChannelTokenProvider
    {
        readonly ICloudCodeService m_CloudCode;
        readonly string m_Module;
        readonly string m_Function;
        readonly CloudCodeScope m_Scope;

        internal ScopeTokenProvider(ICloudCodeService cloudCode, string module, string function, CloudCodeScope scope)
        {
            m_CloudCode = cloudCode;
            m_Module = module;
            m_Function = function;
            m_Scope = scope;
        }

        public async Task<ChannelToken> GetTokenAsync()
        {
            var reply = await m_CloudCode.CallModuleEndpointAsync<ScopeTokenReply>(m_Module, m_Function, scope: m_Scope);
            if (!string.IsNullOrEmpty(reply?.Error) || string.IsNullOrEmpty(reply?.Token) || string.IsNullOrEmpty(reply.Alias))
            {
                var reason = reply == null ? "empty reply"
                    : string.IsNullOrEmpty(reply.Error) ? "the reply had no token or alias" : reply.Error;
                throw new CloudCodeException(CloudCodeExceptionReason.SubscriptionError, 1337,
                    $"'{m_Module}/{m_Function}' returned no scope token: {reason}", null);
            }

            return new ChannelToken { ChannelName = reply.Alias, Token = reply.Token };
        }

        /// <summary>The JSON shape of <c>IPushClient.GenerateScopeTokenAsync</c>'s reply.</summary>
        [Preserve]
        [DataContract]
        internal class ScopeTokenReply
        {
            [Preserve]
            [DataMember(Name = "token")]
            public string Token { get; set; }

            [Preserve]
            [DataMember(Name = "alias")]
            public string Alias { get; set; }

            [Preserve]
            [DataMember(Name = "error")]
            public string Error { get; set; }
        }
    }
}
