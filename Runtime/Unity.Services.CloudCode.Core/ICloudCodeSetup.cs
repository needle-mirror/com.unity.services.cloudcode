namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    ///     The Cloud Code setup class, can only be implemented once per module
    /// </summary>
    /// <example>
    ///     The following code will register all a Singleton, Scoped and Transient dependency
    /// <c>
    /// public class ModuleConfig : ICloudCodeSetup
    /// {
    ///     public void Setup(ICloudCodeConfig config)
    ///     {
    ///         config.Dependencies.AddSingleton&lt;ISingletonCounter, CounterDependency>();
    ///         config.Dependencies.AddScoped&lt;IPerRequestCounter, CounterDependency>();
    ///         config.Dependencies.AddTransient&lt;ITransientCounter, CounterDependency>();
    ///     }
    /// }
    /// </c>
    /// </example>
    public interface ICloudCodeSetup
    {
        /// <summary>
        ///     The method that sets up a module on startup
        /// </summary>
        /// <param name="config">The config options</param>
        void Setup(ICloudCodeConfig config);
    }
}
