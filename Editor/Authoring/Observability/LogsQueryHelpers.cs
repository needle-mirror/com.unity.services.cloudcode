using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Unity.Services.CloudCode.Authoring.Editor.Observability
{
    static class LogsQueryHelpers
    {
        static readonly Regex k_RelativeTime = new Regex(@"^now(?:-(?<amount>\d{1,5})(?<unit>[mhd]))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        internal static string FormatQueryTimestamp(DateTime utc)
        {
            return utc.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        }

        internal static bool HasMorePages(int offset, int limit, int recordCount, int total)
        {
            if (recordCount < limit)
            {
                return false;
            }

            return total <= 0 || offset + limit < total;
        }

        internal static string ResolveTimestamp(string value, string argName, DateTime utcNow)
        {
            if (!value.StartsWith("now", StringComparison.OrdinalIgnoreCase))
                return value;
            var match = k_RelativeTime.Match(value);
            if (!match.Success)
                throw new ArgumentException($"'{value}' is not a valid relative time (expected now or now-<n><m|h|d>).", argName);
            if (!match.Groups["amount"].Success)
                return FormatQueryTimestamp(utcNow);
            if (!int.TryParse(match.Groups["amount"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var amount))
                throw new ArgumentException($"'{value}' is not a valid relative time (expected now or now-<n><m|h|d>).", argName);
            var offset = char.ToLowerInvariant(match.Groups["unit"].Value[0]) switch
            {
                'm' => TimeSpan.FromMinutes(amount),
                'h' => TimeSpan.FromHours(amount),
                _ => TimeSpan.FromDays(amount)
            };
            return FormatQueryTimestamp(utcNow - offset);
        }

        internal static LogSeverity ParseSeverity(string severity)
        {
            if (string.IsNullOrEmpty(severity))
                return LogSeverity.All;
            // Enum.TryParse also accepts numbers and comma lists; names are letters only.
            for (var i = 0; i < severity.Length; i++)
            {
                if (!char.IsLetter(severity[i]))
                    throw InvalidSeverity(severity);
            }

            if (!Enum.TryParse<LogSeverity>(severity, true, out var parsed))
                throw InvalidSeverity(severity);
            return parsed;
        }

        static ArgumentException InvalidSeverity(string severity)
        {
            return new ArgumentException($"'{severity}' is not a valid severity (expected all, debug, info, warn or error).", nameof(severity));
        }
    }
}
