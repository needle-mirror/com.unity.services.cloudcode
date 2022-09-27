using Unity.Services.CloudCode.Authoring.Editor.Core.Model;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Crypto
{
    interface IHashComputer
    {
        string ComputeFileHash(IScript script);
    }
}
