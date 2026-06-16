using Unity.Services.CloudCode.Core;

[StateScope(Scope.MultiplayerSession)]
public class CloudCodeModuleCloudTemplate
{
    [CloudCodeFunction("SayHelloCloudCodeModuleCloudTemplate")]
    public string Hello(string name)
    {
        return $"Hello, {name}!";
    }
}
