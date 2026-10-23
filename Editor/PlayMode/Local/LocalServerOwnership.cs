using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using UnityEditor;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    // A scenario that starts the local server owns it until the scenario is switched away, even if its
    // Cloud Code settings change in between. SessionState survives the play mode domain reloads, and the
    // process id keeps a server restarted from the toolbar from being claimed.
    static class LocalServerOwnership
    {
        const string k_OwnedServerPidKey = "CloudCode.PlayMode.OwnedLocalServerPid";
        const int k_NoServer = -1;

        internal static void Claim(ICloudCodeLocalServer server)
        {
            SessionState.SetInt(k_OwnedServerPidKey, server.GetServerPid());
        }

        internal static bool IsOwned(ICloudCodeLocalServer server)
        {
            var ownedPid = SessionState.GetInt(k_OwnedServerPidKey, k_NoServer);
            return ownedPid != k_NoServer && ownedPid == server.GetServerPid();
        }

        internal static void Release()
        {
            SessionState.EraseInt(k_OwnedServerPidKey);
        }
    }
}
