namespace Unity.Services.CloudCode.Authoring.Editor.Core.Analytics
{
    /// <summary>
    /// Which Cloud Code asset a deployment is for. Selects the analytics event, which
    /// <c>Language</c> cannot: a .ccmr and a .ccmu are both <c>Language.CS</c>.
    /// </summary>
    enum DeploymentAssetKind
    {
        Script,
        ModuleReference,
        Module
    }
}
