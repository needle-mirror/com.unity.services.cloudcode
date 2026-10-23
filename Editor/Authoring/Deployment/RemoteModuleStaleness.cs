#if UNITY_6000_3_OR_NEWER
using System;
using System.IO;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using DeploymentTarget = Unity.Services.CloudCode.Authoring.Editor.Core.Model.LastSuccessfulDeploymentInfo.DeploymentTarget;

namespace Unity.Services.CloudCode.Authoring.Editor.Deployment
{
    enum StalenessState
    {
        UpToDate,
        Stale,
        Unknown
    }

    enum StalenessReason
    {
        None,
        ModifiedSinceLastDeploy,
        SourceChangedOnDisk,
        NoRemoteRecord,
        NoRecordedHash,
        CannotHash,
        NoSourceDirectory,
        BuildInputsNotCovered,
        SourceScanFailed
    }

    readonly struct StalenessVerdict
    {
        public readonly StalenessState State;
        public readonly StalenessReason Reason;

        StalenessVerdict(StalenessState state, StalenessReason reason)
        {
            State = state;
            Reason = reason;
        }

        public bool IsUpToDate => State == StalenessState.UpToDate;

        public static StalenessVerdict UpToDate() => new StalenessVerdict(StalenessState.UpToDate, StalenessReason.None);
        public static StalenessVerdict Stale(StalenessReason reason) => new StalenessVerdict(StalenessState.Stale, reason);
        public static StalenessVerdict Unknown(StalenessReason reason) => new StalenessVerdict(StalenessState.Unknown, reason);
    }

    class RemoteModuleStaleness
    {
        enum ScanResult
        {
            Current,
            Changed,
            NoDirectory,
            NotCovered,
            Failed
        }

        readonly ILastSuccessfulDeploymentStore m_DeploymentStore;
#if UNITY_6000_5_OR_NEWER
        readonly IModuleContentHasher m_ContentHasher;
#endif

        public RemoteModuleStaleness(
            ILastSuccessfulDeploymentStore deploymentStore
#if UNITY_6000_5_OR_NEWER
            , IModuleContentHasher contentHasher
#endif
        )
        {
            m_DeploymentStore = deploymentStore;
#if UNITY_6000_5_OR_NEWER
            m_ContentHasher = contentHasher;
#endif
        }

        public async Task<StalenessVerdict> EvaluateAsync(CloudCodeModuleReference moduleReference)
        {
            var remote = m_DeploymentStore.GetRecord(moduleReference, DeploymentTarget.Remote);
            if (remote == null)
                return StalenessVerdict.Unknown(StalenessReason.NoRemoteRecord);

            var sourceDir = LocalAutoDeployer.GetSourceDir(moduleReference);
            if (sourceDir == null)
                return StalenessVerdict.Unknown(StalenessReason.NoSourceDirectory);

            var solutionPath = moduleReference.SolutionPath;
            var deployedFingerprint = remote.LastDeployedContentHash;
            var sourceSnapshotUtc = m_DeploymentStore.GetSourceLastWriteUtc(moduleReference, DeploymentTarget.Remote);
            var floorUtc = remote.DeployedAtUtc > sourceSnapshotUtc ? remote.DeployedAtUtc : sourceSnapshotUtc;

            var scan = await Task.Run(() => Scan(sourceDir, solutionPath, deployedFingerprint, floorUtc));
            switch (scan)
            {
                case ScanResult.Changed:
                    return StalenessVerdict.Stale(StalenessReason.SourceChangedOnDisk);
                case ScanResult.NoDirectory:
                    return StalenessVerdict.Unknown(StalenessReason.NoSourceDirectory);
                case ScanResult.NotCovered:
                    return StalenessVerdict.Unknown(StalenessReason.BuildInputsNotCovered);
                case ScanResult.Failed:
                    return StalenessVerdict.Unknown(StalenessReason.SourceScanFailed);
                default:
                    return StalenessVerdict.UpToDate();
            }
        }

        static ScanResult Scan(string sourceDir, string solutionPath, string deployedFingerprint, DateTime floorUtc)
        {
            try
            {
                if (!Directory.Exists(sourceDir))
                    return ScanResult.NoDirectory;

                var current = ModuleReferenceBuildInputs.Fingerprint(solutionPath);
                if (current == null)
                    return ScanResult.NotCovered;

                var changed = string.IsNullOrEmpty(deployedFingerprint)
                    ? LocalAutoDeployer.HasSourceNewerThan(sourceDir, floorUtc)
                    : !string.Equals(current, deployedFingerprint, StringComparison.Ordinal);
                return changed ? ScanResult.Changed : ScanResult.Current;
            }
            catch (Exception)
            {
                return ScanResult.Failed;
            }
        }

#if UNITY_6000_5_OR_NEWER
        public async Task<StalenessVerdict> EvaluateAsync(CloudCodeModule module)
        {
            var remote = m_DeploymentStore.GetRecord(module, DeploymentTarget.Remote);
            if (remote == null)
                return StalenessVerdict.Unknown(StalenessReason.NoRemoteRecord);

            if (string.IsNullOrEmpty(remote.LastDeployedContentHash))
                return StalenessVerdict.Unknown(StalenessReason.NoRecordedHash);

            // Not the tracker's cached hash, which lags an edit made just before Play.
            var current = await m_ContentHasher.ComputeHashAsync(module);

            if (string.IsNullOrEmpty(current))
                return StalenessVerdict.Unknown(StalenessReason.CannotHash);

            return string.Equals(current, remote.LastDeployedContentHash, StringComparison.Ordinal)
                ? StalenessVerdict.UpToDate()
                : StalenessVerdict.Stale(StalenessReason.ModifiedSinceLastDeploy);
        }

#endif
    }
}
#endif
