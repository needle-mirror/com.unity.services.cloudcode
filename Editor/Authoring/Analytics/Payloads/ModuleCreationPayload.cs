using System;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads
{
    /// <summary>
    /// Payload for the module creation events: cloudcode_ccmuModuleCreated and
    /// cloudcode_ccmuModuleScriptAdded. Adding a field here changes both schemas.
    /// </summary>
    // Lowercase to match the naming schema
    // ReSharper disable InconsistentNaming
    [Serializable]
    class ModuleCreationPayload : IAnalytic.IData
    {
        public string module_type;
    }

    /// <summary>Failure variant: a separate type keeps the key out of a success, which the serializer would write as "".</summary>
    [Serializable]
    class ModuleCreationErrorPayload : ModuleCreationPayload
    {
        public string error;
        public string error_data;
    }
    // ReSharper restore InconsistentNaming
}
