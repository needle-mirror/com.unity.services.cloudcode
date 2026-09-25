using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Deployment.ModuleGeneration.Exceptions;
using Unity.Services.CloudCode.Authoring.Editor.Core.Dotnet;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;
using Unity.Services.CloudCode.Authoring.Editor.Projects.Settings;
using Unity.Services.DeploymentApi.Editor;

namespace Unity.Services.CloudCode.Authoring.Editor.Projects.Dotnet
{
    class DotnetRunner : IDotnetRunner
    {
        readonly IProcessRunner m_ProcessRunner;
        readonly ICloudCodePreferences m_Preferences;
        readonly ILogger m_Logger;

        static readonly string k_VersionCommand = "--version";
        static readonly string k_DotnetDefaultPathFallback = "dotnet";

        public DotnetRunner(IProcessRunner processRunner, ICloudCodePreferences settings, ILogger logger)
        {
            m_ProcessRunner = processRunner;
            m_Preferences = settings;
            m_Logger = logger;
        }

        public async Task<bool> IsDotnetAvailable()
        {
            try
            {
                await ExecuteDotnetAsync(new List<string>
                {
                    k_VersionCommand
                });
                return true;
            }
            catch (Exception)
            {
                try
                {
                    m_Preferences.DotnetPath = k_DotnetDefaultPathFallback;
                    m_Preferences.WriteToEditorPrefs();

                    await ExecuteDotnetAsync(new List<string>
                    {
                        k_VersionCommand
                    });
                    return true;
                }
                catch (Exception e)
                {
                    m_Logger.LogVerbose($"Error executing .NET: {e}");
                    return false;
                }
            }
        }

        public async Task<string> ExecuteDotnetAsync(IEnumerable<string> arguments = default, CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo(m_Preferences.DotnetPath, string.Join(" ", arguments))
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                m_Logger.LogVerbose($"Running dotnet with '{string.Join(" ", arguments ?? Array.Empty<string>())}'");
                var output = await m_ProcessRunner.RunAsync(startInfo, null, cancellationToken);
                if (output.ExitCode != 0)
                {
                    var(summary, reported) = ReportDiagnostics(DescribeCommand(arguments), output);
                    throw new DotnetCommandFailedException(summary, reported);
                }

                return output.StdOut;
            }
            catch (Win32Exception e)
            {
                m_Logger.LogVerbose($"Error {e}");
                throw new DotnetNotFoundException(e);
            }
            catch (Exception e)
            {
                m_Logger.LogVerbose($"Error {e}");
                throw;
            }
        }

        // One Console entry per diagnostic, so the Console can filter and count them. The raw output
        // stays at Verbose; the returned summary becomes the item's deploy status.
        (string Summary, bool Reported) ReportDiagnostics(string command, ProcessOutput output)
        {
            m_Logger.LogVerbose(
                $"{command} exited with {output.ExitCode}.\nstdout:\n{output.StdOut}\nstderr:\n{output.StdErr}");

            var combined = string.Join("\n", new[] { output.StdOut, output.StdErr });
            var diagnostics = MsBuildDiagnostics.Parse(combined);

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.Severity == SeverityLevel.Error)
                    m_Logger.LogError(diagnostic.Text);
                else
                    m_Logger.LogWarning(diagnostic.Text);
            }

            return (MsBuildDiagnostics.Summarize(command, output.ExitCode, diagnostics), diagnostics.Count > 0);
        }

        // Arguments arrive as one string per call, so the verb is its first token:
        // "publish \"/some/path.sln\" -c ..." -> "dotnet publish".
        static string DescribeCommand(IEnumerable<string> arguments)
        {
            var first = arguments?.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(first))
                return "dotnet";

            var verb = first.Trim().Split(' ')[0];
            return string.IsNullOrEmpty(verb) ? "dotnet" : $"dotnet {verb}";
        }

        public Task<List<SemVersion>> GetAvailableCoreRuntimes(CancellationToken ct = default)
        {
            return GetAvailableRuntimes("Microsoft.NETCore.App", ct);
        }

        public async Task<List<SemVersion>> GetAvailableRuntimes(string frameworkName, CancellationToken ct = default)
        {
            var executionResult = await ExecuteDotnetAsync(new[] {"--list-runtimes"}, ct);
            var versions = executionResult
                .Split("\n", StringSplitOptions.RemoveEmptyEntries)
                .Where(s => s.StartsWith(frameworkName))
                .Select(s =>
                {
                    s = s.Trim();
                    return SemVersion.ParseString(s);
                })
                .ToList();

            return versions;
        }
    }
}
