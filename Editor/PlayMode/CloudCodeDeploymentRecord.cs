using System;
using System.Globalization;
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using UnityEditor;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    enum CloudCodeDeploymentOutcome
    {
        None,
        Succeeded,
        Failed,
        NoModules
    }

    readonly struct CloudCodeDeployment
    {
        public readonly CloudCodeDeploymentOutcome Outcome;
        public readonly CloudCodeDeployTarget Target;
        public readonly string Environment;
        public readonly DateTime TimeUtc;
        public readonly string Message;

        public CloudCodeDeployment(
            CloudCodeDeploymentOutcome outcome,
            CloudCodeDeployTarget target,
            string environment,
            DateTime timeUtc,
            string message)
        {
            Outcome = outcome;
            Target = target;
            Environment = environment;
            TimeUtc = timeUtc;
            Message = message;
        }
    }

    // The last module deployment a scenario ran, for the Play Mode Status window. SessionState survives the
    // play mode domain reloads, so the record is still there when the window rebuilds after a run.
    static class CloudCodeDeploymentRecord
    {
        const string k_Prefix = "CloudCode.PlayMode.LastDeployment.";
        const string k_OutcomeKey = k_Prefix + "Outcome";
        const string k_TargetKey = k_Prefix + "Target";
        const string k_EnvironmentKey = k_Prefix + "Environment";
        const string k_TimeKey = k_Prefix + "TimeUtc";
        const string k_MessageKey = k_Prefix + "Message";

        internal static event Action Changed;

        internal static void Record(
            CloudCodeDeployTarget target,
            string environment,
            CloudCodeDeploymentOutcome outcome,
            string message = null)
        {
            SessionState.SetInt(k_OutcomeKey, (int)outcome);
            SessionState.SetInt(k_TargetKey, (int)target);
            SessionState.SetString(k_EnvironmentKey, environment ?? string.Empty);
            SessionState.SetString(k_TimeKey, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            SessionState.SetString(k_MessageKey, message ?? string.Empty);
            Changed?.Invoke();
        }

        internal static bool TryGet(out CloudCodeDeployment deployment)
        {
            var outcome = (CloudCodeDeploymentOutcome)SessionState.GetInt(k_OutcomeKey, (int)CloudCodeDeploymentOutcome.None);
            if (outcome == CloudCodeDeploymentOutcome.None
                || !DateTime.TryParse(SessionState.GetString(k_TimeKey, null), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out var timeUtc))
            {
                deployment = default;
                return false;
            }

            deployment = new CloudCodeDeployment(
                outcome,
                (CloudCodeDeployTarget)SessionState.GetInt(k_TargetKey, 0),
                NullIfEmpty(SessionState.GetString(k_EnvironmentKey, null)),
                timeUtc,
                NullIfEmpty(SessionState.GetString(k_MessageKey, null)));
            return true;
        }

        internal static bool HasSucceeded(CloudCodeDeployTarget target, string environment)
        {
            return TryGet(out var deployment)
                && deployment.Target == target
                && deployment.Environment == environment
                && deployment.Outcome == CloudCodeDeploymentOutcome.Succeeded;
        }

        internal static void Clear()
        {
            SessionState.EraseInt(k_OutcomeKey);
            SessionState.EraseInt(k_TargetKey);
            SessionState.EraseString(k_EnvironmentKey);
            SessionState.EraseString(k_TimeKey);
            SessionState.EraseString(k_MessageKey);
            Changed?.Invoke();
        }

        internal static bool HasModules(CloudCodeAuthoringServices services) => ModuleCount(services) > 0;

        internal static int ModuleCount(CloudCodeAuthoringServices services)
        {
            return services.GetService<CloudCodeModuleReferenceCollection>().Count
                + services.GetService<CloudCodeModuleCollection>().Count;
        }

        static string NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
    }
}
