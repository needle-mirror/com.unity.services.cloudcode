using System.Collections.Generic;
using System.Linq;

namespace Unity.Services.CloudCode.Authoring.Editor.Logging
{
    /// <summary>
    /// The scripting defines behind Cloud Code's verbose logging, as used across the Unity Services
    /// packages. <see cref="Logger.LogVerbose"/> calls are compiled out unless one of them is set,
    /// so verbosity is a compile-time choice rather than a runtime setting.
    /// </summary>
    static class VerboseLogging
    {
        // Owned by other Unity Services settings pages: honoured here, but never written, so that
        // turning Cloud Code's verbose logging off cannot silence every other service.
        internal const string k_ServicesDefine = "ENABLE_UNITY_SERVICES_VERBOSE_LOGGING";
        internal const string k_CloudCodeDefine = "ENABLE_UNITY_CLOUD_CODE_AUTHORING_VERBOSE_LOGGING";

        // Whether this assembly was compiled with verbose logging on. Anything gated on the defines
        // above can read this instead of repeating them in an #if.
#if ENABLE_UNITY_SERVICES_VERBOSE_LOGGING || ENABLE_UNITY_CLOUD_CODE_AUTHORING_VERBOSE_LOGGING
        internal const bool k_Enabled = true;
#else
        internal const bool k_Enabled = false;
#endif

        internal static bool IsEnabled(IEnumerable<string> defines) =>
            defines.Any(d => d == k_ServicesDefine || d == k_CloudCodeDefine);

        // True when verbose logging comes from the services-wide define, which Cloud Code cannot
        // turn off on another package's behalf.
        internal static bool IsEnabledForAllServices(IEnumerable<string> defines) =>
            defines.Any(d => d == k_ServicesDefine);

        internal static List<string> SetEnabled(IEnumerable<string> defines, bool enabled)
        {
            var updated = defines.Where(d => d != k_CloudCodeDefine).ToList();
            if (enabled)
            {
                updated.Add(k_CloudCodeDefine);
            }

            return updated;
        }
    }
}
