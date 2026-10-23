using System;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    /// <summary>
    /// Base for Cloud Code editor analytics events. A concrete event adds an
    /// <see cref="AnalyticInfoAttribute"/> and its payload. The payload is built at the call site,
    /// since <see cref="TryGatherData"/> runs when the platform processes the event, not when it is raised.
    /// </summary>
    abstract class CloudCodeAnalyticEvent<TPayload> : IAnalytic where TPayload : IAnalytic.IData
    {
        readonly TPayload m_Payload;

        protected CloudCodeAnalyticEvent(TPayload payload)
        {
            m_Payload = payload;
        }

        public bool TryGatherData(out IAnalytic.IData data, out Exception error)
        {
            error = null;
            data = m_Payload;
            return data != null;
        }
    }
}
