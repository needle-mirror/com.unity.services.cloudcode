namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    /// <summary>Value domain of the source analytics field: the entry point the user acted from.</summary>
    enum AnalyticsSource
    {
        Inspector,
        TopMenu,
        ProjectSettings,
        DeploymentWindow
    }

    static class AnalyticsSourceExtensions
    {
        public static string ToAnalyticsValue(this AnalyticsSource source)
        {
            switch (source)
            {
                case AnalyticsSource.TopMenu:
                    return "topMenu";
                case AnalyticsSource.ProjectSettings:
                    return "projectSettings";
                case AnalyticsSource.DeploymentWindow:
                    return "deploymentWindow";
                default:
                    return "inspector";
            }
        }
    }
}
