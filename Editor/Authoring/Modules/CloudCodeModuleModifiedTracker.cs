using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Services.CloudCode.Editor.Shared.Assets;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;

namespace Unity.Services.CloudCode.Authoring.Editor.Modules
{
    class CloudCodeModuleModifiedTracker : IDisposable
    {
        static readonly string[] k_TrackedExtensions = { ".cs", ".asmdef" };

        readonly IEnumerable<CloudCodeModule> m_CloudCodeModules;
        readonly AssetPostprocessorProxy m_PostprocessorProxy;

        public CloudCodeModuleModifiedTracker(
            CloudCodeModuleCollection cloudCodeModules,
            AssetPostprocessorProxy assetPostprocessorProxy)
            : this((IEnumerable<CloudCodeModule>)cloudCodeModules, assetPostprocessorProxy)
        {
        }

        internal CloudCodeModuleModifiedTracker(
            IEnumerable<CloudCodeModule> cloudCodeModules,
            AssetPostprocessorProxy assetPostprocessorProxy)
        {
            m_CloudCodeModules = cloudCodeModules;
            m_PostprocessorProxy = assetPostprocessorProxy;
            m_PostprocessorProxy.AllAssetsPostprocessed += OnAllAssetsPostprocessed;
        }

        void OnAllAssetsPostprocessed(object sender, PostProcessEventArgs e)
        {
            var allPaths = e.ImportedAssetPaths
                .Concat(e.DeletedAssetPaths)
                .Concat(e.MovedAssetPaths)
                .Concat(e.MovedFromAssetPaths);

            foreach (var path in allPaths)
            {
                if (!IsTrackedExtension(path))
                    continue;

                MarkAffectedModules(path);
            }
        }

        void MarkAffectedModules(string changedPath)
        {
            var normalizedChanged = changedPath.Replace('\\', '/');

            foreach (var moduleRef in m_CloudCodeModules)
            {
                if (!IsUpToDate(moduleRef.Status))
                    continue;

                var asmdefDir = GetAsmdefDirectory(moduleRef);
                if (asmdefDir == null)
                    continue;

                if (normalizedChanged.StartsWith(asmdefDir + "/", StringComparison.Ordinal))
                {
                    moduleRef.Status = DeploymentStatus.ModifiedLocally;
                }
            }
        }

        internal virtual string GetAsmdefDirectory(CloudCodeModule moduleRef)
        {
            if (moduleRef.CloudAssemblyDefinition == null)
                return null;

            var asmdefPath = AssetDatabase.GetAssetPath(moduleRef.CloudAssemblyDefinition);
            if (string.IsNullOrEmpty(asmdefPath))
                return null;

            return Path.GetDirectoryName(asmdefPath)?.Replace('\\', '/');
        }

        static bool IsTrackedExtension(string path)
        {
            for (var i = 0; i < k_TrackedExtensions.Length; i++)
            {
                if (path.EndsWith(k_TrackedExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static bool IsUpToDate(DeploymentStatus status)
        {
            var upToDate = DeploymentStatus.UpToDate;
            return status.Message == upToDate.Message
                && status.MessageSeverity == upToDate.MessageSeverity;
        }

        public void Dispose()
        {
            m_PostprocessorProxy.AllAssetsPostprocessed -= OnAllAssetsPostprocessed;
        }
    }
}
