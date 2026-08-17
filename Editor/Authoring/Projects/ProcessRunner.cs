using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.CloudCode.Authoring.Editor.Core.Logging;

namespace Unity.Services.CloudCode.Authoring.Editor.Projects
{
    interface IProcessRunner
    {
        Task<ProcessOutput> RunAsync(
            ProcessStartInfo startInfo,
            string stdIn = default,
            CancellationToken cancellationToken = default,
            TimeSpan timeout = default);

        /// <param name="onStandardErrorEnd">
        /// Invoked once redirected stderr reaches EOF. Because <see cref="Process.ErrorDataReceived"/>
        /// is asynchronous, a process can be observed as exited before its final lines have been
        /// delivered; callers that need everything it wrote must wait for this rather than for exit.
        /// </param>
        Process RunAsyncFireAndForget(
            ProcessStartInfo startInfo,
            Action<string> onStandardError = null,
            Action onStandardErrorEnd = null);
        bool Start(ProcessStartInfo startInfo);
        void Stop(int processId);
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
            cancellationToken.ThrowIfCancellationRequested();

            startInfo.RedirectStandardInput = true;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;

            using var process = new Process();
            var exitTask = WrapProcessInTask(process);

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
            await HandleExit(process, exitTask, timeout, cancellationToken);

            return new ProcessOutput
            {
                ExitCode = process.ExitCode,
                StdOut = stdOut.ToString(),
                StdErr = stdErr.ToString(),
            };
        }

        public Process RunAsyncFireAndForget(
            ProcessStartInfo startInfo,
            Action<string> onStandardError = null,
            Action onStandardErrorEnd = null)
        {
            // stdout is left inherited; stderr is only redirected when a handler is supplied, so a
            // caller can surface early/fatal launch output during startup without keeping a pipe
            // open for the process lifetime.
            var captureStandardError = onStandardError != null || onStandardErrorEnd != null;
            if (captureStandardError)
                startInfo.RedirectStandardError = true;

            var process = new Process();
            process.StartInfo = startInfo;
            process.EnableRaisingEvents = true;

            if (captureStandardError)
            {
                process.ErrorDataReceived += (sender, args) =>
                {
                    // A null Data is the EOF sentinel, raised once the stream closes.
                    if (args.Data == null)
                        onStandardErrorEnd?.Invoke();
                    else if (args.Data.Length > 0)
                        onStandardError?.Invoke(args.Data);
                };
            }

            process.Start();

            if (captureStandardError)
                process.BeginErrorReadLine();

            return process;
        }

        static async Task HandleExit(
            Process process,
            Task exitTask,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            exitTask.Start();
            var timeoutTask = Task.Delay(timeout == default ? TimeSpan.FromMinutes(5) : timeout, cancellationToken);
            var completed = await Task.WhenAny(exitTask, timeoutTask);
            if (completed != timeoutTask)
                return;

            process.Kill();
            cancellationToken.ThrowIfCancellationRequested();
        }

        public bool Start(ProcessStartInfo startInfo)
        {
            using var process = new Process();
            process.StartInfo = startInfo;
            return process.Start();
        }

        // No CancellationToken here: a Task built with one that is already cancelled is born completed,
        // and Start() then throws instead of running. Cancellation is handled around the wait instead.
        static Task WrapProcessInTask(Process process)
        {
            return new Task(process.WaitForExit);
        }

        public void Stop(int processID)
        {
            try
            {
                using var process = Process.GetProcessById(processID);
                if (process.HasExited)
                    return;

                process.Kill();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
    }
}
