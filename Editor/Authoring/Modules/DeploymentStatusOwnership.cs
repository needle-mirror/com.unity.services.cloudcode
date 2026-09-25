#if UNITY_6000_3_OR_NEWER
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    /// <summary>
    /// Indicates which <see cref="DeploymentStatus"/> values the modified-trackers own and may
    /// overwrite: no meaningful status yet or one of the two states derived from content.
    /// Transient and error states written by deploys are left alone.
    /// Shared by the module (ccmu) and module-reference (ccmr) trackers, which live behind different define gates.
    /// </summary>
    static class DeploymentStatusOwnership
    {
        public static bool IsReconcilable(DeploymentStatus status)
        {
            return IsEmptyOrUnknown(status)
                || Matches(status, DeploymentStatus.UpToDate)
                || Matches(status, DeploymentStatus.ModifiedLocally);
        }

        public static bool IsContentDerived(DeploymentStatus status)
        {
            return Matches(status, DeploymentStatus.UpToDate)
                || Matches(status, DeploymentStatus.ModifiedLocally);
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
