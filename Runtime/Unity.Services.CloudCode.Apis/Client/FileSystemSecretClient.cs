using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Core;

namespace Unity.Services.CloudCode.Apis
{
    /// <summary>
    /// A file system-based implementation of ISecretClient that reads secrets from a JSON file.
    /// The JSON file should contain a dictionary of secret keys to secret values.
    /// Example JSON format:
    /// {
    ///   "DATABASE_PASSWORD": "my-db-password",
    ///   "API_KEY": "my-api-key"
    /// }
    /// </summary>
    internal class FileSystemSecretClient : ISecretClient
    {
        private readonly string _secretsFilePath;
        private Dictionary<string, string> _secrets;
        private readonly object _lock = new();

        /// <summary>
        /// Creates a new FileSystemSecretClient instance.
        /// </summary>
        /// <param name="secretsFilePath">Path to the JSON file containing secrets</param>
        public FileSystemSecretClient(string secretsFilePath)
        {
            if (string.IsNullOrEmpty(secretsFilePath))
            {
                throw new ArgumentNullException(nameof(secretsFilePath), "Secrets file path cannot be null or empty");
            }

            _secretsFilePath = secretsFilePath;
            _secrets = new Dictionary<string, string>();
        }

        /// <summary>
        /// Retrieves a secret by key from the JSON file.
        /// </summary>
        /// <param name="executionContext">The execution context (not used in file system implementation)</param>
        /// <param name="secretKey">The key of the secret to retrieve</param>
        /// <returns>A Secret object containing the secret value</returns>
        /// <exception cref="ArgumentException">Thrown when the secret key is null or empty</exception>
        /// <exception cref="KeyNotFoundException">Thrown when the secret key is not found</exception>
        public async Task<Secret> GetSecret(IExecutionContext executionContext, string secretKey)
        {
            if (string.IsNullOrEmpty(secretKey))
            {
                throw new ArgumentException("Secret key cannot be null or empty", nameof(secretKey));
            }

            await LoadSecretsIfNeeded();

            lock (_lock)
            {
                if (_secrets.TryGetValue(secretKey, out var value))
                {
                    return new Secret(value);
                }
            }

            throw new KeyNotFoundException(
                $"Secret with key '{secretKey}' not found in file '{_secretsFilePath}'"
            );
        }

        /// <summary>
        /// Loads secrets from the JSON file if caching is disabled or if not yet loaded.
        /// </summary>
        private async Task LoadSecretsIfNeeded()
        {
            lock (_lock)
            {
                if (_secrets.Count > 0)
                {
                    return;
                }
            }

            await LoadSecretsFromFile();
        }

        /// <summary>
        /// Loads secrets from the JSON file.
        /// </summary>
        private Task LoadSecretsFromFile()
        {
            lock (_lock)
            {
                if (!File.Exists(_secretsFilePath))
                {
                    throw new FileNotFoundException($"Secrets file not found at path: {_secretsFilePath}");
                }

                try
                {
                    string jsonContent = File.ReadAllText(_secretsFilePath);
                    var secrets = JsonConvert.DeserializeObject<Dictionary<string, string>>(jsonContent);

                    _secrets = secrets ?? throw new InvalidOperationException($"Failed to deserialize secrets from file: {_secretsFilePath}");
                }
                catch (JsonException ex)
                {
                    throw new InvalidOperationException($"Invalid JSON format in secrets file: {_secretsFilePath}", ex);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Error reading secrets file: {_secretsFilePath}", ex);
                }
            }

            return Task.CompletedTask;
        }
    }
}
