using System;
using System.Collections.Generic;
using Unity.Services.CloudCode.Authoring.Editor.Analytics.Payloads;
using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Editor.Shared.Analytics;
using Unity.Services.CloudCode.Editor.Shared.Logging;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;
using UnityEngine.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Analytics.Deployment
{
    class DeploymentAnalytics : IDeploymentAnalytics
    {
        // Deprecated: superseded by cloudcode_jsScriptDeployed, cloudcode_ccmrDeployed and
        // cloudcode_ccmuDeployed. Remove once its dashboards have moved.
        const string k_EventNameDeploy = "cloudcode_filedeployed";
        const int k_VersionDeploy = 1;

        // Deprecated: superseded by cloudcode_jsScriptPublished. Remove once its dashboards have moved.
        const string k_EventNamePublish = "cloudcode_filepublished";
        const int k_VersionPublish = 1;

        const string k_EventNameJsScriptDeployed = "cloudcode_jsScriptDeployed";
        const int k_VersionJsScriptDeployed = 1;

        const string k_EventNameJsScriptPublished = "cloudcode_jsScriptPublished";
        const int k_VersionJsScriptPublished = 1;

        const string k_EventNameCcmrDeployed = "cloudcode_ccmrDeployed";
        const int k_VersionCcmrDeployed = 1;

        const string k_EventNameCcmuDeployed = "cloudcode_ccmuDeployed";
        const int k_VersionCcmuDeployed = 1;

        const string k_TargetLocal = "local";
        const string k_TargetRemote = "remote";

        const string k_UserAction = "automatic";

        readonly Lazy<IEnvironmentProvider> m_EnvironmentProvider;
        readonly IModuleMetadataProvider m_ModuleMetadata;
        readonly IAnalyticsSender m_Sender;

        public DeploymentAnalytics(
            Lazy<IEnvironmentProvider> environmentProvider,
            IModuleMetadataProvider moduleMetadata,
            IAnalyticsSender sender)
        {
            m_ModuleMetadata = moduleMetadata;
            m_Sender = sender;
            AnalyticsUtils.RegisterEventDefault(k_EventNameDeploy, k_VersionDeploy);
            AnalyticsUtils.RegisterEventDefault(k_EventNamePublish, k_VersionPublish);
            m_EnvironmentProvider = environmentProvider;
        }

        public IDisposable Scope()
        {
            return new DisposableScope(() => {});
        }

        public IDisposable BeginDeploySend(int fileSize, DeploymentAssetKind kind, DeploymentTarget target, string deployedName)
        {
            return new AnalyticsTimer((duration) => SendSuccessfulDeploymentEvent(duration, fileSize, kind, target, deployedName));
        }

        void SendSuccessfulDeploymentEvent(int duration, int fileSize, DeploymentAssetKind kind, DeploymentTarget target, string deployedName)
        {
            var deploymentArguments = new DeploymentParameters()
            {
                origin = k_UserAction,
                environment = m_EnvironmentProvider.Value.Current,
                status = "success",
                exception = null,
                duration = duration,
                size = fileSize,
                fileType =  LegacyFileType(kind),
            };
            SendDeployEvent(target, deploymentArguments);

            switch (kind)
            {
                case DeploymentAssetKind.Script:
                    SendJsScriptDeployedEvent(JsPayload(fileSize, duration, null));
                    break;
                case DeploymentAssetKind.ModuleReference:
                    SendCcmrDeployedEvent(CcmrPayload(target, fileSize, duration, null));
                    break;
                case DeploymentAssetKind.Module:
                    SendCcmuDeployedEvent(CcmuPayload(deployedName, target, fileSize, duration, null));
                    break;
            }
        }

        public void SendFailureDeploymentEvent(string exceptionType, string errorCode, string errorData, DeploymentAssetKind kind, DeploymentTarget target, IReadOnlyList<string> deployedNames)
        {
            var deploymentArguments = new DeploymentParameters()
            {
                origin = k_UserAction,
                environment = m_EnvironmentProvider.Value.Current,
                status = "failure",
                exception = exceptionType,
                duration = 0,
                size = 0
            };
            SendDeployEvent(target, deploymentArguments);

            // One event per item the failure covers: these events are per-asset, so a batch failure
            // that fails N modules reports N of them, matching the success path.
            foreach (var deployedName in deployedNames ?? Array.Empty<string>())
            {
                switch (kind)
                {
                    case DeploymentAssetKind.Script:
                        SendJsScriptDeployedEvent(JsPayload(0, 0, errorCode, errorData));
                        break;
                    case DeploymentAssetKind.ModuleReference:
                        SendCcmrDeployedEvent(CcmrPayload(target, 0, 0, errorCode, errorData));
                        break;
                    case DeploymentAssetKind.Module:
                        SendCcmuDeployedEvent(CcmuPayload(deployedName, target, 0, 0, errorCode, errorData));
                        break;
                }
            }
        }

        // Success builds the base type, so the error key is absent rather than empty.
        internal static JsScriptDeployedPayload JsPayload(int size, int duration, string error, string errorData = null)
        {
            var payload = error == null
                ? new JsScriptDeployedPayload()
                : new JsScriptDeployedErrorPayload { error = error, error_data = errorData };

            payload.size_bytes = size;
            payload.duration_ms = duration;
            return payload;
        }

        internal static CcmrDeployedPayload CcmrPayload(DeploymentTarget target, int size, int duration, string error, string errorData = null)
        {
            var payload = error == null
                ? new CcmrDeployedPayload()
                : new CcmrDeployedErrorPayload { error = error, error_data = errorData };

            payload.size_bytes = size;
            payload.target = TargetName(target);
            payload.duration_ms = duration;
            return payload;
        }

        CcmuDeployedPayload CcmuPayload(string deployedName, DeploymentTarget target, int size, int duration, string error, string errorData = null)
        {
            var metadata = m_ModuleMetadata.GetMetadata(deployedName);
            var payload = error == null
                ? new CcmuDeployedPayload()
                : new CcmuDeployedErrorPayload { error = error, error_data = errorData };

            payload.size_bytes = size;
            payload.target = TargetName(target);
            payload.duration_ms = duration;
            payload.module_type = metadata.Type.ToAnalyticsValue();
            payload.player_scope_used = metadata.PlayerScopeUsed;
            payload.multiplayer_session_scope_used = metadata.MultiplayerSessionScopeUsed;
            return payload;
        }

        // The deprecated cloudcode_filedeployed schema only distinguishes scripts from modules.
        static string LegacyFileType(DeploymentAssetKind kind)
        {
            return kind == DeploymentAssetKind.Script
                ? DeploymentFileTypes.k_Script
                : DeploymentFileTypes.k_Module;
        }

        static string TargetName(DeploymentTarget target)
        {
            return target == DeploymentTarget.Local ? k_TargetLocal : k_TargetRemote;
        }

        public void SendSuccessfulPublishEvent(DeploymentAssetKind kind)
        {
            var publishParameters = new PublishParameters()
            {
                origin = k_UserAction,
                environment = m_EnvironmentProvider.Value.Current,
                status = "success",
                exception = null,
            };
            SendPublishEvent(publishParameters);

            if (kind == DeploymentAssetKind.Script)
            {
                SendJsScriptPublishedEvent(ErrorPayloads.For(null));
            }
        }

        public void SendFailurePublishEvent(string exceptionType, DeploymentAssetKind kind)
        {
            var publishParameters = new PublishParameters()
            {
                origin = k_UserAction,
                environment = m_EnvironmentProvider.Value.Current,
                status = "failure",
                exception = exceptionType,
            };
            SendPublishEvent(publishParameters);

            if (kind == DeploymentAssetKind.Script)
            {
                SendJsScriptPublishedEvent(ErrorPayloads.For(
                    AnalyticsErrorCode.k_AdminApiError,
                    AnalyticsErrorData.FromExceptionName(exceptionType)));
            }
        }

        // Remote only: the deprecated event has no target field, so a local deploy would be
        // indistinguishable from a remote one.
        void SendDeployEvent(DeploymentTarget target, DeploymentParameters deploymentParameters)
        {
            if (target != DeploymentTarget.Remote)
                return;

#if UNITY_2023_2_OR_NEWER
            var res = m_Sender.Send(new DeployAnalytic(deploymentParameters));
#else
            var res = EditorAnalytics.SendEventWithLimit(k_EventNameDeploy, deploymentParameters, k_VersionDeploy);
#endif
            LogVerbose(k_EventNameDeploy, k_VersionDeploy, res);
        }

        void SendPublishEvent(PublishParameters publishParameters)
        {
#if UNITY_2023_2_OR_NEWER
            var res = m_Sender.Send(new PublishAnalytic(publishParameters));
#else
            var res = EditorAnalytics.SendEventWithLimit(k_EventNamePublish, publishParameters, k_VersionPublish);
#endif
            LogVerbose(k_EventNamePublish, k_VersionPublish, res);
        }

        void SendJsScriptDeployedEvent(JsScriptDeployedPayload payload)
        {
            var res = m_Sender.Send(new JsScriptDeployedAnalytic(payload));
            LogVerbose(k_EventNameJsScriptDeployed, k_VersionJsScriptDeployed, res);
        }

        void SendJsScriptPublishedEvent(EmptyPayload payload)
        {
            var res = m_Sender.Send(new JsScriptPublishedAnalytic(payload));
            LogVerbose(k_EventNameJsScriptPublished, k_VersionJsScriptPublished, res);
        }

        void SendCcmrDeployedEvent(CcmrDeployedPayload payload)
        {
            var res = m_Sender.Send(new CcmrDeployedAnalytic(payload));
            LogVerbose(k_EventNameCcmrDeployed, k_VersionCcmrDeployed, res);
        }

        void SendCcmuDeployedEvent(CcmuDeployedPayload payload)
        {
            var res = m_Sender.Send(new CcmuDeployedAnalytic(payload));
            LogVerbose(k_EventNameCcmuDeployed, k_VersionCcmuDeployed, res);
        }

        static void LogVerbose(string eventName, int version, AnalyticsResult result)
        {
            Logger.LogVerbose($"Sent Analytics Event: {eventName}.v{version}. Result: {result}");
        }

#if UNITY_2023_2_OR_NEWER
        [AnalyticInfo(
            eventName: k_EventNameDeploy,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionDeploy)]
        class DeployAnalytic : IAnalytic
        {
            readonly DeploymentParameters m_Payload;

            public DeployAnalytic(DeploymentParameters payload)
            {
                m_Payload = payload;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Payload;
                return true;
            }
        }

        [AnalyticInfo(
            eventName: k_EventNamePublish,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionPublish)]
        class PublishAnalytic : IAnalytic
        {
            readonly PublishParameters m_Payload;

            public PublishAnalytic(PublishParameters payload)
            {
                m_Payload = payload;
            }

            public bool TryGatherData(out IAnalytic.IData data, out Exception error)
            {
                error = null;
                data = m_Payload;
                return true;
            }
        }
#endif

        [AnalyticInfo(
            eventName: k_EventNameJsScriptDeployed,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionJsScriptDeployed)]
        class JsScriptDeployedAnalytic : CloudCodeAnalyticEvent<JsScriptDeployedPayload>
        {
            public JsScriptDeployedAnalytic(JsScriptDeployedPayload payload) : base(payload) {}
        }

        [AnalyticInfo(
            eventName: k_EventNameJsScriptPublished,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionJsScriptPublished)]
        class JsScriptPublishedAnalytic : CloudCodeAnalyticEvent<EmptyPayload>
        {
            public JsScriptPublishedAnalytic(EmptyPayload payload) : base(payload) {}
        }

        [AnalyticInfo(
            eventName: k_EventNameCcmrDeployed,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionCcmrDeployed)]
        class CcmrDeployedAnalytic : CloudCodeAnalyticEvent<CcmrDeployedPayload>
        {
            public CcmrDeployedAnalytic(CcmrDeployedPayload payload) : base(payload) {}
        }

        [AnalyticInfo(
            eventName: k_EventNameCcmuDeployed,
            vendorKey: AnalyticsConstants.k_VendorKey,
            version: k_VersionCcmuDeployed)]
        class CcmuDeployedAnalytic : CloudCodeAnalyticEvent<CcmuDeployedPayload>
        {
            public CcmuDeployedAnalytic(CcmuDeployedPayload payload) : base(payload) {}
        }
    }
}
