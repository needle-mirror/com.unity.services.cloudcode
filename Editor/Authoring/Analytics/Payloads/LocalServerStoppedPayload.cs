using System;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads
{
    /// <summary>Payload for <c>cloudcode_localServerStopped</c>.</summary>
    // Lowercase to match the naming schema
    // ReSharper disable InconsistentNaming
    [Serializable]
    struct LocalServerStoppedPayload : IAnalytic.IData
    {
        public long up_time_ms;
    }
    // ReSharper restore InconsistentNaming
}
