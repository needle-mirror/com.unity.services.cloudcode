#if UNITY_6000_3_OR_NEWER
namespace Unity.Services.CloudCode.Authoring.Editor.Debugger.Apis
{
    abstract class LocalCloudCodeResponseBase{}

    class CloudCodeLocalHealthCheckResponse : LocalCloudCodeResponseBase
    {
        public string status;
        public string message;
        public int pid;
    }

    class CloudCodeLocalShutdownResponse : LocalCloudCodeResponseBase
    {
        public string message;
        public int shutdowntimeoutSeconds;
    }
}
#endif
