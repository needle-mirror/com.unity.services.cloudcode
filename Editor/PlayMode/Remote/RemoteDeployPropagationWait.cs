using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Editor.PlayMode
{
    class RemoteDeployPropagationWait
    {
        internal static readonly TimeSpan k_PropagationDelay = TimeSpan.FromSeconds(30);
        internal static readonly TimeSpan k_ProgressTick = TimeSpan.FromMilliseconds(250);

        internal Func<DateTime> UtcNow = () => DateTime.UtcNow;
        internal Func<TimeSpan, CancellationToken, Task> Delay = Task.Delay;

        internal static TimeSpan Remaining(DateTime anchorUtc, DateTime nowUtc)
        {
            var remaining = anchorUtc + k_PropagationDelay - nowUtc;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        // Progress must stay below 1, or a cancelled wait reads as completed instead of aborted.
        internal async Task WaitAsync(DateTime anchorUtc, Action<float> setProgress, CancellationToken cancellationToken)
        {
            var reported = 0f;
            while (true)
            {
                var remaining = Remaining(anchorUtc, UtcNow());
                if (remaining <= TimeSpan.Zero)
                    return;

                var progress = 1f - (float)(remaining.TotalSeconds / k_PropagationDelay.TotalSeconds);
                if (progress > reported)
                {
                    reported = progress;
                    setProgress(progress);
                }

                await Delay(remaining < k_ProgressTick ? remaining : k_ProgressTick, cancellationToken);
            }
        }
    }
}
