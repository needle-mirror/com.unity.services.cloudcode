using System;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    [Serializable]
    struct CloudCodeEndpoint
    {
        public const int k_NoServerPid = -1;

        public CloudCodeDeployTarget Target;
        public ushort Port;
        public int ServerPid;

        public static CloudCodeEndpoint Local(ushort port, int serverPid)
        {
            return new CloudCodeEndpoint
            {
                Target = CloudCodeDeployTarget.Local,
                Port = port,
                ServerPid = serverPid
            };
        }

        public static CloudCodeEndpoint Remote()
        {
            return new CloudCodeEndpoint
            {
                Target = CloudCodeDeployTarget.Remote,
                Port = 0,
                ServerPid = k_NoServerPid
            };
        }
    }
}
