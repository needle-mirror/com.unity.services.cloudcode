using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if UNITY_EDITOR_WIN
using System.Net.NetworkInformation;
#endif
using System.Threading;
using System.Threading.Tasks;
#if UNITY_6000_3_OR_NEWER
using Unity.Multiplayer.PlayMode;
#endif
using Unity.Services.CloudCode.Authoring.Editor.Debugger.Apis;
using Unity.Services.CloudCode.Authoring.Editor.Debugger.Deployment;
using Unity.Services.CloudCode.Authoring.Editor.Modules;
using Unity.Services.CloudCode.Authoring.Editor.Projects;
using Unity.Services.CloudCode.Authoring.Editor.Projects.Settings;
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
        const string K_ServerPidKey = "LOCAL_CLOUD_CODE_PID";
        const string K_ServerStatus = "LOCAL_CLOUD_CODE_STATUS";
        const string K_ServerFailure = "LOCAL_CLOUD_CODE_FAILURE";

        // Required dependencies
        readonly IEnvironmentsApi m_EnvironmentsApi;
        readonly ILogger m_Logger;
        readonly IProcessRunner m_ProcessRunner;
        readonly CloudCodeLocalModuleDeployCommand m_CloudCodeLocalDeployCommand;
        readonly EditorCloudCodeLocalModuleDeploymentHandler m_DeployHandler;
        readonly IAccessTokens m_AccessTokens;
        readonly ICloudCodePreferences m_Preferences;
        readonly ICloudCodeLocalServerApi m_LocalServerClient;
        readonly CloudCodeModuleReferenceCollection m_CloudCodeModuleReferenceCollection;

        // Handling of Server status and states
        LocalCloudCodeServerStatus m_CurrentServerStatus;
        CancellationTokenSource m_CancellationTokenSource;
        int m_CurrentServerPid;
        string m_LastKnownFailure;
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

        internal CloudCodeLocalServer(
            ILogger logger,
            IProcessRunner processRunner,
            CloudCodeLocalModuleDeployCommand cloudCodeLocalDeployCommand,
            EditorCloudCodeLocalModuleDeploymentHandler deployHandler,
            IEnvironmentsApi environmentsApi,
            IAccessTokens mAccessTokens,
            ICloudCodePreferences preferences,
            CloudCodeModuleReferenceCollection cloudCodeModuleReferenceCollection)
        {
            m_AccessTokens = mAccessTokens;
            m_Logger = logger;
            m_ProcessRunner = processRunner;
            m_CloudCodeLocalDeployCommand = cloudCodeLocalDeployCommand;
            m_DeployHandler = deployHandler;
            m_EnvironmentsApi = environmentsApi;
            m_CancellationTokenSource = new CancellationTokenSource();
            m_Preferences = preferences;
            m_CloudCodeModuleReferenceCollection = cloudCodeModuleReferenceCollection;

            // Local debug server client setup with the current port configuration
            var endpoint = $"{k_ServerUrl}:{GetPort()}";
            m_LocalServerClient = new CloudCodeLocalServerApi(endpoint, logger);

            Initialize();
        }

        void Initialize()
        {
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
                m_Logger.LogInfo($"Connecting to new local server on port {GetPort()}");

                // Fail fast if selected port is in use.
                if (!await IsPortAvailable(GetPort(), cancelToken))
                    throw new Exception($"Server Port {GetPort()} is not available.");

                cancelToken.ThrowIfCancellationRequested();

                // Create a token should the user want to cancel mid-launch.
                // Generate the Compile modules in preparation for Local CC Deploy
                var compiledModuleDir = await GenerateAndCompileAllModules(cancelToken);

                cancelToken.ThrowIfCancellationRequested();

                // Now start the server pointed to the compiled module directories
                await StartLocalServer(compiledModuleDir, cancelToken);
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
        async Task<string> GenerateAndCompileAllModules(CancellationToken cancellationToken)
        {
            await m_EnvironmentsApi.RefreshAsync();
            var ccmrs = m_CloudCodeModuleReferenceCollection.ToList();

            cancellationToken.ThrowIfCancellationRequested();

            return await m_CloudCodeLocalDeployCommand.CompileAndDeployAsync(ccmrs, cancellationToken);
        }

#endregion

#region Local Server

    async Task StartLocalServer(string compiledModuleDir, CancellationToken cancellationToken)
    {
        SetAndTrackServerStatus(LocalCloudCodeServerStatus.Starting);

        // Start the Local Cloud Code Server process, point it to modules
        try
        {
            // TODO MTT-13965: Perform Logging Initialization before process start.

            var compiledCloudCodeServerPath = GetLocalCloudCodeServerPath();
            var secretsFile = GetSecretsFile();
            var secretsPath = "";
            if(secretsFile != null)
            {
                // Have to call GetDirectoryName() because GetAssetPath() returns a value relative to the parent directory of Application.dataPath
                secretsPath = Path.GetFullPath(FileUtil.GetPhysicalPath(AssetDatabase.GetAssetPath(secretsFile)), Path.GetDirectoryName(Application.dataPath));
            };
            var port = GetPort();
            var startInfo = new ProcessStartInfo()
            {
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = false,
                UseShellExecute = false,
                FileName = m_Preferences.DotnetPath,
                Arguments = $"{compiledCloudCodeServerPath} run" +
                            $" -p \"{compiledModuleDir}\"" +
                            $" --port {port}" +
                            (string.IsNullOrEmpty(secretsPath) ? "" : $" -s \"{secretsPath}\"")
            };

            m_Logger.LogInfo($"Starting local server with arguments {startInfo.FileName} {startInfo.Arguments}");

            startInfo.EnvironmentVariables["GATEWAY_JWT"] = await m_AccessTokens.GetServicesGatewayTokenAsync();

            // Fail fast if selected port is in use.
            if (!await IsPortAvailable(GetPort(), cancellationToken))
                throw new Exception($"Server Port {GetPort()} is not available.");
            cancellationToken.ThrowIfCancellationRequested();

            // Reset the Client's configuration to point to the new port
            ((CloudCodeLocalServerApi)m_LocalServerClient).Configuration.BasePath = $"{k_ServerUrl}:{port}";
            EditorPrefs.SetInt("CLOUD_CODE_DEBUG_PORT", port);

            // Start the process and track its PID
            var process = m_ProcessRunner.RunAsyncFireAndForget(startInfo, OnServerStdOut);
            SetAndTrackServerPid(process.Id);

            // If the user force cancels, abort
            cancellationToken.ThrowIfCancellationRequested();

            // Perform health checks until the Server is fully running, or timeout.
            await m_LocalServerClient.HealthCheck(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            // Else the server had started successfully.
            SetAndTrackServerStatus(LocalCloudCodeServerStatus.Started);
            _ = Task.Run(() => PeriodicHealthCheckTask(m_LocalServerClient, OnHealthCheckPingsFail, cancellationToken),
                                                       cancellationToken);
            m_Logger.LogVerbose("Local CC Server has Started.");
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

            // TODO MTT-13965: Cleanup Logging. Might want to retain logs if ExitCode != 0

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
            ClearDeploymentStatus();
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

            // TODO MTT-13965: Restore Logging

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
                m_Logger.LogError( $"Local Server Restore Failed: {e}");
            }

            // In an event of failure, ensure that any resources are stopped
            ForceStopLocalServerSafe();
        }
    }

    string GetLocalCloudCodeServerPath()
    {
        var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(GetType().Assembly);
        return Path.Combine(packageInfo.resolvedPath, "Editor", "CloudCodeDebugger~", "CloudCodeDebugger.dll");
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

    // TODO MTT-13965: Figure out Local CC Server logging - we can't use console out.
    void OnServerStdOut(string output)
    {
        m_Logger.LogInfo($"[Local Server] {output}");
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

        var lsofProcess = await m_ProcessRunner.RunAsync(lsofStartInfo, cancellationToken: cancellationToken);
        return lsofProcess.ExitCode == 1;
#endif
    }

#endregion

#region Deployment Status Helper

    void SetDeployStatusWithState(string message, string messageDetail, SeverityLevel messageSeverity)
    {
        var ccmrs = m_CloudCodeModuleReferenceCollection.ToList();
        m_DeployHandler.SetDeployStatusWithState(ccmrs, message, messageDetail, messageSeverity);
    }

    void ClearDeploymentStatus()
    {
        var ccmrs = m_CloudCodeModuleReferenceCollection.ToList();
        m_DeployHandler.ClearDeploymentStatus(ccmrs);
    }

#endregion

    }
}
