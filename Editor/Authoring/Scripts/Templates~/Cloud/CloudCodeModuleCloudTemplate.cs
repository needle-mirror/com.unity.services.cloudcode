using Unity.Services.CloudCode.Core;

namespace CloudCodeModuleAssemblyTemplate.Cloud
{
    // A Cloud Code script's scope determines how its state is persisted.
    // For more details on available scopes, refer to https://docs.unity.com/en-us/cloud-code/stateful-cloud-code/stateful-cloud-code
    [StateScope(Scope.Player)]
    public class CloudCodeModuleCloudTemplate
    {
        [CloudCodeFunction("SayHelloCloudCodeModuleCloudTemplate")]
        public string Hello(string name)
        {
            return $"Hello, {name}!";
        }
    }
}
