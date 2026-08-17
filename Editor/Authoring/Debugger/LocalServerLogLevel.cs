#if UNITY_6000_3_OR_NEWER
namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    /// <summary>
    /// The minimum severity the local Cloud Code server writes to its log. Mirrors the server's
    /// <c>--log-level</c> option; the names must match the values it accepts.
    /// </summary>
    internal enum LocalServerLogLevel
    {
        Verbose,
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }
}
#endif
