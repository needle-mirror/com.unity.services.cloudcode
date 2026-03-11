using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    interface ICloudCodeLocalServer
    {
        enum LocalCloudCodeServerStatus
        {
            Idle,
            Preparing,
            Starting,
            Started,
            Stopping
        }

        event EventHandler<LocalCloudCodeServerStatus> OnServerStatusChanged;
        LocalCloudCodeServerStatus GetCurrentServerStatus();
        string GetLastServerFailure();
        Task StartCompilationAndService(bool restore = false);
        Task StopCompilationAndService();
        ushort GetPort();
        void SetPort(ushort port);
        TextAsset GetSecretsFile();
        void SetSecretsFile(TextAsset path);
        int GetServerPid();
        void ClearServerState();
    }
}
