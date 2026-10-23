using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    interface ISolutionGenerationAnalytics
    {
        void SendSolutionGeneratedEvent(AnalyticsSource source, Exception exception = null);
    }
}
