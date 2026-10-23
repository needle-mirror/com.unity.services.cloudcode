using System;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads
{
    /// <summary>
    /// Payload for the events whose only field is the error code: jsScriptCreated,
    /// jsScriptPublished, ccmrCreated, ccmrSolutionGenerated, ccmuModuleCreated,
    /// ccmuModuleScriptAdded, localServerStarted. A success carries no fields at all.
    ///
    /// The error field lives on a derived type rather than being left null, because the Unity
    /// serializer writes "" for a null string and the backend stores that as an empty string
    /// rather than NULL. Omitting the key is the only way to record the absence of an error, and
    /// a type cannot omit a field it declares. Every payload with an error field is split this way.
    /// </summary>
    // Lowercase to match the naming schema
    // ReSharper disable InconsistentNaming
    [Serializable]
    class EmptyPayload : IAnalytic.IData
    {
    }

    /// <summary>Failure variant: a separate type keeps the key out of a success, which the serializer would write as "".</summary>
    [Serializable]
    class CloudCodeErrorPayload : EmptyPayload
    {
        public string error;
        public string error_data;
    }
    // ReSharper restore InconsistentNaming
}
