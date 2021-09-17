using System;
using System.Text;

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

        string message = null;
        public override string ToString()
        {
            if (message == null)
            {
                var err = InnerException as Unity.GameBackend.CloudCode.Http.HttpException<Unity.GameBackend.CloudCode.Models.BasicErrorResponse>;
                if (err != null)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine(err.Message);
                    foreach (var errorMessage in err.ActualError.Details)
                    {
                        sb.AppendLine(errorMessage.ToString());
                    }

                    message = sb.ToString();
                    return message;
                }
                else
                {
                    return base.ToString();
                }
            }

            return message;
        }

        public override string Message => ToString();
    }
}
