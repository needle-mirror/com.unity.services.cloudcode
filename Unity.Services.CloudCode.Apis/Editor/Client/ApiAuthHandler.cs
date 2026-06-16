using System;
using System.Text;

namespace Unity.Services.CloudCode.Shared
{
    public interface IAuthType
    {
        public string GetHeader();
    }

    public class BearerAuth : IAuthType
    {
        private readonly string accessToken;

        public BearerAuth(string accessToken)
        {
            this.accessToken = accessToken;
        }

        public string GetHeader()
        {
            return $"Bearer {accessToken}";
        }
    }

    public class BasicAuth : IAuthType
    {
        private readonly string key;
        private readonly string secret;

        public BasicAuth(string key, string secret)
        {
            this.key = key;
            this.secret = secret;
        }

        public string GetHeader()
        {
            return $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}"))}";
        }
    }
}
