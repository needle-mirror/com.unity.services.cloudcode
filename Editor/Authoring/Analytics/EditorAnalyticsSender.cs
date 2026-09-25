using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    class EditorAnalyticsSender : IAnalyticsSender
    {
        public AnalyticsResult Send(IAnalytic analytic)
        {
            return EditorAnalytics.SendAnalytic(analytic);
        }
    }
}
