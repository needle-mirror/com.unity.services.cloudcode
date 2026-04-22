#if UNITY_SERVICES_CLOUDCODE_EXPERIMENTAL
namespace Unity.Services.CloudCode.Authoring.Editor.Debugger.Apis
{
    abstract class LocalCloudCodeRequestBase
    {
        public abstract string ConstructUrl(string url);
    }

    class CloudCodeLocalHealthCheckRequest : LocalCloudCodeRequestBase
    {
        public override string ConstructUrl(string requestBasePath)
        {
            return requestBasePath + "/v1/lifecycle/health";
        }
    }

    class CloudCodeLocalShutdownRequest : LocalCloudCodeRequestBase
    {
        public override string ConstructUrl(string requestBasePath)
        {
            return requestBasePath + "/v1/lifecycle/shutdown";
        }
    }
}
#endif
