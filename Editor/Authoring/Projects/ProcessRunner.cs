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
            CancellationToken cancellationToken = default,
            TimeSpan timeout = default);
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
            CancellationToken cancellationToken = default,
            TimeSpan timeout = default)
        {
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;

            using var process = new Process();
            var exitTask = WrapProcessInTask(process, cancellationToken);

            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;
            var stdOut = new StringBuilder();
            process.OutputDataReceived += (sender, args) =>
            {
                stdOut.AppendLine(args.Data);
            };
            var stdErr = new StringBuilder();
            process.ErrorDataReceived += (sender, args) =>
            {
                stdErr.AppendLine(args.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!string.IsNullOrEmpty(stdIn))
            {
                await process.StandardInput.WriteAsync(stdIn);
                process.StandardInput.Close();
            }

            await HandleExit(process, exitTask, timeout);

            return new ProcessOutput
            {
                ExitCode = process.ExitCode,
                StdOut = stdOut.ToString(),
                StdErr = stdErr.ToString(),
            };
        }

        static async Task HandleExit(
            Process process,
            Task exitTask,
            TimeSpan timeout)
        {
            exitTask.Start();
            var timeoutTask = Task.Delay(timeout == default ? TimeSpan.FromMinutes(5) : timeout);
            var completed = await Task.WhenAny(exitTask, timeoutTask);
            if (completed == timeoutTask)
            {
                process.Kill();
            }
        }

        public bool Start(ProcessStartInfo startInfo)
        {
            using var process = new Process();
            process.StartInfo = startInfo;
            return process.Start();
        }

        static Task WrapProcessInTask(Process process, CancellationToken cancellationToken = default)
        {
            var t = new Task(process.WaitForExit);
            return t;
        }
    }
}
