using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Authoring.Editor.Projects
{
    interface IProcessRunner
    {
        Task<ProcessOutput> RunAsync(
            ProcessStartInfo startInfo,
            string stdIn = default,
            CancellationToken cancellationToken = default);
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
        public async Task<ProcessOutput> RunAsync(
            ProcessStartInfo startInfo,
            string stdIn = default,
            CancellationToken cancellationToken = default)
        {
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            var stdOutBuilder = new StringBuilder();
            var stdErrBuilder = new StringBuilder();

            process.OutputDataReceived += (_, args) => stdOutBuilder.AppendLine(args.Data);
            process.ErrorDataReceived += (_, args) => stdErrBuilder.AppendLine(args.Data);

            process.Start();
            if (!string.IsNullOrEmpty(stdIn))
            {
                await process.StandardInput.WriteAsync(stdIn);
                process.StandardInput.Close();
            }

            await WaitForExitAsync(process, cancellationToken);

            RemoveLastNewLineIfNecessary(stdOutBuilder);
            RemoveLastNewLineIfNecessary(stdErrBuilder);

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

        static void RemoveLastNewLineIfNecessary(StringBuilder stringBuilder)
        {
            if (stringBuilder.Length != 0)
            {
                stringBuilder.Remove(stringBuilder.Length - Environment.NewLine.Length, Environment.NewLine.Length);
            }
        }

        static Task WaitForExitAsync(Process process, CancellationToken cancellationToken = default)
        {
            var tcs = new TaskCompletionSource<object>();
            process.Exited += (s, e) => tcs.TrySetResult(null);

            if (cancellationToken != default)
                cancellationToken.Register(() => tcs.SetCanceled());

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }
    }
}
