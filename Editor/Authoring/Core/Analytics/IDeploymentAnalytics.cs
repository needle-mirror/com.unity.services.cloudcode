using System;
using System.Collections.Generic;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Analytics
{
    interface IDeploymentAnalytics
    {
        IDisposable Scope();
        IDisposable BeginDeploySend(int fileSize, DeploymentAssetKind kind, DeploymentTarget target, string deployedName);
        void SendFailureDeploymentEvent(string exceptionType, string errorCode, string errorData, DeploymentAssetKind kind, DeploymentTarget target, IReadOnlyList<string> deployedNames);
        void SendSuccessfulPublishEvent(DeploymentAssetKind kind);
        void SendFailurePublishEvent(string exceptionType, DeploymentAssetKind kind);
    }
}
