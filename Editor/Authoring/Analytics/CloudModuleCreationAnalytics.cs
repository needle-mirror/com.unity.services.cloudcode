using Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    class CloudModuleCreationAnalytics
    {
        const string k_EventNameReferenceCreate = "cloudcode_ccmrCreated";
        const string k_EventNameCloudCodeModuleCreate = "cloudcode_ccmuModuleCreated";
        const string k_EventNameCloudCodeScriptAdded = "cloudcode_ccmuModuleScriptAdded";
        const int k_VersionReferenceCreate = 1;
        const int k_VersionCloudCodeModuleCreate = 1;
        const int k_VersionCloudCodeScriptAdded = 1;

        readonly IAnalyticsSender m_Sender;

        public CloudModuleCreationAnalytics(IAnalyticsSender sender)
        {
            m_Sender = sender;
        }

        public void SendReferenceCreatedEvent(string error = null, string errorData = null)
        {
            m_Sender.Send(new ReferenceCreatedAnalytic(ErrorPayloads.For(error, errorData)));
        }

        public void SendCloudCodeModuleCreatedEvent(ModuleType moduleType, string error = null, string errorData = null)
        {
            m_Sender.Send(new CloudCodeModuleCreatedAnalytic(CreationPayload(moduleType, error, errorData)));
        }

        public void SendCloudCodeScriptAddedEvent(ModuleType moduleType, string error = null, string errorData = null)
        {
            m_Sender.Send(new CloudCodeScriptAddedAnalytic(CreationPayload(moduleType, error, errorData)));
        }

        // Success builds the base type, so the error key is absent rather than empty.
        internal static ModuleCreationPayload CreationPayload(ModuleType moduleType, string error, string errorData = null)
        {
            var payload = error == null
                ? new ModuleCreationPayload()
                : new ModuleCreationErrorPayload { error = error, error_data = errorData };

            payload.module_type = moduleType.ToAnalyticsValue();
            return payload;
        }

        [AnalyticInfo(
            eventName: k_EventNameReferenceCreate,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionReferenceCreate)]
        class ReferenceCreatedAnalytic : CloudCodeAnalyticEvent<EmptyPayload>
        {
            public ReferenceCreatedAnalytic(EmptyPayload payload) : base(payload) {}
        }

        [AnalyticInfo(
            eventName: k_EventNameCloudCodeModuleCreate,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionCloudCodeModuleCreate)]
        class CloudCodeModuleCreatedAnalytic : CloudCodeAnalyticEvent<ModuleCreationPayload>
        {
            public CloudCodeModuleCreatedAnalytic(ModuleCreationPayload payload) : base(payload) {}
        }

        [AnalyticInfo(
            eventName: k_EventNameCloudCodeScriptAdded,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionCloudCodeScriptAdded)]
        class CloudCodeScriptAddedAnalytic : CloudCodeAnalyticEvent<ModuleCreationPayload>
        {
            public CloudCodeScriptAddedAnalytic(ModuleCreationPayload payload) : base(payload) {}
        }
    }
}
