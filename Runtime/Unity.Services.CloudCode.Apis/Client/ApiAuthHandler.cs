using System;
using System.Text;

namespace Unity.Services.CloudCode.Shared
{
    /// <summary>
    /// Interface for API authentication types.
    /// </summary>
    public interface IAuthType
    {
        /// <summary>
        /// Gets the authentication header value.
        /// </summary>
        /// <returns>The formatted authentication header value.</returns>
        public string GetHeader();
    }

    /// <summary>
    /// Bearer token authentication implementation.
    /// </summary>
    public class BearerAuth : IAuthType
    {
        private readonly string accessToken;

        /// <summary>
        /// Initializes a new instance of <see cref="BearerAuth"/> with the specified access token.
        /// </summary>
        /// <param name="accessToken">The bearer token for authentication.</param>
        public BearerAuth(string accessToken)
        {
            this.accessToken = accessToken;
        }

        /// <summary>
        /// Gets the bearer authentication header value.
        /// </summary>
        /// <returns>The formatted bearer authentication header.</returns>
        public string GetHeader()
        {
            return $"Bearer {accessToken}";
        }
    }

    /// <summary>
    /// HTTP Basic authentication implementation.
    /// </summary>
    public class BasicAuth : IAuthType
    {
        private readonly string key;
        private readonly string secret;

        /// <summary>
        /// Initializes a new instance of <see cref="BasicAuth"/> with the specified key and secret.
        /// </summary>
        /// <param name="key">The username or key for authentication.</param>
        /// <param name="secret">The password or secret for authentication.</param>
        public BasicAuth(string key, string secret)
        {
            this.key = key;
            this.secret = secret;
        }

        /// <summary>
        /// Gets the HTTP Basic authentication header value.
        /// </summary>
        /// <returns>The formatted Basic authentication header.</returns>
        public string GetHeader()
        {
            return $"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes($"{key}:{secret}"))}";
        }
    }
}
