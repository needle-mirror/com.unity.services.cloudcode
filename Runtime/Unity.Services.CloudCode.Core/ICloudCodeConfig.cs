using Microsoft.Extensions.DependencyInjection;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     The Cloud Code configuration interface that allows a module to defined dependencies for use in Dependency Injection
    /// </summary>
    public interface ICloudCodeConfig
    {
        /// <summary>
        ///     The dependencies to include. Uses standard .NET service collection descriptors
        /// </summary>
        IServiceCollection Dependencies { get; }

        /// <summary>
        ///     Gets a configuration value by key.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="defaultValue">The default value to return if the key is not found.</param>
        /// <returns>The configuration value, or the default value if the key is not found.</returns>
        string GetString(string key, string defaultValue = "");

        /// <summary>
        ///     Sets a configuration value by key.
        /// </summary>
        /// <param name="key">The configuration key.</param>
        /// <param name="value">The value to store.</param>
        void SetString(string key, string value);
    }
}
