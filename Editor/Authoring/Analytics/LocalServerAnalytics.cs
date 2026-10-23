#if UNITY_6000_3_OR_NEWER
using System;
using Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads;
using UnityEngine.Analytics;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    /// <summary>
    /// Sends the local server lifecycle events.
    /// </summary>
    class LocalServerAnalytics : ILocalServerAnalytics
    {
        const string k_EventNameStarted = "cloudcode_localServerStarted";
        const int k_VersionStarted = 1;

        const string k_EventNameStopped = "cloudcode_localServerStopped";
        const int k_VersionStopped = 1;

        const string k_EventNameStateCleared = "cloudcode_localServerStateCleared";
        const int k_VersionStateCleared = 1;

        readonly ILogger m_Logger;
        readonly IAnalyticsSender m_Sender;

        public LocalServerAnalytics(ILogger logger, IAnalyticsSender sender)
        {
            m_Logger = logger;
            m_Sender = sender;
        }

        public void SendServerStartedEvent(string error = null, string errorData = null)
        {
            var result = m_Sender.Send(
                new ServerStartedAnalytic(ErrorPayloads.For(error, errorData)));
            LogVerbose(k_EventNameStarted, k_VersionStarted, result);
        }

        public void SendServerStoppedEvent(long upTimeMs)
        {
            var result = m_Sender.Send(
                new ServerStoppedAnalytic(new LocalServerStoppedPayload { up_time_ms = upTimeMs }));
            LogVerbose(k_EventNameStopped, k_VersionStopped, result);
        }

        public void SendServerStateClearedEvent()
        {
            var result = m_Sender.Send(new StateClearedAnalytic(new EmptyPayload()));
            LogVerbose(k_EventNameStateCleared, k_VersionStateCleared, result);
        }

        void LogVerbose(string eventName, int version, AnalyticsResult result)
        {
            m_Logger.LogVerbose($"Sent Analytics Event: {eventName}.v{version}. Result: {result}");
        }

        [AnalyticInfo(
            eventName: k_EventNameStarted,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionStarted)]
        class ServerStartedAnalytic : CloudCodeAnalyticEvent<EmptyPayload>
        {
            public ServerStartedAnalytic(EmptyPayload payload) : base(payload) {}
        }

        [AnalyticInfo(
            eventName: k_EventNameStopped,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionStopped)]
        class ServerStoppedAnalytic : CloudCodeAnalyticEvent<LocalServerStoppedPayload>
        {
            public ServerStoppedAnalytic(LocalServerStoppedPayload payload) : base(payload) {}
        }

        [AnalyticInfo(
            eventName: k_EventNameStateCleared,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionStateCleared)]
        class StateClearedAnalytic : CloudCodeAnalyticEvent<EmptyPayload>
        {
            public StateClearedAnalytic(EmptyPayload payload) : base(payload) {}
        }
    }
}
#endif
