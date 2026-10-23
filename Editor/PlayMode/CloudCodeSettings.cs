using System;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    // No constructor and no field initializers: the scenario system's per-user settings store
    // crashes the editor on structs that declare a parameterless constructor.
    [Serializable]
    struct CloudCodeSettings
    {
        public bool DeployCloudCode;
        public CloudCodeDeployTarget DeployTarget;
    }
}
