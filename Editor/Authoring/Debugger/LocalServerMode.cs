#if UNITY_6000_3_OR_NEWER
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    // Local-only mode launches the server with --local-only-mode, which stubs its lobby lookup and
    // access control, and lifts the environment requirement on this side: a start skips the environment
    // validation, and deploys land in a fallback folder when no environment is selected. Only the test
    // harness asks for it, and every fresh start sets it, so a start from the Editor always runs without
    // it. SessionState keeps it through the Play Mode domain reloads the end-to-end fixtures drive.
    static class LocalServerMode
    {
        const string k_LocalOnlyKey = "Unity.Services.CloudCode.LocalServer.LocalOnly";

        internal static bool LocalOnly
        {
            get => SessionState.GetBool(k_LocalOnlyKey, false);
            set => SessionState.SetBool(k_LocalOnlyKey, value);
        }

        internal static void Clear() => SessionState.EraseBool(k_LocalOnlyKey);
    }
}
#endif
