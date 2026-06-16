using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    class CloudModuleCreationAnalytics
    {
        const string k_EventNameReferenceCreate = "cloudcode_referencecreated";
        const string k_EventNameCloudCodeModuleCreate = "cloudcode_nativemodulecreated";
        const string k_EventNameCloudCodeScriptAdded = "cloudcode_nativescriptadded";
        const int k_VersionCreate = 1;

        public CloudModuleCreationAnalytics()
        {
            EditorAnalytics.RegisterEventWithLimit(k_EventNameReferenceCreate, AnalyticsConstants.k_MaxEventPerHour, AnalyticsConstants.k_MaxItems, AnalyticsConstants.k_VendorKey, k_VersionCreate);
            EditorAnalytics.RegisterEventWithLimit(k_EventNameCloudCodeModuleCreate, AnalyticsConstants.k_MaxEventPerHour, AnalyticsConstants.k_MaxItems, AnalyticsConstants.k_VendorKey, k_VersionCreate);
            EditorAnalytics.RegisterEventWithLimit(k_EventNameCloudCodeScriptAdded, AnalyticsConstants.k_MaxEventPerHour, AnalyticsConstants.k_MaxItems, AnalyticsConstants.k_VendorKey, k_VersionCreate);
        }

        public void SendReferenceCreatedEvent()
        {
            EditorAnalytics.SendEventWithLimit(k_EventNameReferenceCreate, null, k_VersionCreate);
        }

        public void SendCloudCodeModuleCreatedEvent()
        {
            EditorAnalytics.SendEventWithLimit(k_EventNameCloudCodeModuleCreate, null, k_VersionCreate);
        }

        public void SendCloudCodeScriptAddedEvent()
        {
            EditorAnalytics.SendEventWithLimit(k_EventNameCloudCodeScriptAdded, null, k_VersionCreate);
        }
    }
}
