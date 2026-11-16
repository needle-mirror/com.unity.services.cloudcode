#if UNITY_6000_3_OR_NEWER
using System.Threading.Tasks;
using ClearStateResult = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.ClearStateResult;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    static class LocalServerStateReset
    {
        // Clears the state of a server a Play Mode Scenario keeps running, which the scenario restarts on the
        // next Play. Deleting the state under a running server needs a restart anyway, so it is stopped first.
        internal static async Task<ClearStateResult> StopAndClearAsync(ICloudCodeLocalServer server)
        {
            if (server.GetCurrentServerStatus() != LocalCloudCodeServerStatus.Idle)
                await server.StopCompilationAndService();

            // The stop records a failure rather than throwing, so check it actually left the server idle.
            if (server.GetCurrentServerStatus() != LocalCloudCodeServerStatus.Idle)
                return ClearStateResult.Failed;

            return server.ClearServerState();
        }
    }
}
#endif
