#if UNITY_6000_3_OR_NEWER
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using ILogger = Unity.Services.CloudCode.Authoring.Editor.Core.Logging.ILogger;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    // Re-pipes the Local Cloud Code Server's --log-file (CLEF JSON lines from the server's
    // Serilog file sink) into the Unity Editor console. The read offset is persisted in
    // EditorPrefs so tailing resumes — without gaps or duplicates — after a domain reload,
    // when the server process survives but the launching Process (and its stdout subscription)
    // does not.
    class LocalServerLogTailer
    {
        internal enum Level { Verbose, Info, Warning, Error }

        const string k_Prefix = "[Local Server]";
        const string k_LogFileKey = "LOCAL_CLOUD_CODE_LOG_FILE";
        const string k_LogOffsetKey = "LOCAL_CLOUD_CODE_LOG_OFFSET";
        const double k_PollIntervalSeconds = 0.5;

        // The server logs its own plumbing at Information: per-request ASP.NET/Serilog lines, the
        // no-op auth and session stubs (several per function call), Orleans and Centrifuge/PushHub
        // chatter. None of it says anything about the user's module, but all of it lands in the
        // Console alongside the module's own output. Demote it to Verbose so it is still recoverable
        // from the log file, and from the Console under the verbose-logging define, without
        // drowning what users came for.
        static readonly string[] k_NoiseSourceContextPrefixes =
        {
            "ScriptRunner.NoOp",
            "Serilog.AspNetCore.RequestLoggingMiddleware",
            "Microsoft.AspNetCore.",
            "Microsoft.Hosting.",
            "Orleans.",
            // The server's own services: module load and assembly load timings. Deliberately not
            // the whole "CloudCodeDebugger." namespace — its controllers report user script errors
            // at Information, and those must reach the Console.
            "CloudCodeDebugger.Services.",
        };

        // Sources that are plumbing at every level, not just at Information. The ProblemDetails
        // middleware logs a generic "An unhandled exception has occurred while executing the
        // request." Error for the same failure the server already reports through CloudCodeErrors,
        // naming the module and carrying the stack trace.
        static readonly string[] k_NoiseSourceContextPrefixesAllLevels =
        {
            "Hellang.Middleware.ProblemDetails.",
        };

        // Server-side chatter logged without a SourceContext, matched on the message itself.
        static readonly string[] k_NoiseMessagePrefixes =
        {
            "Centrifuge:",
            "PushHub:",
            "Logging to file:",
            "Starting Debugger API",
            "Graceful shutdown initiated",
        };

        readonly ILogger m_Logger;

        string m_LogFilePath;
        long m_Offset;
        bool m_Polling;
        double m_NextPollTime;

        internal LocalServerLogTailer(ILogger logger)
        {
            m_Logger = logger;
            m_LogFilePath = EditorPrefs.GetString(k_LogFileKey, string.Empty);
            m_Offset = long.TryParse(EditorPrefs.GetString(k_LogOffsetKey, "0"), out var v) ? v : 0L;
            // PumpOnce advances the offset in memory; persisting only on state changes and right
            // before a domain reload avoids a disk/registry write on every 500ms poll.
            AssemblyReloadEvents.beforeAssemblyReload += PersistState;
        }

        internal string LogFilePath => m_LogFilePath;
        internal long Offset => m_Offset;

        internal void PersistState()
        {
            EditorPrefs.SetString(k_LogFileKey, m_LogFilePath ?? string.Empty);
            EditorPrefs.SetString(k_LogOffsetKey, m_Offset.ToString());
        }

        // Begin tailing a fresh log file from the start.
        public void Start(string logFilePath)
        {
            StopPolling();
            m_LogFilePath = logFilePath;
            m_Offset = 0;
            PersistState();
            StartPolling();
        }

        // Resume tailing from the persisted offset after a domain reload.
        public void Restore()
        {
            if (string.IsNullOrEmpty(m_LogFilePath))
                return;
            StartPolling();
        }

        // Stop tailing, flushing any remaining whole lines first. The log file is left on disk for
        // post-mortem inspection; old per-run logs are pruned on the next Start.
        public void Stop()
        {
            StopPolling();
            PumpOnce();

            m_LogFilePath = string.Empty;
            m_Offset = 0;
            PersistState();
        }

        void StartPolling()
        {
            if (m_Polling)
                return;
            m_Polling = true;
            m_NextPollTime = 0;
            EditorApplication.update += Tick;
        }

        void StopPolling()
        {
            if (!m_Polling)
                return;
            m_Polling = false;
            EditorApplication.update -= Tick;
        }

        void Tick()
        {
            if (EditorApplication.timeSinceStartup < m_NextPollTime)
                return;
            m_NextPollTime = EditorApplication.timeSinceStartup + k_PollIntervalSeconds;
            PumpOnce();
        }

        // Emit whole lines appended since the last offset. A trailing partial line (not yet
        // newline-terminated) is left in place and picked up once the writer completes it.
        internal void PumpOnce()
        {
            var path = LogFilePath;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var start = m_Offset;
                if (stream.Length < start)
                    start = 0; // file was truncated or rotated
                var available = stream.Length - start;
                if (available <= 0)
                    return;

                stream.Seek(start, SeekOrigin.Begin);
                var buffer = new byte[available];
                var read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    return; // nothing read (e.g. truncated between the length check and the read)

                var lastNewline = Array.LastIndexOf(buffer, (byte)'\n', read - 1);
                if (lastNewline < 0)
                    return; // no complete line available yet

                var text = Encoding.UTF8.GetString(buffer, 0, lastNewline + 1);
                foreach (var line in text.Split('\n'))
                {
                    var trimmed = line.TrimEnd('\r');
                    if (trimmed.Length > 0)
                        Emit(trimmed);
                }

                m_Offset = start + lastNewline + 1;
            }
            catch (IOException)
            {
                // File momentarily locked by the writer; retry on the next poll.
            }
        }

        void Emit(string line)
        {
            var(level, message) = Format(line);
            var prefixed = $"{k_Prefix} {message}";
            switch (level)
            {
                case Level.Error:
                    m_Logger.LogError(prefixed);
                    break;
                case Level.Warning:
                    m_Logger.LogWarning(prefixed);
                    break;
                case Level.Verbose:
                    m_Logger.LogVerbose(prefixed);
                    break;
                default:
                    m_Logger.LogInfo(prefixed);
                    break;
            }
        }

        // Parse a CLEF (Compact Log Event Format) line. Falls back to passing the raw line
        // through as Info when it isn't valid CLEF JSON.
        //
        // Routing an entry to Verbose is what decides whether it reaches the Console: Unity compiles
        // LogVerbose out unless the verbose-logging define is set.
        internal static (Level level, string message) Format(string clefLine)
        {
            if (string.IsNullOrWhiteSpace(clefLine))
                return (Level.Info, string.Empty);

            JObject obj;
            try
            {
                obj = JObject.Parse(clefLine);
            }
            catch (Exception)
            {
                return (Level.Info, clefLine);
            }

            var message = obj.Value<string>("@m");
            if (string.IsNullOrEmpty(message))
                message = obj.Value<string>("@mt");
            if (string.IsNullOrEmpty(message))
                message = clefLine;

            var exception = obj.Value<string>("@x");
            if (!string.IsNullOrEmpty(exception))
                message = $"{message}\n{exception}";

            var level = ParseLevel(obj.Value<string>("@l"));
            var sourceContext = obj.Value<string>("SourceContext");

            // The Console should carry the module's output, not the server's plumbing. Only routine
            // chatter is demoted; a warning or error from the same source still needs to be seen,
            // unless the source is plumbing whatever the level.
            if (IsInfrastructureNoiseAtAnyLevel(sourceContext) ||
                (level == Level.Info && IsInfrastructureNoise(sourceContext, message)))
                level = Level.Verbose;

            return (level, message);
        }

        // True for sources whose entries are plumbing at every level, warnings and errors included.
        internal static bool IsInfrastructureNoiseAtAnyLevel(string sourceContext)
        {
            if (string.IsNullOrEmpty(sourceContext))
                return false;

            foreach (var prefix in k_NoiseSourceContextPrefixesAllLevels)
            {
                if (sourceContext.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        // True for log entries emitted by the server's own plumbing rather than by module code.
        internal static bool IsInfrastructureNoise(string sourceContext, string message)
        {
            if (!string.IsNullOrEmpty(sourceContext))
            {
                foreach (var prefix in k_NoiseSourceContextPrefixes)
                {
                    if (sourceContext.StartsWith(prefix, StringComparison.Ordinal))
                        return true;
                }
            }

            if (string.IsNullOrEmpty(message))
                return false;

            foreach (var prefix in k_NoiseMessagePrefixes)
            {
                if (message.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        // CLEF omits @l for Information, the assumed default.
        static Level ParseLevel(string clefLevel)
        {
            if (string.IsNullOrEmpty(clefLevel))
                return Level.Info;

            switch (clefLevel.ToLowerInvariant())
            {
                case "fatal":
                case "error":
                    return Level.Error;
                case "warning":
                    return Level.Warning;
                case "debug":
                case "verbose":
                    return Level.Verbose;
                default:
                    return Level.Info;
            }
        }
    }
}
#endif
