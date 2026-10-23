using System;
using Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads;
using Unity.Services.CloudCode.Editor.Shared.Analytics;
using Unity.Services.CloudCode.Editor.Shared.EditorUtils;
using UnityEngine.Analytics;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;

using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    class CloudCodeModuleReferenceBindingsGenerationAnalytics : ICloudCodeModuleReferenceBindingsGenerationAnalytics
    {
        // Deprecated: these are shared_common actions, superseded by the source field on
        // cloudcode_ccmrBindingsGenerated. Remove once their dashboards have moved.
        const string k_EventNameBindingGenFromInspector = "ccm_bindings_inspector_btn";
        const string k_EventNameBindingGenFromTopMenu = "ccm_bindings_topmenu";
        const string k_EventNameBindingGenFromProjectSettings = "ccm_bindings_project_settings";
        const string k_EventNameBindingGenFromCommand = "ccm_bindings_command";

        const string k_EventNameBindingsGenerated = "cloudcode_ccmrBindingsGenerated";
        const int k_VersionBindingsGenerated = 1;

        readonly ILogger m_Logger;
        readonly ICommonAnalytics m_CommonAnalytics;
        readonly IAnalyticsSender m_Sender;

        public CloudCodeModuleReferenceBindingsGenerationAnalytics(
            ICommonAnalytics commonAnalytics, ILogger logger, IAnalyticsSender sender)
        {
            m_CommonAnalytics = commonAnalytics;
            m_Logger = logger;
            m_Sender = sender;
        }

        public void SendCodeGenerationFromInspectorBtnEvent(Exception exception = null)
        {
            Send(k_EventNameBindingGenFromInspector, AnalyticsSource.Inspector, exception);
        }

        public void SendCodeGenerationFromTopMenuEvent(Exception exception = null)
        {
            Send(k_EventNameBindingGenFromTopMenu, AnalyticsSource.TopMenu, exception);
        }

        public void SendCodeGenerationFromProjectSettingsEvent(Exception exception = null)
        {
            Send(k_EventNameBindingGenFromProjectSettings, AnalyticsSource.ProjectSettings, exception);
        }

        public void SendCodeGenerationFromCommandEvent(Exception exception = null)
        {
            Send(k_EventNameBindingGenFromCommand, AnalyticsSource.DeploymentWindow, exception);
        }

        void Send(string legacyAction, AnalyticsSource source, Exception exception)
        {
            Sync.RunNextUpdateOnMain(() =>
            {
                var result = m_CommonAnalytics.Send(new ICommonAnalytics.CommonEventPayload
                {
                    action = legacyAction,
                    context = nameof(CloudCodeModuleReferenceBindingsGenerationAnalytics),
                    exception = exception?.GetType().FullName
                });
                m_Logger.LogVerbose($"Sent Analytics Event: {legacyAction}. Result: {result}");

                var bindingsResult = m_Sender.Send(new BindingsGeneratedAnalytic(
                    GenerationPayloads.For(
                        source,
                        AnalyticsErrorCode.FromException(exception),
                        AnalyticsErrorData.FromException(exception))));
                m_Logger.LogVerbose(
                    $"Sent Analytics Event: {k_EventNameBindingsGenerated}.v{k_VersionBindingsGenerated}. Result: {bindingsResult}");
            });
        }

        [AnalyticInfo(
            eventName: k_EventNameBindingsGenerated,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionBindingsGenerated)]
        class BindingsGeneratedAnalytic : CloudCodeAnalyticEvent<GenerationEventPayload>
        {
            public BindingsGeneratedAnalytic(GenerationEventPayload payload) : base(payload) {}
        }
    }
}
