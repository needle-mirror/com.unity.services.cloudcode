#if UNITY_6000_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Services.CloudCode.Editor.Shared.Infrastructure.IO;

namespace Unity.Services.CloudCode.Authoring.Editor.Debugger
{
    class LocalServerLogEntry
    {
        public string Time;
        public string Level;
        public string Message;
    }

    class LocalServerLogPage
    {
        public string LogFile;
        public string Cursor;
        public bool HasMore;
        public bool CursorReset;
        public string Message;
        public List<LocalServerLogEntry> Entries = new List<LocalServerLogEntry>();
    }

    static class LocalServerLogReader
    {
        internal const int k_MaxReadBytes = 1024 * 1024;
        internal const string k_NoLogMessage =
            "No local Cloud Code server log for this project. Start the local server to create one;"
            + " a running server has none when its log folder is not writable.";

        internal const string k_RedactionTimedOutMessage = "<message withheld: redaction timed out>";

        static readonly TimeSpan k_RegexTimeout = TimeSpan.FromMilliseconds(250);

        static readonly Regex k_JwtPattern = CreateJwtPattern(k_RegexTimeout);
        static readonly Regex k_BearerPattern = CreateBearerPattern(k_RegexTimeout);

        // The lookbehind stops a start inside a token that was already scanned from retrying, which keeps the
        // match linear on input such as "eyJeyJeyJ...".
        internal static Regex CreateJwtPattern(TimeSpan timeout) => new Regex(
            @"(?<![A-Za-z0-9_-])eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]*", RegexOptions.CultureInvariant,
            timeout);

        // The 8-character minimum keeps prose such as "bearer of bad news" intact; no real token is that short.
        internal static Regex CreateBearerPattern(TimeSpan timeout) => new Regex(
            @"\bbearer\s+[A-Za-z0-9._~+/=-]{8,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, timeout);

        internal static LocalServerLogPage Read(string path, int limit, string level, string cursor,
            int maxBytes = k_MaxReadBytes)
        {
            if (limit < 1)
                throw new ArgumentException($"'{limit}' is not a valid limit (expected 1 or more).", nameof(limit));
            if (!Enum.TryParse<LocalServerLogTailer.Level>(level, true, out var minLevel) ||
                !Enum.IsDefined(typeof(LocalServerLogTailer.Level), minLevel))
                throw new ArgumentException(
                    $"'{level}' is not a valid level (expected verbose, info, warning or error).", nameof(level));

            var hasCursor = !string.IsNullOrEmpty(cursor);
            var (cursorFile, cursorOffset, cursorMidRecord) = hasCursor ? ParseCursor(cursor) : (null, 0L, false);

            var page = new LocalServerLogPage();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                page.Message = k_NoLogMessage;
                return page;
            }

            var name = Path.GetFileName(path);
            page.LogFile = path;

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = stream.Length;

            long cursorOffsetOut;
            bool midRecord;
            if (!hasCursor)
                cursorOffsetOut = ReadTail(stream, length, limit, minLevel, maxBytes, page, out midRecord);
            else
            {
                page.CursorReset = cursorFile != name || cursorOffset > length;
                cursorOffsetOut = ReadForward(stream, length, page.CursorReset ? 0 : cursorOffset, limit, minLevel,
                    maxBytes, page, !page.CursorReset && cursorMidRecord, out midRecord);
            }

            // A trailing "~" marks a cursor inside an oversized record, whose remainder the next read skips.
            page.Cursor = $"{name}@{cursorOffsetOut}{(midRecord ? "~" : string.Empty)}";
            return page;
        }

        internal static string ServerLogDirectory(string projectRoot) =>
            PathUtils.Join(projectRoot, "Logs", "CloudCodeLocalServer");

        internal const string k_ServerLogSearchPattern = "server-*.log";

        internal static string ServerLogFileName(DateTime now) => $"server-{now:yyyyMMdd-HHmmss-fff}.log";

        internal static string FindNewestServerLog(string logDir)
        {
            if (string.IsNullOrEmpty(logDir) || !Directory.Exists(logDir))
                return null;

            string newest = null;
            var newestTime = DateTime.MinValue;
            foreach (var file in Directory.GetFiles(logDir, k_ServerLogSearchPattern))
            {
                var time = File.GetLastWriteTimeUtc(file);
                if (newest == null || time > newestTime)
                {
                    newest = file;
                    newestTime = time;
                }
            }

            return newest;
        }

        // A server that launched without a log file has none; the newest file on disk is an earlier run's.
        internal static string ResolveLogFile(string tailerPath, string logDir, bool serverRunsWithoutLog)
        {
            if (!string.IsNullOrEmpty(tailerPath))
                return tailerPath;

            return serverRunsWithoutLog ? null : FindNewestServerLog(logDir);
        }

        internal static string Redact(string text) => Redact(text, k_BearerPattern, k_JwtPattern);

        // Fails closed: text that cannot be scanned in time is withheld rather than returned unredacted.
        internal static string Redact(string text, Regex bearerPattern, Regex jwtPattern)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            try
            {
                text = bearerPattern.Replace(text, "Bearer <redacted>");
                return jwtPattern.Replace(text, "<redacted-jwt>");
            }
            catch (RegexMatchTimeoutException)
            {
                return k_RedactionTimedOutMessage;
            }
        }

        static long ReadTail(FileStream stream, long length, int limit, LocalServerLogTailer.Level minLevel,
            int maxBytes, LocalServerLogPage page, out bool midRecord)
        {
            midRecord = false;
            var readFrom = Math.Max(0, length - maxBytes);
            var buffer = ReadRange(stream, readFrom, length - readFrom);
            var split = SplitLines(buffer, readFrom, false);

            // The window's first byte only probes for a record boundary: when it is the newline ending the
            // previous record, the first split element is empty and the next record is whole. Otherwise that
            // element is a cut record and is dropped; its end is the only line boundary known when nothing
            // complete follows it.
            var boundary = 0L;
            if (readFrom > 0 && buffer.Length > 0 && buffer[0] != (byte)'\n')
            {
                if (split.Count > 0)
                {
                    boundary = split[0].End;
                    split.RemoveAt(0);
                }
                else
                {
                    // No newline in the whole window: one record larger than the window, treated as in ReadForward.
                    page.Entries = new List<LocalServerLogEntry> { OversizedEntry(maxBytes) };
                    midRecord = true;
                    return length;
                }
            }

            var matching = new List<LocalServerLogEntry>();
            foreach (var (text, _, _) in split)
            {
                if (TryMatch(text, minLevel, out var entry))
                    matching.Add(entry);
            }

            var skip = Math.Max(0, matching.Count - limit);
            page.Entries = matching.GetRange(skip, matching.Count - skip);
            return split.Count > 0 ? split[split.Count - 1].End : boundary;
        }

        static long ReadForward(FileStream stream, long length, long from, int limit,
            LocalServerLogTailer.Level minLevel, int maxBytes, LocalServerLogPage page, bool skipFragment,
            out bool midRecord)
        {
            midRecord = skipFragment;
            var skipping = skipFragment;
            var remaining = length - from;
            var buffer = ReadRange(stream, from, Math.Min(maxBytes, remaining));
            var windowEnd = from + buffer.Length;
            var split = SplitLines(buffer, from, remaining > maxBytes);

            var consumed = from;
            for (var i = 0; i < split.Count; i++)
            {
                consumed = split[i].End;
                if (skipping)
                {
                    skipping = split[i].Oversized;
                    midRecord = skipping;
                    continue;
                }

                midRecord = split[i].Oversized;
                var entry = split[i].Oversized ? OversizedEntry(maxBytes) : null;
                if (entry == null && !TryMatch(split[i].Text, minLevel, out entry))
                    continue;

                page.Entries.Add(entry);
                if (page.Entries.Count == limit)
                {
                    page.HasMore = i + 1 < split.Count || windowEnd < length;
                    return consumed;
                }
            }

            page.HasMore = windowEnd < length;
            return consumed;
        }

        static byte[] ReadRange(FileStream stream, long from, long count)
        {
            stream.Seek(from, SeekOrigin.Begin);
            var buffer = new byte[count];
            var total = 0;
            int read;
            while (total < buffer.Length && (read = stream.Read(buffer, total, buffer.Length - total)) > 0)
                total += read;

            if (total < buffer.Length)
                Array.Resize(ref buffer, total);
            return buffer;
        }

        // Splits on 0x0A so the byte offsets stay exact. A trailing line with no newline yet is left
        // out, so a cursor never lands inside a line the writer has not finished.
        static List<(string Text, long End, bool Oversized)> SplitLines(byte[] buffer, long baseOffset,
            bool skipUnterminated)
        {
            var result = new List<(string Text, long End, bool Oversized)>();
            var lineStart = 0;
            int newline;
            while ((newline = Array.IndexOf(buffer, (byte)'\n', lineStart)) >= 0)
            {
                var text = Encoding.UTF8.GetString(buffer, lineStart, newline - lineStart).TrimEnd('\r');
                result.Add((text, baseOffset + newline + 1, false));
                lineStart = newline + 1;
            }

            // A window full of bytes with no newline would otherwise never advance the cursor. Its bytes are
            // a fragment of one record, which would parse as Info and vanish under a level filter.
            if (result.Count == 0 && skipUnterminated && buffer.Length > 0)
                result.Add((null, baseOffset + buffer.Length, true));

            return result;
        }

        static LocalServerLogEntry OversizedEntry(int maxBytes) => new LocalServerLogEntry
        {
            Level = LocalServerLogTailer.Level.Warning.ToString(),
            Message = $"Log record larger than {maxBytes} bytes skipped (cursor advanced past it)"
        };

        static bool TryMatch(string line, LocalServerLogTailer.Level minLevel, out LocalServerLogEntry entry)
        {
            entry = null;
            if (string.IsNullOrWhiteSpace(line))
                return false;

            var (level, message, time) = LocalServerLogTailer.Parse(line);
            if (level < minLevel)
                return false;

            entry = new LocalServerLogEntry { Time = time, Level = level.ToString(), Message = Redact(message) };
            return true;
        }

        static (string file, long offset, bool midRecord) ParseCursor(string cursor)
        {
            var midRecord = cursor.EndsWith("~", StringComparison.Ordinal);
            var body = midRecord ? cursor.Substring(0, cursor.Length - 1) : cursor;
            var at = body.LastIndexOf('@');
            if (at <= 0 || !long.TryParse(body.Substring(at + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
                throw new ArgumentException(
                    $"'{cursor}' is not a cursor returned by get_local_server_logs.", nameof(cursor));

            return (body.Substring(0, at), offset, midRecord);
        }
    }
}
#endif
