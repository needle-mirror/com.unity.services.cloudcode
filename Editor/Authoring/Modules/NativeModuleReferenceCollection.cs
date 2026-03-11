using Unity.Services.CloudCode.Editor.Shared.Assets;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    class NativeModuleReferenceCollection : ObservableAssets<NativeModuleReference>
    {
        public NativeModuleReferenceCollection()
            : base(new[] { ".asset" }, new AssetPostprocessorProxy(), true) {}
    }
}
