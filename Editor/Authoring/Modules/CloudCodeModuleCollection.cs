#if UNITY_6000_5_OR_NEWER
using Unity.Services.CloudCode.Editor.Shared.Assets;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    class CloudCodeModuleCollection : ObservableAssets<CloudCodeModule>
    {
        public CloudCodeModuleCollection()
            : base(new[] { CloudCodeModuleResources.FileExtension }, new AssetPostprocessorProxy(), true) {}
    }
}
#endif
