using System;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules.UI
{
    // Set by the Play Mode Scenario integration, which references this assembly rather than the reverse.
    internal static class CloudCodeLocalServerScenarioControl
    {
        // Returns how the active scenario controls Cloud Code, or null when the toolbar does.
        internal static Func<Info> GetActiveControl;

        internal sealed class Info
        {
            internal Info(string message, bool usesLocalServer)
            {
                Message = message;
                UsesLocalServer = usesLocalServer;
            }

            internal string Message { get; }

            // False for a remote deploy, so the popup hides a status the user can no longer act on.
            internal bool UsesLocalServer { get; }
        }
    }
}
