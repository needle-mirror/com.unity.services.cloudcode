using System;
using Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads;
using Unity.Services.CloudCode.Editor.Shared.EditorUtils;
using UnityEngine.Analytics;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;

using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    class SolutionGenerationAnalytics : ISolutionGenerationAnalytics
    {
        const string k_EventNameSolutionGenerated = "cloudcode_ccmrSolutionGenerated";
        const int k_VersionSolutionGenerated = 1;

        readonly ILogger m_Logger;
        readonly IAnalyticsSender m_Sender;

        public SolutionGenerationAnalytics(ILogger logger, IAnalyticsSender sender)
        {
            m_Logger = logger;
            m_Sender = sender;
        }

        public void SendSolutionGeneratedEvent(AnalyticsSource source, Exception exception = null)
        {
            Sync.RunNextUpdateOnMain(() =>
            {
                var result = m_Sender.Send(new SolutionGeneratedAnalytic(
                    GenerationPayloads.For(
                        source,
                        AnalyticsErrorCode.FromException(exception),
                        AnalyticsErrorData.FromException(exception))));
                m_Logger.LogVerbose(
                    $"Sent Analytics Event: {k_EventNameSolutionGenerated}.v{k_VersionSolutionGenerated}. Result: {result}");
            });
        }

        [AnalyticInfo(
            eventName: k_EventNameSolutionGenerated,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionSolutionGenerated)]
        class SolutionGeneratedAnalytic : CloudCodeAnalyticEvent<GenerationEventPayload>
        {
            public SolutionGeneratedAnalytic(GenerationEventPayload payload) : base(payload) {}
        }
    }
}
