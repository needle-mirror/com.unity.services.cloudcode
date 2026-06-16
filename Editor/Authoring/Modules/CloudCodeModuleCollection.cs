using Unity.Services.CloudCode.Editor.Shared.Assets;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    class CloudCodeModuleCollection : ObservableAssets<CloudCodeModule>
    {
        public CloudCodeModuleCollection()
            : base(new[] { CloudCodeModuleResources.FileExtension }, new AssetPostprocessorProxy(), true) {}
    }
}
