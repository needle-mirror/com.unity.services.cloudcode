namespace Unity.Services.CloudCode.Authoring.Editor.Core.Analytics
{
    /// <summary>
    /// Whether a deployment was asked for by the user. Automatic ones are not reported: the local
    /// server redeploys on focus regain, after every recompile and on start, which would swamp the
    /// deploy events with machine activity.
    /// </summary>
    enum DeploymentOrigin
    {
        Automatic,
        Manual
    }
}
