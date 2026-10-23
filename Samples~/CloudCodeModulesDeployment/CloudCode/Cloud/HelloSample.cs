using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Unity.Services.CloudCode.Core;

namespace HelloSample.Cloud
{
    // This class runs on Cloud Code, not on the device. Unity compiles it into the HelloSample
    // assembly and the Deployment window uploads that assembly as the module.
    //
    // Scope.Player keeps a separate copy of the public fields for each player, persisted between calls.
    // Scopes: https://docs.unity.com/en-us/cloud-code/3.0.0/modules/stateful-cloud-code/state-scopes
    [StateScope(Scope.Player)]
    public class HelloSample
    {
        readonly ILogger<HelloSample> m_Logger;

        public int GreetingCount;
        public string LastDelayedGreeting;
        public string PendingGreetingTimerId;

        public HelloSample(ILogger<HelloSample> logger)
        {
            m_Logger = logger;
        }

        // Each [CloudCodeFunction] represents an RPC: to call it, the client uses the generated HelloSampleClient,
        // e.g. await client.SayHello(name), which sends the request to Cloud Code and returns this method's result.
        [CloudCodeFunction("SayHello")]
        public string SayHello(string name)
        {
            GreetingCount++;
            m_Logger.LogInformation("SayHello for {Name}, greeting {Count}", name, GreetingCount);
            return $"Hello, {name}! You have been greeted {GreetingCount} time(s).";
        }

        // [CloudCodeService] asks Cloud Code to inject ITimerService; the caller never sends it.
        // The timer targets this player's instance, so DelayedGreeting runs on the same state.
        [CloudCodeFunction("ScheduleGreeting")]
        public async Task<string> ScheduleGreeting([CloudCodeService] ITimerService timers, string name, int delaySeconds)
        {
            // The scene clamps the delay too, but any HTTP client can call this endpoint.
            if (delaySeconds < 1 || delaySeconds > 3600)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Delay must be between 1 and 3600 seconds.");
            }

            // One pending greeting per player: scheduling again replaces the previous timer.
            if (PendingGreetingTimerId != null)
            {
                var previous = await timers.GetTimerAsync(PendingGreetingTimerId);
                previous.Cancel();
            }

            PendingGreetingTimerId = await timers.RegisterTimerAsync(
                TimeSpan.FromSeconds(delaySeconds),
                "DelayedGreeting",
                new Dictionary<string, object> { { "name", name } });
            LastDelayedGreeting = null;
            return PendingGreetingTimerId;
        }

        // Timer callbacks run as the service, not as a player, so only Cloud Code may call this.
        // Parameter names must match the dictionary keys passed to RegisterTimerAsync.
        [CloudCodeFunction("DelayedGreeting", Access.Service)]
        public void DelayedGreeting(string name)
        {
            GreetingCount++;
            LastDelayedGreeting = $"Hello, {name}! Greeted {GreetingCount} time(s), delivered at {DateTime.UtcNow:HH:mm:ss} UTC.";
            PendingGreetingTimerId = null;
            // No client is waiting on this call, so the log line is how you confirm the timer fired.
            m_Logger.LogInformation("DelayedGreeting delivered for {Name}, greeting {Count}", name, GreetingCount);
        }

        // A module cannot push to the client, so the scene polls this until the greeting lands.
        [CloudCodeFunction("GetGreetingStatus")]
        public async Task<string> GetGreetingStatus([CloudCodeService] ITimerService timers)
        {
            if (PendingGreetingTimerId != null)
            {
                var timer = await timers.GetTimerAsync(PendingGreetingTimerId);
                return $"Pending: {Math.Ceiling(timer.RemainingTime().TotalSeconds)} s left.";
            }

            return LastDelayedGreeting ?? "No delayed greeting yet.";
        }
    }
}
