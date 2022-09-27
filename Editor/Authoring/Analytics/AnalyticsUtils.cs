using Unity.Services.CloudCode.Authoring.Editor.Shared.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Threading;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    static class AnalyticsUtils
    {
        public static void RegisterEventDefault(string eventName, int version = 1)
        {
            Sync.RunNextUpdateOnMain(() =>
            {
                var result = EditorAnalytics.RegisterEventWithLimit(
                    eventName,
                    AnalyticsConstants.k_MaxEventPerHour,
                    AnalyticsConstants.k_MaxItems,
                    AnalyticsConstants.k_VendorKey,
                    version);

                Logger.LogVerbose($"Analytics: {eventName}.v{version} registered with result {result}");
            });
        }
    }
}
