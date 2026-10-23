#if UNITY_6000_3_OR_NEWER
namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    interface ILocalServerAnalytics
    {
        /// <summary>A null error means the start succeeded</summary>
        void SendServerStartedEvent(string error = null, string errorData = null);

        /// <summary>Pass 0 when no start time is on record</summary>
        void SendServerStoppedEvent(long upTimeMs);

        void SendServerStateClearedEvent();
    }
}
#endif
