using System;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads
{
    /// <summary>Payload for <c>cloudcode_ccmuDeployed</c>.</summary>
    // Lowercase to match the naming schema
    // ReSharper disable InconsistentNaming
    [Serializable]
    class CcmuDeployedPayload : IAnalytic.IData
    {
        public int size_bytes;
        public string target;
        public long duration_ms;
        public string module_type;
        public bool player_scope_used;
        public bool multiplayer_session_scope_used;
    }

    /// <summary>Failure variant: a separate type keeps the key out of a success, which the serializer would write as "".</summary>
    [Serializable]
    class CcmuDeployedErrorPayload : CcmuDeployedPayload
    {
        public string error;
        public string error_data;
    }
    // ReSharper restore InconsistentNaming
}
