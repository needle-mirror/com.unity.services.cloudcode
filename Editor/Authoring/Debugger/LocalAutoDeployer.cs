#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
using Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
#if MPPM_API_AVAILABLE_V2_0_OR_NEWER
using Unity.Multiplayer.PlayMode;
#endif
#if UNITY_6000_5_OR_NEWER
using Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules;
#endif
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

using Unity.Services.CloudCode.Authoring.Editor.Core.Analytics;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    /// <summary>
    /// Redeploys modules onto the already running local server when their source has changed, so picking
    /// up a code change does not require stopping and starting it. The server reloads a module whose zip
    /// is newer than the copy it cached, so overwriting that zip is the whole mechanism - this only
    /// decides when to run the same deploy the Deployment Window runs.
    /// </summary>
    class LocalAutoDeployer
    {
        // Source kinds that can change what a module reference compiles to - a .NET project outside the
        // Assets tree. The project's own build output is excluded: it changes as a result of the deploy,
        // which would make every deploy look like a new change. Assembly-definition modules are authored in
        // the Assets tree instead and track a different set, see TrackedModuleSourcePatterns.
        static readonly string[] k_TrackedSourcePatterns = { "*.cs", "*.csproj", "*.sln", "*.props", "*.targets" };
        static readonly string[] k_IgnoredDirectories = { "bin", "obj" };

        readonly ICloudCodeLocalServer m_LocalServer;
        readonly IEnumerable<CloudCodeModuleReference> m_ModuleReferences;
        readonly CloudCodeModuleReferenceLocalDeployCommand m_ModuleReferenceDeployCommand;
        readonly IModuleProjectRetriever m_ProjectRetriever;
        readonly IEnvironmentsApi m_EnvironmentsApi;
        readonly ILogger m_Logger;
#if UNITY_6000_5_OR_NEWER
        readonly IEnumerable<CloudCodeModule> m_Modules;
        readonly CloudCodeModuleDeployCommand m_ModuleDeployCommand;
#endif

        Task m_InFlightDeploy;
#if UNITY_6000_5_OR_NEWER
        bool m_PlayModeChangeReported;
#endif

        public LocalAutoDeployer(
            ICloudCodeLocalServer localServer,
            CloudCodeModuleReferenceCollection moduleReferences,
            CloudCodeModuleReferenceLocalDeployCommand moduleReferenceDeployCommand,
            IModuleProjectRetriever projectRetriever,
#if UNITY_6000_5_OR_NEWER
            CloudCodeModuleCollection modules,
            CloudCodeModuleDeployCommand moduleDeployCommand,
#endif
            IEnvironmentsApi environmentsApi,
            ILogger logger)
            : this(
                localServer,
                (IEnumerable<CloudCodeModuleReference>)moduleReferences,
                moduleReferenceDeployCommand,
                projectRetriever,
#if UNITY_6000_5_OR_NEWER
                (IEnumerable<CloudCodeModule>)modules,
                moduleDeployCommand,
#endif
                environmentsApi,
                logger)
        {
            Initialize();
        }

        internal LocalAutoDeployer(
            ICloudCodeLocalServer localServer,
            IEnumerable<CloudCodeModuleReference> moduleReferences,
            CloudCodeModuleReferenceLocalDeployCommand moduleReferenceDeployCommand,
            IModuleProjectRetriever projectRetriever,
#if UNITY_6000_5_OR_NEWER
            IEnumerable<CloudCodeModule> modules,
            CloudCodeModuleDeployCommand moduleDeployCommand,
#endif
            IEnvironmentsApi environmentsApi,
            ILogger logger)
        {
            m_LocalServer = localServer;
            m_ModuleReferences = moduleReferences;
            m_ModuleReferenceDeployCommand = moduleReferenceDeployCommand;
            m_ProjectRetriever = projectRetriever;
#if UNITY_6000_5_OR_NEWER
            m_Modules = modules;
            m_ModuleDeployCommand = moduleDeployCommand;
#endif
            m_EnvironmentsApi = environmentsApi;
            m_Logger = logger;
        }

        void Initialize()
        {
            // A module reference is edited in an external IDE, which produces no asset event at all, so
            // regaining focus is the only signal that anything may have changed. The deferred call covers
            // the domain reload this instance was constructed by - the server's status is restored during
            // that same reload, so checking immediately could read a stale Idle.
            EditorApplication.focusChanged += OnFocusChanged;
            ScheduleCheck();
        }

        void OnFocusChanged(bool hasFocus)
        {
            if (hasFocus)
                ScheduleCheck();
        }

        void ScheduleCheck()
        {
            EditorApplication.delayCall += () => _ = DeployChangedModules();
        }

        async Task DeployChangedModules()
        {
            if (!ShouldCheck())
                return;

            try
            {
                await DeployChangedModulesAsync(CancellationToken.None);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // The deploy commands already report failures on the module's own status; logging at
                // verbose keeps a module that fails to compile from filling the Console on every focus.
                m_Logger.LogVerbose($"Local auto deploy failed: {e.Message}");
            }
        }

        bool ShouldCheck()
        {
            return IsMainEditor()
                && m_LocalServer.GetCurrentServerStatus() == LocalCloudCodeServerStatus.Started
                && m_EnvironmentsApi.ActiveEnvironmentId != null;
        }

        /// <summary>
        /// Redeploys every module whose source changed since it was deployed to the running server, and
        /// throws when that cannot be done. A check already in flight is joined rather than started again.
        /// Cancelling ends only this caller's wait. The shared check runs to completion.
        /// </summary>
        internal Task DeployChangedModulesAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled(cancellationToken);

            if (m_InFlightDeploy is not { IsCompleted: false })
            {
                m_InFlightDeploy = DeployChangedModulesCore(CancellationToken.None);
                _ = ObserveFailure(m_InFlightDeploy);
            }

            return WaitCancellable(m_InFlightDeploy, cancellationToken);
        }

        static async Task ObserveFailure(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Once every caller has stopped waiting nobody observes a failure, and the deploy commands
                // already report it on the module.
            }
        }

        internal static async Task WaitCancellable(Task task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            {
                await task.ConfigureAwait(false);
                return;
            }

            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancelled.TrySetCanceled(cancellationToken)))
            {
                var first = await Task.WhenAny(task, cancelled.Task).ConfigureAwait(false);
                await first.ConfigureAwait(false);
            }
        }

        async Task DeployChangedModulesCore(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (m_EnvironmentsApi.ActiveEnvironmentId == null)
            {
                throw new InvalidOperationException(
                    "Cloud Code: no active environment is selected. Select one in Project Settings > Services > Environments.");
            }

            // An asmdef module deploys the assembly Unity compiles for it, so a deploy started while
            // Unity is still importing or compiling would compare against, or package, a stale DLL.
            await WaitUntilEditorSettled(cancellationToken);

            var deployedDir = Path.Combine(
                EditorCloudCodeLocalModuleDeploymentHandler.GetModuleDestinationDir(),
                m_EnvironmentsApi.ActiveEnvironmentId.ToString());

            await DeployChangedModuleReferences(deployedDir, cancellationToken);
#if UNITY_6000_5_OR_NEWER
            await DeployChangedCloudCodeModules(deployedDir, cancellationToken);
#endif
        }

        static async Task WaitUntilEditorSettled(CancellationToken cancellationToken)
        {
            if (IsEditorSettled())
                return;

            var settled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnUpdate()
            {
                if (IsEditorSettled())
                    settled.TrySetResult(true);
            }

            EditorApplication.update += OnUpdate;
            try
            {
                using (cancellationToken.Register(() => settled.TrySetCanceled(cancellationToken)))
                {
                    await settled.Task;
                }
            }
            finally
            {
                EditorApplication.update -= OnUpdate;
            }
        }

        static bool IsEditorSettled()
        {
            return !EditorApplication.isCompiling && !EditorApplication.isUpdating;
        }

        /// <summary>
        /// Modules whose most recent deploy attempt failed. The deploy commands skip a module that fails to
        /// compile rather than throwing, so this is how a caller finds out the server is running without it.
        /// </summary>
        internal IReadOnlyList<IDeploymentItem> GetModulesInErrorState()
        {
            var failed = new List<IDeploymentItem>();
            AddModulesInErrorState(m_ModuleReferences, failed);
#if UNITY_6000_5_OR_NEWER
            AddModulesInErrorState(m_Modules, failed);
#endif
            return failed;
        }

        static void AddModulesInErrorState(IEnumerable<IModuleItem> items, List<IDeploymentItem> failed)
        {
            foreach (var item in items)
            {
                if (item.Status.MessageSeverity == SeverityLevel.Error || HasErrorState(item))
                    failed.Add(item);
            }
        }

        // Some failures, such as an unsupported API compatibility level, are recorded only as a state.
        static bool HasErrorState(IDeploymentItem item)
        {
            foreach (var state in item.States)
            {
                if (state.Level == SeverityLevel.Error)
                    return true;
            }

            return false;
        }

        // Additional Editor instances read the same recorded server state as the main Editor, and two of
        // them building the same solution at once would collide.
        internal static bool IsMainEditor()
        {
#if MPPM_API_AVAILABLE_V2_0_OR_NEWER
            return CurrentPlayer.IsMainEditor;
#else
            return true;
#endif
        }

        async Task DeployChangedModuleReferences(string deployedDir, CancellationToken cancellationToken)
        {
            var changed = m_ModuleReferences
                .Where(ccmr => IsStale(GetSourceDir(ccmr), DeployedZipPath(deployedDir, ccmr)))
                .ToList();

            if (changed.Count == 0)
                return;

            await m_ModuleReferenceDeployCommand.CompileAndDeployAsync(changed, DeploymentOrigin.Automatic, cancellationToken);
            LogReloaded(changed);
        }

        /// <summary>
        /// Names what the running server has just picked up, so a redeploy is visible in the Console without
        /// having to call the module to find out. Items the deploy failed on carry an error status and are
        /// left out; the failure itself is already reported on the item.
        /// </summary>
        void LogReloaded(IEnumerable<IDeploymentItem> items)
        {
            var reloaded = items
                .Where(item => item.Status.MessageSeverity != SeverityLevel.Error)
                .Select(item => item.Name)
                .ToList();

            if (reloaded.Count == 0)
                return;

            m_Logger.LogInfo(
                $"Local Cloud Code server reloaded: {string.Join(", ", reloaded)}.");
        }

        internal static string GetSourceDir(CloudCodeModuleReference ccmr)
        {
            try
            {
                return Path.GetDirectoryName(ccmr.SolutionPath);
            }
            catch (Exception)
            {
                // An unset or malformed module path is already surfaced by the deploy itself.
                return null;
            }
        }

        /// <summary>
        /// The deployed zip is named after the solution's main entry project, so that name has to be
        /// resolved the same way the build resolves it. <see cref="CloudCodeModuleReference.ModuleName"/>
        /// holds it once a build has run, but it does not survive a domain reload. Returns null when the
        /// name cannot be determined, which leaves the module alone - a solution that cannot be read is
        /// reported by a real deploy, not by this check.
        /// </summary>
        string DeployedZipPath(string deployedDir, CloudCodeModuleReference ccmr)
        {
            try
            {
                var moduleName = string.IsNullOrEmpty(ccmr.ModuleName)
                    ? m_ProjectRetriever.GetMainEntryProjectName(ccmr.SolutionPath)
                    : ccmr.ModuleName;

                return Path.Combine(deployedDir, Path.GetFileNameWithoutExtension(moduleName) + ".zip");
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Compares a module reference's source against the copy deployed to the local server by
        /// timestamp. Timestamps rather than content hashes because a module reference records no
        /// fingerprint of what was last deployed, and the deployed zip is the only thing to compare
        /// against. A missing zip counts as stale so a module added while the server runs gets deployed.
        /// </summary>
        internal static bool IsStale(string sourceDir, string deployedZipPath, string[] trackedPatterns = null)
        {
            if (string.IsNullOrEmpty(sourceDir) || string.IsNullOrEmpty(deployedZipPath) || !Directory.Exists(sourceDir))
                return false;

            return IsNewerThanDeployed(
                EnumerateSourceFiles(sourceDir, trackedPatterns ?? k_TrackedSourcePatterns), deployedZipPath);
        }

        /// <summary>
        /// Same tracked-source scan as <see cref="IsStale"/>, but against a recorded
        /// deploy time instead of a deployed artifact's timestamp - for the remote case, where no local
        /// artifact exists to compare against.
        /// </summary>
        internal static bool HasSourceNewerThan(string sourceDir, DateTime deployedAtUtc, string[] trackedPatterns = null)
        {
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                return false;

            return EnumerateSourceFiles(sourceDir, trackedPatterns ?? k_TrackedSourcePatterns)
                .Any(file => File.Exists(file) && File.GetLastWriteTimeUtc(file) > deployedAtUtc);
        }

        /// <summary>
        /// Newest tracked-source mtime under the directory, in UTC; the default value when the
        /// directory is missing or holds no tracked files. Snapshotted at deploy time so the remote
        /// staleness check can ask "did the newest source file get newer since we deployed" instead of
        /// comparing mtimes against the wall clock.
        /// </summary>
        internal static DateTime SourceLastWriteUtc(string sourceDir, string[] trackedPatterns = null)
        {
            if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                return default;

            var newest = default(DateTime);
            foreach (var file in EnumerateSourceFiles(sourceDir, trackedPatterns ?? k_TrackedSourcePatterns))
            {
                if (!File.Exists(file))
                    continue;

                var lastWriteUtc = File.GetLastWriteTimeUtc(file);
                if (lastWriteUtc > newest)
                    newest = lastWriteUtc;
            }

            return newest;
        }

        internal static bool HasSourceNewerThanSafe(string sourceDir, DateTime deployedAtUtc)
        {
            try
            {
                return HasSourceNewerThan(sourceDir, deployedAtUtc);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The snapshot is best-effort like every other check here: a solution that cannot be read
        // is surfaced by a real deploy, and the default just falls back to the wall-clock-only staleness floor.
        internal static DateTime SourceLastWriteUtcFor(CloudCodeModuleReference ccmr)
        {
            try
            {
                return SourceLastWriteUtc(GetSourceDir(ccmr));
            }
            catch (Exception)
            {
                return default;
            }
        }

        internal static bool IsNewerThanDeployed(IEnumerable<string> sourceFiles, string deployedZipPath)
        {
            if (!File.Exists(deployedZipPath))
                return true;

            var deployedAt = File.GetLastWriteTimeUtc(deployedZipPath);
            return sourceFiles.Any(file => File.Exists(file) && File.GetLastWriteTimeUtc(file) > deployedAt);
        }

        static IEnumerable<string> EnumerateSourceFiles(string sourceDir, string[] trackedPatterns)
        {
            foreach (var pattern in trackedPatterns)
            {
                foreach (var file in Directory.EnumerateFiles(sourceDir, pattern, SearchOption.AllDirectories))
                {
                    if (!IsIgnored(file, sourceDir))
                        yield return file;
                }
            }
        }

        static bool IsIgnored(string file, string sourceDir)
        {
            var relative = file.Substring(sourceDir.Length);
            return relative
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(segment => k_IgnoredDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase));
        }

#if UNITY_6000_5_OR_NEWER
        async Task DeployChangedCloudCodeModules(string deployedDir, CancellationToken cancellationToken)
        {
            // A Cloud Code Module is deployed from the assembly Unity compiles for it, and that assembly
            // cannot be rebuilt reliably during play mode: depending on Script Changes While Playing, Unity
            // either does not compile at all, or reloads the domain and resets the running scene's
            // non-serialized state. Point the change out instead of acting on it.
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                ReportModulesChangedInPlayMode(deployedDir);
                return;
            }

            m_PlayModeChangeReported = false;

            if (EditorUtility.scriptCompilationFailed)
                return;

            var modified = m_Modules.Where(ccm => IsModuleStale(ccm, deployedDir)).ToList();
            if (modified.Count == 0)
                return;

            await m_ModuleDeployCommand.GenerateAndDeployToLocalAsync(modified, DeploymentOrigin.Automatic, cancellationToken);
            LogReloaded(modified);
        }

        /// <summary>
        /// Names the modules whose cloud source changed since the copy on the local server was deployed, so
        /// a change made in play mode is not silently ignored. Source timestamps rather than the compiled
        /// assembly, because the point is to report the edit Unity has not compiled yet. Reported once per
        /// play session, since the check runs on every focus change.
        /// </summary>
        void ReportModulesChangedInPlayMode(string deployedDir)
        {
            if (m_PlayModeChangeReported)
                return;

            var changed = m_Modules
                .Where(ccm => HasChangedCloudSource(ccm, deployedDir))
                .Select(ccm => ccm.name)
                .ToList();

            if (changed.Count == 0)
                return;

            m_PlayModeChangeReported = true;
            m_Logger.LogWarning(
                $"Cloud Code Module(s) {string.Join(", ", changed)} changed while in play mode and were not " +
                "deployed to the local server: Unity cannot rebuild them until play mode ends. Exit play mode " +
                "and enter it again to run the new code.");
        }

        // What an assembly-definition module is built from, taken from the hasher so the play-mode warning
        // and the module's content hash agree on which files count. An .asmdef is source here: editing one
        // changes the module's references, and Unity cannot recompile it during play mode either.
        internal static readonly string[] TrackedModuleSourcePatterns =
            ModuleContentHasher.TrackedExtensions.Select(extension => "*" + extension).ToArray();

        // Spans the module's whole assembly closure, not just its cloud directory: an edit to a shared
        // assembly changes what a redeploy would carry just as much as an edit to the module itself.
        static bool HasChangedCloudSource(CloudCodeModule ccm, string deployedDir)
        {
            if (ccm.CloudAssemblyDefinition == null)
                return false;

            var moduleName = CloudCodeModuleDeployCommand.GetDeployModuleName(ccm);
            var deployedZip = Path.Combine(deployedDir, moduleName + ".zip");

            return ModuleAssemblyClosure.ProjectSourceDirectories(ccm.CloudAssemblyDefinition)
                .Any(directory => IsStale(directory, deployedZip, TrackedModuleSourcePatterns));
        }

        /// <summary>
        /// What a Cloud Code Module deploys is the assemblies Unity compiled for it, not its source, so those
        /// assemblies are what decide whether a redeploy would carry anything new. Source that Unity has not
        /// recompiled yet leaves them untouched, and redeploying would reload the running module for no change
        /// at all.
        /// </summary>
        static bool IsModuleStale(CloudCodeModule ccm, string deployedDir)
        {
            var moduleName = CloudCodeModuleDeployCommand.GetDeployModuleName(ccm);
            var assemblyPaths = PackagedAssemblyPaths(ccm);
            if (assemblyPaths.Count == 0)
                return false;

            return IsNewerThanDeployed(assemblyPaths, Path.Combine(deployedDir, moduleName + ".zip"));
        }

        /// <summary>
        /// The assemblies the deploy packages into the .ccm — the module's own plus everything its asmdef
        /// references, precompiled ones included — resolved through the same cache the deploy resolves them
        /// with, so this sees exactly the files a redeploy would upload. A shared assembly rebuilt on its own,
        /// or a precompiled DLL reimported on its own, still counts as a change.
        /// </summary>
        static List<string> PackagedAssemblyPaths(CloudCodeModule ccm)
        {
            if (ccm.CloudAssemblyDefinition == null)
                return new List<string>();

            var references = new HashSet<string>();
            try
            {
                ModuleAssemblyClosure.CollectAssemblyNames(ccm.CloudAssemblyDefinition, references);
            }
            catch (Exception)
            {
                // A malformed asmdef is surfaced by a real deploy, not by this check.
                return new List<string>();
            }

            var assemblyCache = CloudCodeModuleDeployCommand.BuildAssemblyPathCache();
            return references
                .Where(assemblyCache.ContainsKey)
                .Select(reference => assemblyCache[reference])
                .ToList();
        }

#endif
    }
}
#endif
