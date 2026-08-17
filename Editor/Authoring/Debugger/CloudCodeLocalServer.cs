#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
#if UNITY_EDITOR_WIN
using System.Net.NetworkInformation;
#endif
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Dotnet;
using Unity.Services.CloudCode.Authoring.Editor.Core.Model;
#if UNITY_6000_3_OR_NEWER
using Unity.Multiplayer.PlayMode;
#endif
using Unity.Services.CloudCode.Authoring.Editor.Debugger.Apis;
using Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Deployment.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Projects;
using Unity.Services.CloudCode.Authoring.Editor.Projects.Settings;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;
using Unity.Services.Core.Editor;
using Unity.Services.Core.Editor.Environments;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;
using MainThreadScheduler = Unity.Services.CloudCode.Authoring.Client.Scheduler;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;
using LocalCloudCodeServerStatus = Unity.Services.CloudCode.Authoring.Editor.Debugger.ICloudCodeLocalServer.LocalCloudCodeServerStatus;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    class CloudCodeLocalServer : ICloudCodeLocalServer
    {
        const int k_InvalidPID = -1;
        const string k_ServerUrl = "http://localhost";
        const int k_HealthCheckIntervalMs = 1000;
        const int k_MaxRetainedServerLogs = 5;

        // How often the startup wait re-checks whether the server process died while the health
        // check is still retrying.
        const int k_StartupExitPollMs = 250;

        // A launch failure's stderr is a full stack trace; the first frames identify it, the rest
        // only belong in the log file.
        const int k_MaxStartupErrorLines = 8;

        // Upper bound on waiting for stderr to reach EOF after the process exits. Delivery is
        // asynchronous, so the fatal line can still be in flight when the exit is observed; without
        // this wait the failure is occasionally reported as having produced no output at all.
        const int k_StartupErrorFlushMs = 2000;
        const string k_ServerLogSearchPattern = "server-*.log";
        const string K_ServerPidKey = "LOCAL_CLOUD_CODE_PID";
        const string K_ServerStatus = "LOCAL_CLOUD_CODE_STATUS";
        const string K_ServerFailure = "LOCAL_CLOUD_CODE_FAILURE";
        static readonly string k_CloudCodeLocalStatePath = PathUtils.Join("Orleans", "GrainState", "v1");

        // The level the server process is launched with. Verbose logging is a scripting define, so
        // this is fixed at compile time: enabling it recompiles, and the server picks the new level
        // up the next time it starts.
        const LocalServerLogLevel k_ServerLogLevel =
            VerboseLogging.k_Enabled ? LocalServerLogLevel.Verbose : LocalServerLogLevel.Information;

        // Required dependencies
        readonly IEnvironmentsApi m_EnvironmentsApi;
        readonly ILogger m_Logger;
        readonly IProcessRunner m_ProcessRunner;
        readonly CloudCodeModuleReferenceLocalDeployCommand m_CloudCodeLocalDeployCommand;
        #if UNITY_6000_5_OR_NEWER
        readonly CloudCodeModuleDeployCommand m_CloudCodeModuleDeployCommand;
    #endif
            readonly EditorCloudCodeLocalModuleDeploymentHandler m_DeployHandler;
        internal IAccessTokens AccessTokens { get; set; }
        readonly ICloudCodePreferences m_Preferences;
        readonly IDotnetRunner m_DotnetRunner;
        readonly ICloudCodeLocalServerApi m_LocalServerClient;
        readonly CloudCodeModuleReferenceCollection m_CloudCodeModuleReferenceCollection;
#if UNITY_6000_5_OR_NEWER
        readonly CloudCodeModuleCollection m_CloudCodeModuleCollection;
#endif

        // Handling of Server status and states
        LocalCloudCodeServerStatus m_CurrentServerStatus;
        CancellationTokenSource m_CancellationTokenSource;
        int m_CurrentServerPid;
        string m_LastKnownFailure;
        LocalServerLogTailer m_LogTailer;

        // stderr arrives on a background thread while the launching thread waits on the health check.
        readonly ConcurrentQueue<string> m_StartupErrors = new ConcurrentQueue<string>();

        // The command line the server process was last launched with, for tests to assert against.
        internal string LastServerLaunchArguments { get; private set; }
        public event EventHandler<LocalCloudCodeServerStatus> OnServerStatusChanged;

        CloudCodeLocalServerSettings m_CloudCodeLocalServerSettings = null;

        internal CloudCodeLocalServerSettings CloudCodeLocalServerSettings
        {
            get
            {
                if (m_CloudCodeLocalServerSettings == null)
                {
                    m_CloudCodeLocalServerSettings = CloudCodeLocalServerSettings.GetOrCreate();
                }
                return m_CloudCodeLocalServerSettings;
            }
        }

#if UNITY_6000_5_OR_NEWER
        internal CloudCodeLocalServer(
            ILogger logger,
            IProcessRunner processRunner,
            CloudCodeModuleReferenceLocalDeployCommand cloudCodeLocalDeployCommand,
            CloudCodeModuleDeployCommand cloudCodeModuleDeployCommand,
            EditorCloudCodeLocalModuleDeploymentHandler deployHandler,
            IEnvironmentsApi environmentsApi,
            IAccessTokens accessTokens,
            ICloudCodePreferences preferences,
            IDotnetRunner dotnetRunner,
            CloudCodeModuleReferenceCollection cloudCodeModuleReferenceCollection,
            CloudCodeModuleCollection cloudCodeModuleCollection)
        {
            AccessTokens = accessTokens;
            m_Logger = logger;
            m_ProcessRunner = processRunner;
            m_CloudCodeLocalDeployCommand = cloudCodeLocalDeployCommand;
            m_CloudCodeModuleDeployCommand = cloudCodeModuleDeployCommand;
            m_DeployHandler = deployHandler;
            m_EnvironmentsApi = environmentsApi;
            m_CancellationTokenSource = new CancellationTokenSource();
            m_Preferences = preferences;
            m_DotnetRunner = dotnetRunner;
            m_CloudCodeModuleReferenceCollection = cloudCodeModuleReferenceCollection;
            m_CloudCodeModuleCollection = cloudCodeModuleCollection;

            // Local debug server client setup with the current port configuration
            var endpoint = $"{k_ServerUrl}:{GetPort()}";
            m_LocalServerClient = new CloudCodeLocalServerApi(endpoint, logger);

            Initialize();
        }

#else
        internal CloudCodeLocalServer(
            ILogger logger,
            IProcessRunner processRunner,
            CloudCodeModuleReferenceLocalDeployCommand cloudCodeLocalDeployCommand,
            EditorCloudCodeLocalModuleDeploymentHandler deployHandler,
            IEnvironmentsApi environmentsApi,
            IAccessTokens accessTokens,
            ICloudCodePreferences preferences,
            IDotnetRunner dotnetRunner,
            CloudCodeModuleReferenceCollection cloudCodeModuleReferenceCollection)
        {
            AccessTokens = accessTokens;
            m_Logger = logger;
            m_ProcessRunner = processRunner;
            m_CloudCodeLocalDeployCommand = cloudCodeLocalDeployCommand;
            m_DeployHandler = deployHandler;
            m_EnvironmentsApi = environmentsApi;
            m_CancellationTokenSource = new CancellationTokenSource();
            m_Preferences = preferences;
            m_DotnetRunner = dotnetRunner;
            m_CloudCodeModuleReferenceCollection = cloudCodeModuleReferenceCollection;

            // Local debug server client setup with the current port configuration
            var endpoint = $"{k_ServerUrl}:{GetPort()}";
            m_LocalServerClient = new CloudCodeLocalServerApi(endpoint, logger);

            Initialize();
        }

#endif

        void Initialize()
        {
            m_LogTailer = new LocalServerLogTailer(m_Logger);
            m_CurrentServerPid = EditorPrefs.GetInt(K_ServerPidKey, k_InvalidPID);
            m_CancellationTokenSource = new CancellationTokenSource();

            m_LastKnownFailure = EditorPrefs.GetString(K_ServerFailure);
            m_LastKnownFailure = string.IsNullOrEmpty(m_LastKnownFailure) ? null : m_LastKnownFailure;

            if (!Enum.TryParse(EditorPrefs.GetString(K_ServerStatus), true, out m_CurrentServerStatus))
                m_CurrentServerStatus = LocalCloudCodeServerStatus.Idle;

            OnApplicationRestore();

            // Ensure all servers are stopped when the application quits
            EditorApplication.quitting += OnApplicationQuit;
        }

        void OnApplicationRestore()
        {
#if MPPM_API_AVAILABLE_V2_0_OR_NEWER && UNITY_6000_3_OR_NEWER
            // TODO - Remove and Implement proper disabling of Local Server once UUM-131667 is fixed.
            // Ideally we should only register the Local server within Authoring Services in the Main Editor.
            // However, an inhibiting MPPM bug prevents CurrentPlayer API access within InitializeOnLoad
            // triggered by Authoring Services for startup singletons and Toolbar Bootstraps. As such,
            // this code temporary mitigates the issue by doing this check after InitializeOnLoad and
            // performing restoration if needed.
            EditorApplication.delayCall += () =>
            {
                if (CurrentPlayer.IsMainEditor)
                    RestoreLocalServer(m_CancellationTokenSource.Token);
            };
#else
            RestoreLocalServer(m_CancellationTokenSource.Token);
#endif
        }

        void OnApplicationQuit()
        {
            // Sanity check
            if (m_CurrentServerStatus == LocalCloudCodeServerStatus.Idle &&
                m_CurrentServerPid == k_InvalidPID)
                return;

            // The server can be in any state at Unity shutdown.
            // Stop any ongoing tasks, attempt stop, else force stop the server to reset state.
            // Note: Avoid hanging the quit process with long running tasks.
            try
            {
                // Attempt a graceful shutdown within time limit
                Task.Run(RequestShutdownAndCheck).Wait(TimeSpan.FromSeconds(2d));
            }
            catch (Exception)
            {
                // No-op.
            }
            finally
            {
                // Always safely stop all operations.
                // This becomes a no-op if the server is already gracefully terminated.
                ForceStopLocalServerSafe();
            }
        }

        public ushort GetPort()
        {
            return CloudCodeLocalServerSettings.Port;
        }

        public void SetPort(ushort port)
        {
            CloudCodeLocalServerSettings.Port = port;
        }

        public TextAsset GetSecretsFile()
        {
            return CloudCodeLocalServerSettings.SecretsFile;
        }

        public void SetSecretsFile(TextAsset path)
        {
            CloudCodeLocalServerSettings.SecretsFile = path;
        }

        public int GetServerPid()
        {
            return m_CurrentServerPid;
        }

        public void ClearServerState()
        {
            var modulesPath = EditorCloudCodeLocalModuleDeploymentHandler.GetModuleDestinationDir();
            var serverStatePath = PathUtils.Join(modulesPath, k_CloudCodeLocalStatePath);

            try
            {
                if (Directory.Exists(serverStatePath))
                {
                    Directory.Delete(serverStatePath, true);
                }
            }
            catch (Exception e)
            {
                m_Logger.LogError($"Error when clearing local server state: {e.Message}");
            }
        }

        public LocalCloudCodeServerStatus GetCurrentServerStatus()
        {
            return m_CurrentServerStatus;
        }

        public string GetLastServerFailure()
        {
            return m_LastKnownFailure;
        }

        public async Task StartCompilationAndService(bool restore)
        {
            // Sanity check
            if (!restore && m_CurrentServerStatus != LocalCloudCodeServerStatus.Idle)
                return;

            SetAndTrackServerStatus(LocalCloudCodeServerStatus.Preparing);
            SetAndTrackServerFailure(null);

            try
            {
                m_CancellationTokenSource = new CancellationTokenSource();
                var cancelToken = m_CancellationTokenSource.Token;
                m_Logger.LogVerbose($"Connecting to new local server on port {GetPort()}");

                // IsDotnetAvailable repairs an invalid or empty configured path by falling back
                // to resolving "dotnet" from PATH and persisting the working value, so the
                // compile step and the server launch below both get a valid executable.
                var configuredDotnetPath = m_Preferences.DotnetPath;
                if (!await m_DotnetRunner.IsDotnetAvailable())
                {
                    var tried = string.IsNullOrWhiteSpace(configuredDotnetPath)
                        ? "'dotnet' from the system PATH"
                        : $"the configured path '{configuredDotnetPath}' and 'dotnet' from the system PATH";
                    throw new Exception($"Could not find a usable .NET SDK. Tried {tried}. " +
                        $"Set a valid .NET path at Preferences > Cloud Code > .NET Path.");
                }

                // Fail fast on the problems that would otherwise only show up as the server process
                // dying moments after launch, with nothing but a refused connection to go on.
                await EnsureRequiredPortsAvailable(cancelToken);
                await EnsureServerRuntimeAvailable(cancelToken);

                cancelToken.ThrowIfCancellationRequested();

                m_DeployHandler.UpdateDeployStatuses(GetAllModules(), "Queued", severity: SeverityLevel.Info, shouldLog: false);

                // Create a token should the user want to cancel mid-launch.
                // Generate the Deploy all modules (native and referenced) in preparation for Local CC Deploy
                var deployedLocation = await CompileAndDeployAllModules(cancelToken);

                cancelToken.ThrowIfCancellationRequested();

                // Now start the server pointed to the compiled module directories
                await StartLocalServer(deployedLocation, cancelToken);
            }
            catch (Exception e)
            {
                if (e is not OperationCanceledException)
                {
                    SetAndTrackServerFailure(e.Message);
                    m_Logger.LogError($"Local Server Start Failed. Error message: {e.Message}");
                }

                // If Generation or server start fails, enforce fallback.
                SetAndTrackServerStatus(LocalCloudCodeServerStatus.Idle);
            }
        }

        public async Task StopCompilationAndService()
        {
            // Sanity check
            if (m_CurrentServerStatus == LocalCloudCodeServerStatus.Stopping)
                return;

            // Clear CCMR status (no longer deployed)
            ClearDeploymentStatus();

            try
            {
                // Cancel any pending tasks
                if (!m_CancellationTokenSource.IsCancellationRequested)
                    m_CancellationTokenSource.Cancel();

                // Stop the service if it had started
                if (m_CurrentServerStatus == LocalCloudCodeServerStatus.Started)
                    await StopLocalServer();
            }
            catch (Exception e)
            {
                if (e is not OperationCanceledException)
                    m_Logger.LogError($"Local Server Start Failed. Error message: {e.Message}");

                // If Stopping fails, enforce fallback.
                SetAndTrackServerStatus(LocalCloudCodeServerStatus.Idle);
            }
        }

        #region ModuleGeneration

        // Generates and compiles modules in preparation for Local CC Server deployment
        async Task<string> CompileAndDeployAllModules(CancellationToken cancellationToken)
        {
            await m_EnvironmentsApi.RefreshAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var referencedModules = m_CloudCodeModuleReferenceCollection.ToList();
            var referencedModulesDir =
                await m_CloudCodeLocalDeployCommand.CompileAndDeployAsync(referencedModules, cancellationToken);

            // Abort early if a cancellation request was done.
            cancellationToken.ThrowIfCancellationRequested();

#if UNITY_6000_5_OR_NEWER
            var cloudCodeModules = m_CloudCodeModuleCollection.ToList();
            var cloudCodeModulesDir = await m_CloudCodeModuleDeployCommand.GenerateAndDeployToLocalAsync(cloudCodeModules, cancellationToken);

            // If we have native and reference modules, ensure they are deployed to the same location.
            if (!string.IsNullOrEmpty(referencedModulesDir) &&
                !string.IsNullOrEmpty(cloudCodeModulesDir) &&
                referencedModulesDir != cloudCodeModulesDir)
            {
                throw new Exception("Deployment Failure: Mismatched deployed locations for Cloud Code and Referenced modules.");
            }

            // Now start the server pointed to the compiled module directories
            return referencedModulesDir ?? cloudCodeModulesDir;
#else
            return referencedModulesDir;
#endif
        }

        List<IModuleItem> GetAllModules()
        {
            var referencedModules = m_CloudCodeModuleReferenceCollection.ToList();
            List<IModuleItem> allModuleItems = new List<IModuleItem>();
#if UNITY_6000_5_OR_NEWER
            var cloudCodeModules = m_CloudCodeModuleCollection.ToList();
            allModuleItems.AddRange(cloudCodeModules);
#endif
            allModuleItems.AddRange(referencedModules);
            return allModuleItems;
        }

        #endregion

        #region Local Server

        async Task StartLocalServer(string compiledModuleDir, CancellationToken cancellationToken)
        {
            SetAndTrackServerStatus(LocalCloudCodeServerStatus.Starting);

            // Start the Local Cloud Code Server process, point it to modules
            try
            {
                var compiledCloudCodeServerPath = GetLocalCloudCodeServerPath();
                var secretsFile = GetSecretsFile();
                var secretsPath = "";
                if (secretsFile != null)
                {
                    // Have to call GetDirectoryName() because GetAssetPath() returns a value relative to the parent directory of Application.dataPath
                    secretsPath = Path.GetFullPath(FileUtil.GetPhysicalPath(AssetDatabase.GetAssetPath(secretsFile)), Path.GetDirectoryName(Application.dataPath));
                }
                ;
                var port = GetPort();
                var logDir = PathUtils.Join(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "UnityCloudCode", "Logs");
                var logfile = PathUtils.Join(logDir, $"server-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");

                // Each run gets its own log file, kept on disk for post-mortem inspection. Prune the
                // oldest so they don't accumulate without bound.
                PruneServerLogs(logDir, k_MaxRetainedServerLogs);

                // Tail the server's log file into the Editor console, so log capture survives domain reloads.
                m_LogTailer.Start(logfile);
                var startInfo = new ProcessStartInfo()
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    FileName = m_Preferences.DotnetPath,
                    Arguments = $"\"{compiledCloudCodeServerPath}\" run" +
                        $" -p \"{compiledModuleDir}\"" +
                        $" --log-file \"{logfile}\"" +
                        $" --log-level {k_ServerLogLevel}" +
                        $" --port {port}" +
                        (string.IsNullOrEmpty(secretsPath) ? "" : $" -s \"{secretsPath}\"")
                };

                // Recorded so tests can assert on what the server was actually launched with. The log
                // line below is Verbose (it is long, and one per start), so it is not an observation
                // point anything can rely on.
                LastServerLaunchArguments = $"{startInfo.FileName} {startInfo.Arguments}";
                m_Logger.LogVerbose($"Starting local server with arguments {LastServerLaunchArguments}");

                startInfo.EnvironmentVariables["GATEWAY_JWT"] = await AccessTokens.GetServicesGatewayTokenAsync();

                // Re-check now that compilation is done; the ports may have been taken meanwhile.
                await EnsureRequiredPortsAvailable(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Reset the Client's configuration to point to the new port
                ((CloudCodeLocalServerApi)m_LocalServerClient).Configuration.BasePath = $"{k_ServerUrl}:{port}";
                EditorPrefs.SetInt("CLOUD_CODE_DEBUG_PORT", port);

                // Ongoing logs are surfaced by tailing the server's log file (which survives domain
                // reloads). stderr is captured only for the launch phase to surface early/fatal
                // crashes the server can't yet write to that file (missing runtime, bad dotnet path,
                // exceptions before its logger is configured). Disposing the handle when this method
                // returns ends the capture; the running process is re-acquired later by PID.
                while (m_StartupErrors.TryDequeue(out _)) {}
                var standardErrorEnd = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var process = m_ProcessRunner.RunAsyncFireAndForget(
                    startInfo, OnServerStartupError, () => standardErrorEnd.TrySetResult(true));
                SetAndTrackServerPid(process.Id);

                // If the user force cancels, abort
                cancellationToken.ThrowIfCancellationRequested();

                // Perform health checks until the Server is fully running, it dies, or we time out.
                await AwaitServerReady(process, standardErrorEnd.Task, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // The server came up, so anything on stderr was not fatal - but it is still the only
                // place that output exists, so surface it rather than dropping it on the floor.
                while (m_StartupErrors.TryDequeue(out var startupError))
                {
                    if (!string.IsNullOrWhiteSpace(startupError))
                        m_Logger.LogWarning($"[Local Server] {startupError}");
                }

                // The server started. This is the one line about it a user needs, so it is the
                // counterpart to the verbose "Connecting to ..." above: the server's own port
                // announcement is plumbing, and the state transition is traced by
                // SetAndTrackServerStatus.
                SetAndTrackServerStatus(LocalCloudCodeServerStatus.Started);
                _ = Task.Run(() => PeriodicHealthCheckTask(m_LocalServerClient, OnHealthCheckPingsFail, cancellationToken),
                    cancellationToken);
                m_Logger.LogInfo($"Connected to local server on port {GetPort()}");
            }
            catch (Exception e)
            {
                if (e is not OperationCanceledException)
                {
                    SetAndTrackServerFailure(e.Message);
                    SetDeployStatusWithState("Local Server Error", e.Message, SeverityLevel.Error);
                }

                // In an event of failure, ensure that any resources are stopped
                ForceStopLocalServerSafe();
                throw;
            }
        }

        async Task StopLocalServer()
        {
            SetAndTrackServerStatus(LocalCloudCodeServerStatus.Stopping);

            try
            {
                // Cancel any running tasks if not yet already
                if (!m_CancellationTokenSource.IsCancellationRequested)
                    m_CancellationTokenSource.Cancel();

                // Process Sanity check using the PID
                using var process = Process.GetProcessById(m_CurrentServerPid);
                if (process.HasExited)
                    throw new Exception($"Server has already stopped with exit code {process.ExitCode}.");

                // Perform graceful termination, wait for the server to gracefully stop
                // If the server had not yet stopped, force kill it.
                await RequestShutdownAndCheck();
                if (!process.HasExited)
                    throw new Exception("Server has failed to stop.");

                if (process.ExitCode != 0)
                    m_Logger.LogError($"Server has exited with an error ExitCode: {process.ExitCode}");

                m_LogTailer.Stop();

                SetAndTrackServerPid(k_InvalidPID);
                SetAndTrackServerStatus(LocalCloudCodeServerStatus.Idle);
                m_Logger.LogVerbose("Local Server has Stopped.");
            }
            catch (Exception e)
            {
                SetAndTrackServerFailure(e.Message);

                // In an event of failure, ensure that any resources are stopped
                ForceStopLocalServerSafe();
            }
        }

        async Task RequestShutdownAndCheck()
        {
            // Sanity check, return if no process tracked.
            if (m_CurrentServerPid == k_InvalidPID)
                return;

            // Sanity check, return if already exited.
            using var process = Process.GetProcessById(m_CurrentServerPid);
            if (process.HasExited)
                return;

            // Signal shutdown and check.
            var gracefulTimeoutSeconds = await m_LocalServerClient.Shutdown(CancellationToken.None);
            for (int i = 0; i < gracefulTimeoutSeconds.shutdowntimeoutSeconds; i++)
            {
                if (process.HasExited)
                    return;

                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        void ForceStopLocalServerSafe()
        {
            try
            {
                if (!m_CancellationTokenSource.IsCancellationRequested)
                    m_CancellationTokenSource.Cancel();

                // Kill the tracked PID if we have it
                if (m_CurrentServerPid != k_InvalidPID)
                    m_ProcessRunner.Stop(m_CurrentServerPid);
            }
            catch (Exception)
            {
                // Force stopping here. No-op.
            }
            finally
            {
                m_LogTailer.Stop();
                SetAndTrackServerPid(k_InvalidPID);
                SetAndTrackServerStatus(LocalCloudCodeServerStatus.Idle);
            }

            m_Logger.LogVerbose("Local Server has Force Stopped.");
        }

        void RestoreLocalServer(CancellationToken cancellationToken)
        {
            // Sanity check, No pid was set, no server was started.
            if (m_CurrentServerPid == k_InvalidPID &&
                m_CurrentServerStatus == LocalCloudCodeServerStatus.Idle &&
                m_LastKnownFailure == null)
            {
                return;
            }

            // Do not restore failures
            if (m_LastKnownFailure != null)
            {
                // In failure situations, always ensure Local CC server is restartable
                ForceStopLocalServerSafe();
                return;
            }

            // If restoring from a compilation stage, start from the beginning.
            if (m_CurrentServerPid == k_InvalidPID && m_CurrentServerStatus == LocalCloudCodeServerStatus.Preparing)
            {
                _ = StartCompilationAndService(true);
                return;
            }

            try
            {
                using var process = Process.GetProcessById(m_CurrentServerPid);
                if (process.HasExited)
                    throw new Exception("Local Server has Exited.");

                // At this point we have a PID, look at the current status.
                // If we were restoring to a stopping state, stop the server.
                if (m_CurrentServerStatus == LocalCloudCodeServerStatus.Stopping)
                {
                    m_Logger.LogVerbose("Local Server has Restored to a Stopping State.");
                    _ = StopLocalServer();
                    return;
                }

                // Resume tailing the server log file from the persisted offset.
                m_LogTailer.Restore();

                // Resume Health check.
                SetAndTrackServerStatus(LocalCloudCodeServerStatus.Started);
                _ = Task.Run(
                    () => PeriodicHealthCheckTask(m_LocalServerClient, OnHealthCheckPingsFail, cancellationToken),
                    cancellationToken);

                m_Logger.LogVerbose("Local Server has Restored to a Started State.");
            }
            catch (Exception e)
            {
                if (e is not OperationCanceledException && e is not ArgumentException)
                {
                    SetAndTrackServerFailure(e.Message);
                    SetDeployStatusWithState("Local Server Error", e.Message, SeverityLevel.Error);
                    m_Logger.LogError($"Local Server Restore Failed: {e}");
                }

                // In an event of failure, ensure that any resources are stopped
                ForceStopLocalServerSafe();
            }
        }

        string GetLocalCloudCodeServerPath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(GetType().Assembly);
            return PathUtils.Join(packageInfo.resolvedPath, "Editor", "CloudCodeDebugger~", "CloudCodeDebugger.dll");
        }

        // Keeps the most recent per-run server logs and deletes the rest, so they don't accumulate
        // without bound. Best-effort: any file that can't be enumerated or deleted is skipped.
        internal static void PruneServerLogs(string logDir, int maxRetained)
        {
            try
            {
                if (!Directory.Exists(logDir))
                    return;

                var logs = Directory.GetFiles(logDir, k_ServerLogSearchPattern);
                if (logs.Length <= maxRetained)
                    return;

                Array.Sort(logs, (a, b) => File.GetLastWriteTimeUtc(b).CompareTo(File.GetLastWriteTimeUtc(a)));
                for (var i = maxRetained; i < logs.Length; i++)
                {
                    try
                    {
                        File.Delete(logs[i]);
                    }
                    catch (Exception)
                    {
                        // A locked/removed file is fine to skip; it'll be pruned on a later run.
                    }
                }
            }
            catch (Exception)
            {
                // Pruning is non-critical cleanup; never let it disrupt server startup.
            }
        }

        void OnHealthCheckPingsFail()
        {
            // Sanity check. If a late health check ping returns with a failure in the middle
            // of the process of stopping, filter that out.
            if (m_CurrentServerStatus == LocalCloudCodeServerStatus.Stopping)
                return;

            const string kHealthCheckFailedMessage = "Local server health check failed";
            SetAndTrackServerFailure(kHealthCheckFailedMessage);

            // Post successful server launch, we need to clear deployment status before updating with warning
            SetDeployStatusWithState("Local Server Offline ", kHealthCheckFailedMessage, SeverityLevel.Error);

            // Finally Force stop any pending processes
            ForceStopLocalServerSafe();
            m_Logger.LogError(kHealthCheckFailedMessage);
        }

        // Buffers the server process's stderr during launch. The server logs everything else to its
        // log file (tailed separately), so stderr only carries early/fatal output that never reaches
        // that file. It is held rather than logged line by line so that, when the process dies during
        // startup, the failure can be reported as one message that names the actual cause instead of
        // the refused connection that follows it.
        void OnServerStartupError(string line)
        {
            m_StartupErrors.Enqueue(line);
        }

        // Waits for the server to answer a health check, but gives up the moment the process dies —
        // otherwise a server that crashed on launch is reported as a refused connection, which says
        // nothing about why it crashed.
        async Task AwaitServerReady(Process process, Task standardErrorEnd, CancellationToken cancellationToken)
        {
            var healthCheck = m_LocalServerClient.HealthCheck(cancellationToken);

            // If we abandon the health check below, its failure must still be observed.
            _ = healthCheck.ContinueWith(
                t => _ = t.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);

            while (true)
            {
                var finished = await Task.WhenAny(healthCheck, Task.Delay(k_StartupExitPollMs, cancellationToken));

                if (process.HasExited)
                {
                    // stderr is delivered asynchronously, so the fatal line the process wrote on its
                    // way out can still be in flight when the exit is observed. Wait for the reader
                    // to reach EOF - bounded, so a pipe that never closes cannot wedge the start -
                    // before concluding anything about what it did or didn't write.
                    await Task.WhenAny(standardErrorEnd, Task.Delay(k_StartupErrorFlushMs));
                    throw new Exception(BuildStartupExitMessage(process));
                }

                // WhenAny completes rather than throws when the poll delay is cancelled, so the
                // cancellation has to be acted on here or the loop spins.
                cancellationToken.ThrowIfCancellationRequested();

                if (finished == healthCheck)
                {
                    // Surfaces a genuine health-check failure against a server that is still alive.
                    await healthCheck;
                    return;
                }
            }
        }

        // Describes a server that exited during startup, leading with whatever it wrote to stderr —
        // that first line is the real cause (a missing runtime, a port conflict, a bad argument).
        string BuildStartupExitMessage(Process process)
        {
            var errorLines = new List<string>();
            while (m_StartupErrors.TryDequeue(out var line))
            {
                if (!string.IsNullOrWhiteSpace(line))
                    errorLines.Add(line.Trim());
            }

            var exitCode = TryGetExitCode(process);
            var summary = exitCode.HasValue
                ? $"The local Cloud Code server exited during startup (exit code {exitCode.Value})."
                : "The local Cloud Code server exited during startup.";

            if (errorLines.Count == 0)
            {
                return $"{summary} It wrote no error output; see the server log for details.";
            }

            var detail = string.Join("\n", errorLines.Take(k_MaxStartupErrorLines));
            if (errorLines.Count > k_MaxStartupErrorLines)
                detail += $"\n... ({errorLines.Count - k_MaxStartupErrorLines} more lines)";

            return $"{summary}\n{detail}";
        }

        static int? TryGetExitCode(Process process)
        {
            try
            {
                return process.ExitCode;
            }
            catch (Exception)
            {
                // The handle can be gone by the time we ask; the stderr detail still stands on its own.
                return null;
            }
        }

        void SetAndTrackServerPid(int value)
        {
            EditorPrefs.SetInt(K_ServerPidKey, value);
            m_CurrentServerPid = value;
            m_Logger.LogVerbose($"Local Server tracked with PID: {value}");
        }

        void SetAndTrackServerStatus(LocalCloudCodeServerStatus value)
        {
            EditorPrefs.SetString(K_ServerStatus, value.ToString());
            m_CurrentServerStatus = value;
            m_Logger.LogVerbose($"Local Server tracked with State: {value}");
            OnServerStatusChanged?.Invoke(this, value);
        }

        void SetAndTrackServerFailure(string value)
        {
            EditorPrefs.SetString(K_ServerFailure, value);
            m_LastKnownFailure = value;

            if (value != null)
                m_Logger.LogVerbose($"Local Server tracked with Failure: {value}");
        }

        static async Task PeriodicHealthCheckTask(ICloudCodeLocalServerApi client, Action onFail, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await client.HealthCheck(cancellationToken);
                    await Task.Delay(k_HealthCheckIntervalMs, cancellationToken);
                }
            }
            catch (Exception e)
            {
                // Ignore if cancelled, else continue assuming health check failed
                if (e is OperationCanceledException)
                    return;
            }

            // Else a health check failure may be an indication where:
            // 1 - The Server hanged, but process is running
            // 2 - The Server process suddenly stopped without warning.
            // 3 - Network issues prevent us from communicating with the server.
            // Regardless, we need to properly force terminate to enable users to retry.
            await Task.Factory.StartNew(onFail, CancellationToken.None, TaskCreationOptions.None,
                MainThreadScheduler.ThreadHelper.TaskScheduler);
        }

        // Checks every port the server needs, not just the configured one. The Orleans ports are
        // fixed inside the server, so a conflict there is invisible to the user: their chosen port
        // is free, the process starts, and then dies binding a port they were never told about.
        async Task EnsureRequiredPortsAvailable(CancellationToken cancellationToken)
        {
            var port = GetPort();
            if (!await IsPortAvailable(port, cancellationToken))
                throw new Exception($"Server Port {port} is not available. " +
                    "Choose a different port in the Cloud Code Local Server Settings asset.");
        }

        static string UnconfigurablePortMessage(int port) =>
            $"Port {port} is already in use. The local Cloud Code server needs it for its internal " +
            "clustering, and it cannot be reconfigured. Stop whatever is holding the port and try again.";

        // The server ships as a framework-dependent build, so the .NET runtimes named in its
        // runtimeconfig.json must be installed. Without this check a missing runtime shows up only
        // as the process exiting immediately, and the required version moves whenever the server is
        // rebuilt against a different target framework - so it is read from the server itself rather
        // than hardcoded here.
        async Task EnsureServerRuntimeAvailable(CancellationToken cancellationToken)
        {
            List<(string name, Version version)> required;
            try
            {
                required = ReadRequiredFrameworks(GetLocalCloudCodeServerPath());
            }
            catch (Exception e)
            {
                // A check we cannot perform must not block a server that would have started fine.
                m_Logger.LogVerbose($"Could not determine the local server's required .NET runtimes: {e.Message}");
                return;
            }

            foreach (var(name, version) in required)
            {
                List<SemVersion> installed;
                try
                {
                    installed = await m_DotnetRunner.GetAvailableRuntimes(name, cancellationToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    m_Logger.LogVerbose($"Could not list installed {name} runtimes: {e.Message}");
                    return;
                }

                if (installed.Any(v => SatisfiesFrameworkRequirement(v?.Version, version)))
                    continue;

                var found = installed.Count == 0
                    ? "none are installed"
                    : $"found {string.Join(", ", installed.Where(v => v != null).Select(v => v.Version.ToString()))}";
                // The ASP.NET Core Runtime carries both frameworks the server declares, so it is the
                // right remedy whichever one is missing - the plain .NET Runtime download does not
                // include Microsoft.AspNetCore.App.
                throw new Exception(
                    $"The local Cloud Code server requires the {name} {version.Major}.{version.Minor} runtime, but {found}. " +
                    $"Install the ASP.NET Core Runtime {version.Major}.{version.Minor}, or the .NET SDK, from " +
                    "https://dotnet.microsoft.com/download/dotnet and restart the Editor.");
            }
        }

        static List<(string name, Version version)> ReadRequiredFrameworks(string serverDllPath)
        {
            var configPath = Path.ChangeExtension(serverDllPath, ".runtimeconfig.json");
            var config = JObject.Parse(File.ReadAllText(configPath));
            var options = config["runtimeOptions"];

            // A single-framework app declares "framework"; a web app lists several under "frameworks".
            var declared = new List<JToken>();
            if (options ? ["frameworks"] is JArray many)
                declared.AddRange(many);
            if (options ? ["framework"] is JObject single)
                declared.Add(single);

            var required = new List<(string, Version)>();
            foreach (var framework in declared)
            {
                var name = framework.Value<string>("name");
                if (string.IsNullOrEmpty(name) || !Version.TryParse(framework.Value<string>("version"), out var version))
                    continue;
                required.Add((name, version));
            }

            return required;
        }

        // Mirrors the host's default roll-forward policy: a higher patch or minor of the same major
        // satisfies the requirement, a different major does not.
        internal static bool SatisfiesFrameworkRequirement(Version installed, Version required)
        {
            if (installed == null || required == null)
                return false;
            if (installed.Major != required.Major)
                return false;
            if (installed.Minor != required.Minor)
                return installed.Minor > required.Minor;
            return installed.Build >= required.Build;
        }

        async Task<bool> IsPortAvailable(int port, CancellationToken cancellationToken = default)
        {
#if UNITY_EDITOR_WIN
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

            foreach (TcpConnectionInformation tcpi in tcpConnInfoArray)
            {
                if (tcpi.LocalEndPoint.Port == port && tcpi.State == TcpState.Listen)
                {
                    return await Task.FromResult(false);
                }
            }

            return await Task.FromResult(true);
#else
            var lsofStartInfo = new ProcessStartInfo
            {
                FileName = "lsof",
                Arguments = $"-i :{port} -s TCP:LISTEN",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            var lsofProcess = await m_ProcessRunner.RunAsync(lsofStartInfo, cancellationToken : cancellationToken);
            return lsofProcess.ExitCode == 1;
#endif
        }

        #endregion

        #region Deployment Status Helper

        void SetDeployStatusWithState(string message, string messageDetail, SeverityLevel messageSeverity)
        {
            var ccms = GetAllModules();
            m_DeployHandler.SetDeployStatusesWithState(ccms, message, messageDetail, messageSeverity);
        }

        void ClearDeploymentStatus()
        {
            var ccms = GetAllModules();
            m_DeployHandler.ClearDeploymentStatuses(ccms);
        }

        #endregion
    }
}
#endif
