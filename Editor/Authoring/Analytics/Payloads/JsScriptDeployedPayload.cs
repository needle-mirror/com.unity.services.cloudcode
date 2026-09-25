using System;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads
{
    /// <summary>Payload for <c>cloudcode_jsScriptDeployed</c>.</summary>
    // Lowercase to match the naming schema
    // ReSharper disable InconsistentNaming
    [Serializable]
    class JsScriptDeployedPayload : IAnalytic.IData
    {
        public int size_bytes;
        public long duration_ms;
    }

    /// <summary>Failure variant: a separate type keeps the key out of a success, which the serializer would write as "".</summary>
    [Serializable]
    class JsScriptDeployedErrorPayload : JsScriptDeployedPayload
    {
        public string error;
        public string error_data;
    }
    // ReSharper restore InconsistentNaming
}
