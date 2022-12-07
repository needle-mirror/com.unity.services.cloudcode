using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Shared.Infrastructure.SystemEnvironment;

namespace Unity.Services.CloudCode.Authoring.Editor.Projects
{
    class NodePackageManager : INodePackageManager, INodeJsRunner
    {
        const string k_Install = "install";
        const string k_Ci = "ci";
        const string k_Test = "test";
        readonly IEnumerable<string> k_Init = new[] { "init", "-y" };
        readonly IEnumerable<string> k_Run = new[] { "run", "--silent" };

        readonly IProcessRunner m_ProcessRunner;
        readonly string m_NodeJsPath;
        readonly string m_NpmPath;

        public string WorkingDirectory { get; set; } = Directory.GetCurrentDirectory();

        public NodePackageManager(IProcessRunner processRunner, IProjectSettings settings)
        {
            m_ProcessRunner = processRunner;
            m_NodeJsPath = settings.NodeJsPath;
            m_NpmPath = settings.NpmPath;
        }

        public Task Init(CancellationToken cancellationToken = default)
        {
            return NpmRun(k_Init, cancellationToken);
        }

        public Task Install(CancellationToken cancellationToken = default)
        {
            return NpmRun(new[] {k_Install}, cancellationToken);
        }

        public Task Ci(CancellationToken cancellationToken = default)
        {
            return NpmRun(new[] {k_Ci}, cancellationToken);
        }

        public Task Test(CancellationToken cancellationToken = default)
        {
            return NpmRun(new[] {k_Test}, cancellationToken);
        }

        public bool CanRunScript(string script)
        {
            var projectFilePath = GetProjectFilePath();

            if (!string.IsNullOrEmpty(projectFilePath))
            {
                return new NodeProject(projectFilePath).HasScript(script);
            }

            return false;
        }

        public Task<string> RunScript(string script, IEnumerable<string> arguments = default, CancellationToken cancellationToken = default)
        {
            var npmArguments = new List<string>(k_Run) { script, "--" };
            if (arguments != null)
            {
                npmArguments.AddRange(arguments);
            }
            return NpmRun(npmArguments, cancellationToken);
        }

        public async Task<string> ExecNodeJs(IEnumerable<string> arguments = default, CancellationToken cancellationToken = default)
        {
            var startInfo = new ProcessStartInfo(m_NodeJsPath, ProcessArguments.Join(arguments))
            {
                WorkingDirectory = WorkingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            try
            {
                EnsurePathContainsNodeAndNpm();

                var output = await m_ProcessRunner.RunAsync(startInfo, cancellationToken);
                if (output.ExitCode != 0)
                {
                    throw new NpmCommandFailedException(startInfo, output);
                }
                return output.StdOut;
            }
            catch (Win32Exception)
            {
                throw new NpmNotFoundException(m_NodeJsPath, m_NpmPath);
            }
        }

        Task<string> NpmRun(IEnumerable<string> arguments, CancellationToken cancellationToken)
        {
            var nodeArguments = new List<string> { m_NpmPath };
            nodeArguments.AddRange(arguments);

            return ExecNodeJs(nodeArguments, cancellationToken);
        }

        void EnsurePathContainsNodeAndNpm()
        {
            if (!SystemEnvironmentPathUtils.DoesEnvironmentPathContain(m_NpmPath))
                SystemEnvironmentPathUtils.AddProcessToPath(m_NpmPath);
            if (!SystemEnvironmentPathUtils.DoesEnvironmentPathContain(m_NodeJsPath))
                SystemEnvironmentPathUtils.AddProcessToPath(m_NodeJsPath);
        }

        string GetProjectFilePath()
        {
            var parentDir = WorkingDirectory;

            while (parentDir != null)
            {
                var path = Path.Join(parentDir, NodeProject.ProjectFile);

                if (File.Exists(path))
                {
                    return path;
                }

                parentDir = Directory.GetParent(parentDir)?.FullName;
            }

            return null;
        }
    }
}
