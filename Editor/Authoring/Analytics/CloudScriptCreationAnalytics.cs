using Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads;
using UnityEditor;
#if UNITY_2023_2_OR_NEWER
using System;
using UnityEngine.Analytics;
#endif

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    class CloudScriptCreationAnalytics
    {
        // Deprecated: superseded by cloudcode_jsScriptCreated. Remove once its dashboards have moved.
        const string k_EventNameCreate = "cloudcode_filecreated";
        const int k_VersionCreate = 1;

        const string k_EventNameJsScriptCreated = "cloudcode_jsScriptCreated";
        // v2: v1 was registered against the wrong schema.
        const int k_VersionJsScriptCreated = 2;

        readonly IAnalyticsSender m_Sender;

        public CloudScriptCreationAnalytics(IAnalyticsSender sender)
        {
            m_Sender = sender;
#if !UNITY_2023_2_OR_NEWER
            EditorAnalytics.RegisterEventWithLimit(k_EventNameCreate, AnalyticsConstants.k_MaxEventPerHour, AnalyticsConstants.k_MaxItems, AnalyticsConstants.k_VendorKey, k_VersionCreate);
#endif
        }

        /// <summary>Deprecated. Sends <c>cloudcode_filecreated</c>.</summary>
        public void SendCreatedEvent()
        {
#if UNITY_2023_2_OR_NEWER
            m_Sender.Send(new CloudScriptCreatedAnalytic());
#else
            EditorAnalytics.SendEventWithLimit(k_EventNameCreate, null, k_VersionCreate);
#endif
        }

        public void SendJsScriptCreatedEvent(string error = null, string errorData = null)
        {
            m_Sender.Send(new JsScriptCreatedAnalytic(ErrorPayloads.For(error, errorData)));
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(
            eventName: k_EventNameCreate,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionCreate)]
        class CloudScriptCreatedAnalytic : IAnalytic
        {
            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = null;
                return true;
            }
        }
#endif

        [AnalyticInfo(
            eventName: k_EventNameJsScriptCreated,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionJsScriptCreated)]
        class JsScriptCreatedAnalytic : CloudCodeAnalyticEvent<EmptyPayload>
        {
            public JsScriptCreatedAnalytic(EmptyPayload payload) : base(payload) {}
        }
    }
}
