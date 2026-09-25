using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    /// <summary>
    /// Submits a built analytic. Each event carries its own name and version in its
    /// <see cref="AnalyticInfoAttribute"/>, so the sender only has to deliver it.
    /// </summary>
    interface IAnalyticsSender
    {
        AnalyticsResult Send(IAnalytic analytic);
    }
}
