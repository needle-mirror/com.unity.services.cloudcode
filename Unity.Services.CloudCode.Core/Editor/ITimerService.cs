#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Core
{
    public interface ITimerService
    {
        /// <summary>
        /// Register a one-shot timer that will invoke RunModule with the provided function name and params after the specified time span.
        /// </summary>
        /// <param name="timeSpan">The duration the timer should run before invoking the callback.</param>
        /// <param name="functionName">The name of the Cloud Code module function to invoke when the timer elapses.</param>
        /// <param name="paramsJson">Any parameters needed to invoke the designated function.</param>
        /// <returns>The ID of the registered timer. Can be used to fetch a reference to the timer.</returns>
        Task<string> Register(TimeSpan timeSpan, string functionName, Dictionary<string, object>? paramsJson = null);

        /// <summary>
        /// Fetches a reference to a registered timer using its ID. This can be used to check the remaining time or cancel the timer.
        /// </summary>
        /// <param name="timerId">The ID of the timer to fetch.</param>
        /// <returns>A reference to the Cloud Code Timer.</returns>
        Task<ICloudCodeTimer> Fetch(string timerId);
    }

    public interface ICloudCodeTimer
    {
        /// <summary>
        /// The ID of the timer.
        /// </summary>
        string TimerId { get; }

        /// <summary>
        /// Returns the time remaining before the timer elapses. If the timer has already elapsed, this should return a TimeSpan of zero.
        /// </summary>
        /// <returns>The time remaining on this timer.</returns>
        TimeSpan RemainingTime();

        /// <summary>
        /// Cancels the timer.
        /// </summary>
        /// <returns></returns>
        void Cancel();
    }
}
