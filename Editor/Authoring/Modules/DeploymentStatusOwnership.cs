#if UNITY_6000_3_OR_NEWER
using Unity.Services.DeploymentApi.Editor;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    /// <summary>
    /// Indicates which <see cref="DeploymentStatus"/> values the modified-trackers own and may
    /// overwrite: no meaningful status yet or one of the states derived from content.
    /// Transient and error states written by deploys are left alone.
    /// Shared by the module (ccmu) and module-reference (ccmr) trackers, which live behind different define gates.
    /// </summary>
    static class DeploymentStatusOwnership
    {
        public static readonly DeploymentStatus Deployed = DeploymentStatus.GetDeployed(string.Empty);

        public static readonly DeploymentStatus DeployedToLocalServer =
            new DeploymentStatus("Deployed to Local Server", string.Empty, SeverityLevel.Success);

        public static bool IsReconcilable(DeploymentStatus status)
        {
            return IsEmptyOrUnknown(status) || IsContentDerived(status);
        }

        public static bool IsContentDerived(DeploymentStatus status)
        {
            return Matches(status, DeploymentStatus.UpToDate)
                || Matches(status, DeploymentStatus.ModifiedLocally)
                || Matches(status, Deployed)
                || Matches(status, DeployedToLocalServer);
        }

        /// <summary>
        /// The status for a module whose content matches its freshest deployment to <paramref name="target"/>.
        /// A just-deployed status is kept, detail included, until the content diverges.
        /// </summary>
        public static DeploymentStatus InSync(DeploymentStatus current, DeploymentTarget target)
        {
            var inSync = target == DeploymentTarget.Local ? DeployedToLocalServer : DeploymentStatus.UpToDate;
            if (Matches(current, inSync)
                || (target == DeploymentTarget.Remote && Matches(current, Deployed)))
                return current;

            return inSync;
        }

        static bool IsEmptyOrUnknown(DeploymentStatus status)
        {
            return string.IsNullOrEmpty(status.Message) && status.MessageSeverity == SeverityLevel.None;
        }

        public static bool Matches(DeploymentStatus status, DeploymentStatus other)
        {
            return status.Message == other.Message
                && status.MessageSeverity == other.MessageSeverity;
        }
    }
}
#endif
