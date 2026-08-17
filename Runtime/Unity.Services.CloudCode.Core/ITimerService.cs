#nullable enable
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Unity.Services.CloudCode.Core
{
    /// <summary>
    /// Service for registering and managing timers that invoke Cloud Code module functions after a specified delay.
    /// </summary>
    public interface ITimerService
    {
        /// <summary>
        /// Register a one-shot timer that will invoke RunModule with the provided function name and params after the specified time span.
        /// </summary>
        /// <param name="timeSpan">The duration the timer should run before invoking the callback.</param>
        /// <param name="functionName">The name of the Cloud Code module function to invoke when the timer elapses.</param>
        /// <param name="paramsJson">Any parameters needed to invoke the designated function.</param>
        /// <returns>The ID of the registered timer. Can be used to get a reference to the timer.</returns>
        Task<string> RegisterTimerAsync(TimeSpan timeSpan, string functionName, Dictionary<string, object>? paramsJson = null);

        /// <summary>
        /// Gets a reference to a registered timer using its ID. This can be used to check the remaining time or cancel the timer.
        /// </summary>
        /// <param name="timerId">The ID of the timer to get.</param>
        /// <returns>A reference to the Cloud Code Timer.</returns>
        Task<ICloudCodeTimer> GetTimerAsync(string timerId);

        // The obsolete members below forward to the ones above, so they can be deleted without
        // touching any implementation of this interface.

        /// <summary>
        /// Register a one-shot timer that will invoke RunModule with the provided function name and params after the specified time span.
        /// </summary>
        /// <param name="timeSpan">The duration the timer should run before invoking the callback.</param>
        /// <param name="functionName">The name of the Cloud Code module function to invoke when the timer elapses.</param>
        /// <param name="paramsJson">Any parameters needed to invoke the designated function.</param>
        /// <returns>The ID of the registered timer. Can be used to fetch a reference to the timer.</returns>
        [Obsolete("Renamed to RegisterTimerAsync for consistency with the rest of the API. The behaviour is unchanged.")]
        Task<string> Register(TimeSpan timeSpan, string functionName, Dictionary<string, object>? paramsJson = null)
            => RegisterTimerAsync(timeSpan, functionName, paramsJson);

        /// <summary>
        /// Fetches a reference to a registered timer using its ID. This can be used to check the remaining time or cancel the timer.
        /// </summary>
        /// <param name="timerId">The ID of the timer to fetch.</param>
        /// <returns>A reference to the Cloud Code Timer.</returns>
        [Obsolete("Renamed to GetTimerAsync for consistency with the rest of the API. The behaviour is unchanged.")]
        Task<ICloudCodeTimer> Fetch(string timerId)
            => GetTimerAsync(timerId);
    }

    /// <summary>
    /// Represents a registered timer that can be queried or cancelled.
    /// </summary>
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
        void Cancel();
    }
}
