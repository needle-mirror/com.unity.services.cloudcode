using System;

namespace Unity.Services.CloudCode.Shared
{
    /// <summary>
    /// API Exception
    /// </summary>
    public class ApiException : Exception
    {
        /// <summary>
        /// Gets the exception type
        /// </summary>
        /// <value>The exception type.</value>
        public ApiExceptionType Type { get; private set; }

        /// <summary>
        /// Gets the response
        /// </summary>
        /// <value>The response.</value>
        public IApiResponse Response { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiException"/> class.
        /// </summary>
        /// <param name="type">The exception type.</param>
        /// <param name="response">The response.</param>
        public ApiException(ApiExceptionType type, string message, IApiResponse response = null) : base(message)
        {
            this.Type = type;
            this.Response = response;
        }
    }
}
