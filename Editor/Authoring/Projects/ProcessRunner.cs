using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Projects
{
    interface IProcessRunner
    {
        Task<ProcessOutput> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default);
        bool Start(ProcessStartInfo startInfo);
    }

    struct ProcessOutput
    {
        public string StdOut { get; set; }
        public string StdErr { get; set; }
        public int ExitCode { get; set; }
    }

    class ProcessRunner : IProcessRunner
    {
        public async Task<ProcessOutput> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
        {
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stdOutBuilder = new StringBuilder();
            var stdErrBuilder = new StringBuilder();

            process.OutputDataReceived += (_, args) => stdOutBuilder.Append(args.Data);
            process.ErrorDataReceived += (_, args) => stdErrBuilder.Append(args.Data);

            await WaitForExitAsync(process, cancellationToken);

            return new ProcessOutput
            {
                ExitCode = process.ExitCode,
                StdOut = stdOutBuilder.ToString(),
                StdErr = stdErrBuilder.ToString()
            };
        }

        public bool Start(ProcessStartInfo startInfo)
        {
            using var process = new Process { StartInfo = startInfo };
            return process.Start();
        }

        static Task WaitForExitAsync(Process process, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object>();
            process.Exited += (s, e) => tcs.TrySetResult(null);

            if (cancellationToken != default)
                cancellationToken.Register(() => tcs.SetCanceled());

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }
    }
}
