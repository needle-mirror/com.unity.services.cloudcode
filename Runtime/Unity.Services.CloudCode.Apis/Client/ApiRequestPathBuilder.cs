using System;
using System.Collections.Generic;

namespace Unity.Services.CloudCode.Shared
{
    /// <summary>
    /// A URI builder for constructing request paths with parameters.
    /// </summary>
    public class ApiRequestPathBuilder
    {
        private string _baseUrl;
        private string _path;
        private string _query = "?";

        /// <summary>
        /// Initializes a new instance of <see cref="ApiRequestPathBuilder"/>.
        /// </summary>
        /// <param name="baseUrl">The base URL for the request.</param>
        /// <param name="path">The path component of the URL.</param>
        public ApiRequestPathBuilder(string baseUrl, string path)
        {
            _baseUrl = baseUrl;
            _path = path;
        }

        /// <summary>
        /// Adds path parameters to the URL, replacing placeholders in the path.
        /// </summary>
        /// <param name="parameters">A dictionary of path parameter names and values.</param>
        public void AddPathParameters(Dictionary<string, string> parameters)
        {
            foreach (var parameter in parameters)
            {
                _path = _path.Replace("{" + parameter.Key + "}", Uri.EscapeDataString(parameter.Value));
            }
        }

        /// <summary>
        /// Adds query parameters to the URL.
        /// </summary>
        /// <param name="parameters">A multimap of query parameter names and values.</param>
        public void AddQueryParameters(Multimap<string, string> parameters)
        {
            foreach (var parameter in parameters)
            {
                foreach (var value in parameter.Value)
                {
                    _query = _query + parameter.Key + "=" + Uri.EscapeDataString(value) + "&";
                }
            }
        }

        /// <summary>
        /// Gets the complete URI with all parameters applied.
        /// </summary>
        /// <returns>The complete URI string.</returns>
        public string GetFullUri()
        {
            return _baseUrl + _path + _query.Substring(0, _query.Length - 1);
        }
    }
}
