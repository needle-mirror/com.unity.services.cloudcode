using System.IO;
using Unity.Multiplayer.PlayMode.Editor;
using UnityEditor;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    // Carries the main editor's Cloud Code endpoint prefs to the clone editors, which write them into their
    // own EditorPrefs. Syncing EditorPrefs through disk is not enough: on macOS an editor with unsaved pref
    // changes of its own saves its whole in-memory copy on EditorPrefs.Sync instead of reloading, so a clone
    // that wrote any pref keeps, and writes back, the endpoint it started with.
    //
    // TODO(MTT-16268): Replace this with the MPPM module's EditorPrefsSyncedMessage on trunk,
    // made to carry the changed keys and values so clones apply them directly instead of relying on EditorPrefs.Sync.
    // That is a change to the Unity Editor, which is blocked by the 6000.7 feature freeze until Oct 27, 2026.
    class CloudCodeEndpointMessage
    {
        // Read by CloudCodeInitializer when Cloud Code initializes in Play Mode.
        internal const string k_ServerPidPref = "LOCAL_CLOUD_CODE_PID";
        internal const string k_ServerPortPref = "CLOUD_CODE_DEBUG_PORT";
        internal const int k_NoServerPid = -1;

        public int ServerPid;
        public int ServerPort;

        public CloudCodeEndpointMessage(int serverPid, int serverPort)
        {
            ServerPid = serverPid;
            ServerPort = serverPort;
        }

        internal void ApplyToEditorPrefs()
        {
            EditorPrefs.SetInt(k_ServerPidPref, ServerPid);
            EditorPrefs.SetInt(k_ServerPortPref, ServerPort);
        }

        internal static void Serialize(BinaryWriter writer, object message)
        {
            var endpoint = (CloudCodeEndpointMessage)message;
            writer.Write(endpoint.ServerPid);
            writer.Write(endpoint.ServerPort);
        }

        internal static object Deserialize(BinaryReader reader) =>
            new CloudCodeEndpointMessage(reader.ReadInt32(), reader.ReadInt32());

        [SerializeMessageDelegates] // ReSharper disable once UnusedMember.Global
        public static SerializeMessageDelegates SerializeMethods() =>
            new SerializeMessageDelegates { SerializeFunc = Serialize, DeserializeFunc = Deserialize };

        [InitializeOnLoadMethod]
        static void RegisterCloneReceiver()
        {
            if (!VirtualProjectsEditor.IsClone)
                return;

            // The clone's messaging service is recreated on every domain reload, so this re-registers each time.
            EditorContexts.OnInitialized += () =>
                EditorContexts.CloneContext?.MessagingService.Receive<CloudCodeEndpointMessage>(m => m.ApplyToEditorPrefs());
        }
    }
}
