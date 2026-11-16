#if UNITY_PIPELINE_AVAILABLE && UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Pipeline.Commands;
using Unity.Services.CloudCode.Authoring.Editor.Observability;
using UnityEditor;
#if UNITY_6000_3_OR_NEWER
using Unity.Services.CloudCode.Authoring.Editor;
using Unity.Services.CloudCode.Authoring.Editor.Debugger;
#endif

namespace Unity.Services.CloudCode.Cli
{
    /// <summary>
    /// Pipeline CLI commands for inspecting and managing the Cloud Code local server, and for querying the logs of deployed Cloud Code.
    /// Only compiled when the <c>com.unity.pipeline</c> package is present
    /// (gated by the <c>UNITY_PIPELINE_AVAILABLE</c> version define on the asmdef).
    /// </summary>
    /// <remarks>
    /// "Mode" is the Cloud Code backend the runtime SDK targets: <c>Local</c> while this project's Editor
    /// tracks a running local server, otherwise the cloud. The mode is therefore a read-only
    /// consequence of whether the local server is running; the server lifecycle
    /// commands (start/stop) are what change it. Those commands require the local
    /// server itself (Unity 6000.3+); <c>services.cloudcode get_mode</c> works regardless and
    /// reports <c>Remote</c> when the local server is unavailable.
    /// </remarks>
    static class CloudCodeEditorCliCommands
    {
        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Get the active Cloud Code mode: 'Local' when a local server is running, otherwise 'Remote'")]
        public static string GetMode()
        {
            return EditorPrefs.GetInt(CloudCodeInitializer.k_LocalServerPidPrefsKey, CloudCodeCliConstants.NoLocalServer) != CloudCodeCliConstants.NoLocalServer
                ? CloudCodeCliConstants.ModeLocal
                : CloudCodeCliConstants.ModeRemote;
        }

        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Query the logs emitted by the Cloud Code scripts and modules deployed to the active environment (same as Services > CloudCode > Observability Logs). Does not include the local Cloud Code server. To page, run the call again with the NextPage arguments from the result (absolute From and To, so the window cannot drift between pages). Records with identical timestamps can reorder between pages, so prefer one larger --limit over many small pages. Only logger.* lines are ingested, not console.*")]
        public static async Task<RemoteLogsDto> GetRemoteLogs(
            [CliArg("from", "Start of the time range: 'now-<n><m|h|d>' or an ISO 8601 UTC timestamp (default now-1h). Relative values are resolved to absolute timestamps in the result; reuse the returned From when paging")] string from = null,
            [CliArg("to", "End of the time range, same formats as 'from' (default: now). Relative values are resolved to absolute timestamps in the result; reuse the returned To when paging")] string to = null,
            [CliArg("severity", "Only records of this severity band: all, debug, info, warn or error (default all)")] string severity = null,
            [CliArg("filter", "Log filter expression ANDed with the Cloud Code scope, e.g. body ~= \"timeout\"")] string filter = null,
            [CliArg("limit", "Records per page (1-100, default 50)")] string limit = null,
            [CliArg("offset", "Number of records to skip (default 0)")] string offset = null)
        {
            var utcNow = DateTime.UtcNow;
            var query = new LogsQuery
            {
                From = LogsQueryHelpers.ResolveTimestamp(string.IsNullOrEmpty(from) ? CloudCodeCliConstants.DefaultLogsFrom : from, nameof(from), utcNow),
                To = LogsQueryHelpers.ResolveTimestamp(string.IsNullOrEmpty(to) ? "now" : to, nameof(to), utcNow),
                Severity = LogsQueryHelpers.ParseSeverity(severity),
                UserFilter = filter
            };
            if (!string.IsNullOrEmpty(limit))
            {
                if (!int.TryParse(limit, out var parsedLimit) || parsedLimit < 1 || parsedLimit > LogsQuery.MaxLimit)
                    throw new ArgumentException($"'{limit}' is not a valid limit (expected 1-{LogsQuery.MaxLimit}).", nameof(limit));
                query.Limit = parsedLimit;
            }
            if (!string.IsNullOrEmpty(offset))
            {
                if (!int.TryParse(offset, out var parsedOffset) || parsedOffset < 0)
                    throw new ArgumentException($"'{offset}' is not a valid offset (expected 0 or greater).", nameof(offset));
                query.Offset = parsedOffset;
            }

            var response = await LogsClientFactory.CreateClient().GetLogsAsync(query);
            var records = response.Results ?? new List<LogRecord>();
            var hasMore = LogsQueryHelpers.HasMorePages(query.Offset, query.Limit, records.Count, response.Total);
            return new RemoteLogsDto
            {
                From = query.From,
                To = query.To,
                Severity = query.Severity.ToString(),
                Offset = query.Offset,
                Limit = query.Limit,
                Total = response.Total,
                HasMore = hasMore,
                NextOffset = hasMore ? query.Offset + query.Limit : (int?)null,
                Filter = filter,
                NextPage = hasMore ? BuildNextPage(query.From, query.To, query.Severity.ToString(), filter, query.Limit, query.Offset + query.Limit) : null,
                Records = records
            };
        }

        internal static string BuildNextPage(string from, string to, string severity, string filter, int limit, int nextOffset)
        {
            var args = $"--from {from} --to {to} --limit {limit} --offset {nextOffset}";
            if (!string.Equals(severity, LogSeverity.All.ToString(), StringComparison.OrdinalIgnoreCase))
                args += $" --severity {severity.ToLowerInvariant()}";
            if (!string.IsNullOrEmpty(filter))
                // single quotes so a POSIX shell does not expand $ or backticks
                args += $" --filter '{filter.Replace("'", "'\\''")}'";
            return args;
        }

        public class RemoteLogsDto
        {
            public string From { get; set; }
            public string To { get; set; }
            public string Severity { get; set; }
            public int Offset { get; set; }
            public int Limit { get; set; }
            // 0 means the API did not report a total
            public int Total { get; set; }
            public bool HasMore { get; set; }
            public int? NextOffset { get; set; }
            public string Filter { get; set; }
            // Arguments for the next page in POSIX shell syntax (single-quoted filter), null when HasMore is false
            public string NextPage { get; set; }
            public List<LogRecord> Records { get; set; }
        }

#if UNITY_6000_3_OR_NEWER
        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Get the local Cloud Code server state: mode, status, pid, port and last failure")]
        public static LocalServerStatusDto GetLocalServerStatus()
        {
            var server = LocalServer();
            return new LocalServerStatusDto
            {
                Mode = GetMode(),
                Status = server.GetCurrentServerStatus().ToString(),
                Pid = server.GetServerPid(),
                Port = server.GetPort(),
                LastFailure = server.GetLastServerFailure()
            };
        }

        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Read the local Cloud Code server's log (module output and server errors). Pass the returned cursor back to get only newer entries. After the server stops, reads this project's last run's log. Tokens are redacted. Each call reads at most 1 MiB of the log: the newest 1 MiB without a cursor, the next 1 MiB after it with one (HasMore says when more remains)")]
        public static LocalServerLogPage GetLocalServerLogs(
            [CliArg("limit", "Maximum entries to return (default 100). Without a cursor: the newest; with one: the oldest after it")] int limit = 100,
            [CliArg("level", "Minimum level: verbose, info, warning or error (default info). Server plumbing counts as verbose")] string level = "info",
            [CliArg("cursor", "Cursor from a previous call: return only entries written after it")] string cursor = null)
        {
            return LocalServerLogReader.Read(LocalServer().GetLogFilePath(), limit, level, cursor);
        }

        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Start the local Cloud Code server (compiles modules and spawns the server process); switches the active mode to 'Local'")]
        public static async Task<LocalServerStatusDto> StartLocalServer()
        {
            RequireNotPlaying();
            await LocalServer().StartCompilationAndService(false);
            return RequireStatus(ICloudCodeLocalServer.LocalCloudCodeServerStatus.Started, "start");
        }

        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Stop the local Cloud Code server; switches the active mode back to 'Remote'")]
        public static async Task<LocalServerStatusDto> StopLocalServer()
        {
            RequireNotPlaying();
            await LocalServer().StopCompilationAndService();
            return RequireStatus(ICloudCodeLocalServer.LocalCloudCodeServerStatus.Idle, "stop");
        }

        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Set the port the local Cloud Code server listens on")]
        public static LocalServerStatusDto SetLocalServerPort(
            [CliArg("port", "TCP port (1-65535)", Required = true)] string port)
        {
            if (!ushort.TryParse(port, out var value) || value == 0)
                throw new ArgumentException($"'{port}' is not a valid TCP port (expected 1-65535).", nameof(port));

            LocalServer().SetPort(value);
            return GetLocalServerStatus();
        }

        [CliGroupCommand(CloudCodeCliConstants.Group,
            "Delete persisted local Cloud Code server state (requires a server restart to take effect)")]
        public static string DeleteLocalServerState()
        {
            var result = LocalServer().ClearServerState();
            if (result == ICloudCodeLocalServer.ClearStateResult.Failed)
                throw new InvalidOperationException(
                    "Failed to delete the local Cloud Code server state. See the Console for the reason.");

            return result == ICloudCodeLocalServer.ClearStateResult.Cleared ? "cleared" : "nothing to clear";
        }

        static ICloudCodeLocalServer LocalServer()
        {
            return CloudCodeAuthoringServices.Instance.GetService<ICloudCodeLocalServer>();
        }

        // The lifecycle calls never throw - a failure is recorded and the server returns to Idle - so a
        // caller that only sees the returned status cannot tell a completed command from a failed one.
        // A failure left behind belongs to the call this one ran or joined: see StopCompilationAndService.
        static LocalServerStatusDto RequireStatus(
            ICloudCodeLocalServer.LocalCloudCodeServerStatus expected, string action)
        {
            var status = GetLocalServerStatus();
            if (status.Status == expected.ToString() && string.IsNullOrEmpty(status.LastFailure))
                return status;

            var failure = string.IsNullOrEmpty(status.LastFailure)
                ? $"The server is {status.Status}."
                : status.LastFailure;
            throw new InvalidOperationException(
                $"The local Cloud Code server failed to {action}. {failure}");
        }

        // The local server can only be started/stopped outside play mode (matches the toolbar UI).
        static void RequireNotPlaying()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("The local Cloud Code server can only be started or stopped when not in play mode.");
        }

        /// <summary>Serializable view of the local Cloud Code server state for CLI JSON output.</summary>
        public class LocalServerStatusDto
        {
            public string Mode { get; set; }
            public string Status { get; set; }
            public int Pid { get; set; }
            public int Port { get; set; }
            public string LastFailure { get; set; }
        }
#endif
    }
}
#endif
