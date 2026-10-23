using System;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads
{
    /// <summary>
    /// Payload for the generation events: cloudcode_ccmrBindingsGenerated and
    /// cloudcode_ccmrSolutionGenerated. Adding a field here changes both schemas.
    /// </summary>
    // Lowercase to match the naming schema
    // ReSharper disable InconsistentNaming
    [Serializable]
    class GenerationEventPayload : IAnalytic.IData
    {
        public string source;
    }

    /// <summary>Failure variant: a separate type keeps the key out of a success, which the serializer would write as "".</summary>
    [Serializable]
    class GenerationEventErrorPayload : GenerationEventPayload
    {
        public string error;
        public string error_data;
    }
    // ReSharper restore InconsistentNaming
}
