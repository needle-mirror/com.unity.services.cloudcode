using System;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// Exception for results failures from Cloud Code
    /// </summary>
    public class CloudCodeException : Core.RequestFailedException
    {
        private CloudCodeException(int errorCode, string message)
            : base(errorCode, message) { }

        /// <summary>
        /// Exception for results failures from Cloud Code
        /// </summary>
        /// <param name="errorCode">The service error code for this exception</param>
        /// <param name="message">The error message that explains the reason for the exception, or an empty string</param>
        /// <param name="innerException">The exception that is the cause of the current exception</param>
        public CloudCodeException(int errorCode, string message, Exception innerException)
            : base(errorCode, message, innerException) { }
    }
}
