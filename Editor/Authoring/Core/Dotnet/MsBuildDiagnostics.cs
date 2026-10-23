using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Core.Dotnet
{
    // Picks the compiler diagnostics out of a `dotnet` run's output, discarding everything else.
    static class MsBuildDiagnostics
    {
        // MSBuild diagnostics read "<origin>: <severity> <code>: <text> [<project>]".
        static readonly Regex k_Diagnostic = new Regex(
            @"^\s*(?<origin>.*?):\s*(?<severity>error|warning|fatal error)\s+(?<code>[A-Za-z]+\d+):\s*(?<text>.*?)\s*$",
            RegexOptions.Compiled);

        static readonly Regex k_ProjectOrSolutionSuffix = new Regex(
            @"\s*\[[^\]]*\.(?:(?:cs|vb|fs)proj|sln)\]\s*$", RegexOptions.Compiled);

        static readonly char[] k_PathSeparators = { '/', '\\' };

        public static IReadOnlyList<(SeverityLevel Severity, string Text)> Parse(string output)
        {
            if (string.IsNullOrWhiteSpace(output))
                return Array.Empty<(SeverityLevel, string)>();

            var diagnostics = new List<(SeverityLevel, string)>();
            foreach (var raw in output.Split('\n'))
            {
                var line = k_ProjectOrSolutionSuffix.Replace(raw.TrimEnd('\r').TrimEnd(), string.Empty);
                var match = k_Diagnostic.Match(line);
                if (!match.Success)
                    continue;

                var severity = match.Groups["severity"].Value.EndsWith("error", StringComparison.Ordinal)
                    ? SeverityLevel.Error
                    : SeverityLevel.Warning;

                diagnostics.Add((severity, Shorten(match)));
            }

            return diagnostics;
        }

        // "/long/absolute/path/Thing.cs(41,31)" carries one useful part - the file and position.
        static string Shorten(Match match)
        {
            var origin = match.Groups["origin"].Value.Trim();
            var separator = origin.LastIndexOfAny(k_PathSeparators);
            if (separator >= 0 && separator < origin.Length - 1)
                origin = origin.Substring(separator + 1);

            return $"{origin}: {match.Groups["severity"].Value} {match.Groups["code"].Value}: {match.Groups["text"].Value}";
        }

        // Status text for the deploy item; the diagnostics reach the Console on their own.
        public static string Summarize(
            string command, int exitCode, IReadOnlyList<(SeverityLevel Severity, string Text)> diagnostics)
        {
            var errors = 0;
            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity == SeverityLevel.Error)
                    errors++;
            }

            if (errors == 0)
                return $"{command} failed with exit code {exitCode}.";

            return errors == 1
                ? $"{command} failed with 1 error. See the Console for details."
                : $"{command} failed with {errors} errors. See the Console for details.";
        }
    }
}
