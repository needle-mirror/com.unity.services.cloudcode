using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    // Deprecated: the four sends are shared_common actions, superseded by the source field on
    // cloudcode_ccmrBindingsGenerated, which these methods also raise.
    interface ICloudCodeModuleReferenceBindingsGenerationAnalytics
    {
        public void SendCodeGenerationFromInspectorBtnEvent(Exception exception = null);
        public void SendCodeGenerationFromTopMenuEvent(Exception exception = null);
        public void SendCodeGenerationFromProjectSettingsEvent(Exception exception = null);
        public void SendCodeGenerationFromCommandEvent(Exception exception = null);
    }
}
