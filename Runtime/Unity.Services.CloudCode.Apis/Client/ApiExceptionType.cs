namespace Unity.Services.CloudCode.Shared
{
    /// <summary>
    /// API Exception type enumeration.
    /// </summary>
    public enum ApiExceptionType
    {
        /// <summary>
        /// Invalid parameters were provided to the API.
        /// </summary>
        InvalidParameters,
        /// <summary>
        /// A network error occurred during the API request.
        /// </summary>
        Network,
        /// <summary>
        /// An HTTP error occurred during the API request.
        /// </summary>
        Http,
        /// <summary>
        /// An error occurred while deserializing the API response.
        /// </summary>
        Deserialization
    }
}
