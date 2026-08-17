#nullable enable
using System;
using System.Linq;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Shared
{
    /// <summary>
    /// HTTP API client for sending HTTP requests to API endpoints.
    /// </summary>
    public class HttpApiClient : IApiClient
    {
        private HttpClient HttpClient { get; } = new();

        /// <summary>
        /// Sends a GET request to the specified path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response.</returns>
        public Task<ApiResponse> GetAsync(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync(path, HttpMethod.Get, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a GET request to the specified path and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The response body type.</typeparam>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response with deserialized data.</returns>
        public Task<ApiResponse<T>> GetAsync<T>(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync<T>(path, HttpMethod.Get, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a POST request to the specified path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response.</returns>
        public Task<ApiResponse> PostAsync(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync(path, HttpMethod.Post, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a POST request to the specified path and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The response body type.</typeparam>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response with deserialized data.</returns>
        public Task<ApiResponse<T>> PostAsync<T>(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync<T>(path, HttpMethod.Post, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a PUT request to the specified path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response.</returns>
        public Task<ApiResponse> PutAsync(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync(path, HttpMethod.Put, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a PUT request to the specified path and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The response body type.</typeparam>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response with deserialized data.</returns>
        public Task<ApiResponse<T>> PutAsync<T>(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync<T>(path, HttpMethod.Put, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a DELETE request to the specified path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response.</returns>
        public Task<ApiResponse> DeleteAsync(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync(path, HttpMethod.Delete, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a DELETE request to the specified path and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The response body type.</typeparam>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response with deserialized data.</returns>
        public Task<ApiResponse<T>> DeleteAsync<T>(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync<T>(path, HttpMethod.Delete, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a HEAD request to the specified path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response.</returns>
        public Task<ApiResponse> HeadAsync(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync(path, HttpMethod.Head, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a HEAD request to the specified path and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The response body type.</typeparam>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response with deserialized data.</returns>
        public Task<ApiResponse<T>> HeadAsync<T>(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync<T>(path, HttpMethod.Head, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends an OPTIONS request to the specified path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response.</returns>
        public Task<ApiResponse> OptionsAsync(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync(path, HttpMethod.Options, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends an OPTIONS request to the specified path and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The response body type.</typeparam>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response with deserialized data.</returns>
        public Task<ApiResponse<T>> OptionsAsync<T>(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync<T>(path, HttpMethod.Options, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a PATCH request to the specified path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response.</returns>
        public Task<ApiResponse> PatchAsync(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync(path, HttpMethod.Patch, options, configuration, cancellationToken);
        }

        /// <summary>
        /// Sends a PATCH request to the specified path and deserializes the response.
        /// </summary>
        /// <typeparam name="T">The response body type.</typeparam>
        /// <param name="path">The request path.</param>
        /// <param name="options">The request options.</param>
        /// <param name="configuration">The API configuration.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The API response with deserialized data.</returns>
        public Task<ApiResponse<T>> PatchAsync<T>(string path, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            return SendAsync<T>(path, HttpMethod.Patch, options, configuration, cancellationToken);
        }

        private async Task<ApiResponse> SendAsync(string path, HttpMethod method, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            var message = BuildMessage(path, method, options, configuration);
            var response = await HttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            return await ToApiResponse(response);
        }

        private async Task<ApiResponse<T>> SendAsync<T>(string path, HttpMethod method, ApiRequestOptions options, IApiConfiguration configuration, CancellationToken cancellationToken)
        {
            var message = BuildMessage(path, method, options, configuration);
            var response = await HttpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            return await ToApiResponse<T>(response);
        }

        private static HttpRequestMessage BuildMessage(string path, HttpMethod method, ApiRequestOptions options, IApiConfiguration configuration)
        {
            var builder = new ApiRequestPathBuilder(configuration.BasePath, path);
            builder.AddPathParameters(options.PathParameters);
            builder.AddQueryParameters(options.QueryParameters);

            var request = new HttpRequestMessage(method, builder.GetFullUri());

            if (configuration.UserAgent != null)
            {
                request.Headers.TryAddWithoutValidation("User-Agent", configuration.UserAgent);
            }

            if (configuration.DefaultHeaders != null)
            {
                foreach (var headerParam in configuration.DefaultHeaders)
                {
                    request.Headers.TryAddWithoutValidation(headerParam.Key, headerParam.Value);
                }
            }

            if (options.HeaderParameters != null)
            {
                foreach (var headerParam in options.HeaderParameters)
                {
                    foreach (var value in headerParam.Value)
                    {
                        request.Headers.TryAddWithoutValidation(headerParam.Key, value);
                    }
                }
            }

            string? contentType = null;

            if (options.HeaderParameters != null && options.HeaderParameters.ContainsKey("Content-Type"))
            {
                var contentTypes = options.HeaderParameters["Content-Type"];
                contentType = contentTypes.FirstOrDefault();
            }

            switch (contentType)
            {
                case "multipart/form-data":
                    throw new Exception("Not supported");
                case "application/x-www-form-urlencoded":
                    request.Content = new FormUrlEncodedContent(options.FormParameters);
                    break;
                default:
                {
                    if (options.Data == null) return request;
                    var settings = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
                    var data = JsonConvert.SerializeObject(options.Data, settings);
                    request.Content = new StringContent(data, new UTF8Encoding(), "application/json");
                    break;
                }
            }

            return request;
        }

        private static async Task<ApiResponse> ToApiResponse(HttpResponseMessage response)
        {
            var rawContent = await response.Content.ReadAsStringAsync();

            var apiResponse = new ApiResponse()
            {
                StatusCode = response.StatusCode,
                ErrorText = response.IsSuccessStatusCode ? "" : response.ReasonPhrase,
                RawContent = rawContent
            };

            if (response.IsSuccessStatusCode) return apiResponse;
            if (response.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
            {
                throw new ApiException(ApiExceptionType.Http, response.ReasonPhrase, apiResponse);
            }

            throw new ApiException(ApiExceptionType.Network, response.ReasonPhrase, apiResponse);
        }

        private static async Task<ApiResponse<T>> ToApiResponse<T>(HttpResponseMessage response)
        {
            var rawContent = await response.Content.ReadAsStringAsync();

            var apiResponse = new ApiResponse<T>()
            {
                StatusCode = response.StatusCode,
                ErrorText = response.IsSuccessStatusCode ? "" : response.ReasonPhrase,
                RawContent = rawContent
            };

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode is >= HttpStatusCode.BadRequest and < HttpStatusCode.InternalServerError)
                {
                    throw new ApiException(ApiExceptionType.Http, response.ReasonPhrase, apiResponse);
                }

                throw new ApiException(ApiExceptionType.Network, response.ReasonPhrase, apiResponse);
            }

            try
            {
                if (!string.IsNullOrEmpty(rawContent))
                {
                    apiResponse.Data = JsonConvert.DeserializeObject<T>(rawContent) !;
                }
            }
            catch (Exception)
            {
                throw new ApiException(ApiExceptionType.Deserialization, $"Deserialization of type '{typeof(T)}' failed.", apiResponse);
            }

            return apiResponse;
        }
    }
}
