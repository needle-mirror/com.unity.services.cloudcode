#if UNITY_6000_3_OR_NEWER
using Status =
    Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics
{
    enum LocalServerEvent
    {
        None,
        Started,
        StartFailed,
        Stopped
    }

    /// <summary>Which analytics event a server status change is worth.</summary>
    static class LocalServerLifecycle
    {
        public static LocalServerEvent Decide(Status previous, Status next) => (previous, next) switch
        {
            // No event if the status hasn't changed. This pair needs its own arm or the next one
            // catches it, which happens when a domain reload picks up a server that never stopped.
            (Status.Started, Status.Started) => LocalServerEvent.None,
            (_, Status.Started) => LocalServerEvent.Started,
            (Status.Started or Status.Stopping, Status.Idle) => LocalServerEvent.Stopped,
            (Status.Preparing or Status.Starting, Status.Idle) => LocalServerEvent.StartFailed,
            _ => LocalServerEvent.None
        };
    }
}
#endif
