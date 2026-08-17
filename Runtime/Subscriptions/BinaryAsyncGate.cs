using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Services.CloudCode.Subscriptions
{
    /// <summary>
    /// A single-permit async gate whose waiters resume through Unity's player loop instead of the .NET task
    /// scheduler.
    /// </summary>
    /// <remarks>
    /// WebGL players run with threads disabled, so a continuation posted to the thread pool never runs.
    /// <see cref="Awaitable"/> completions are raised by the engine and so are unaffected. Not thread-safe:
    /// it is only selected for WebGL, which is single-threaded.
    /// </remarks>
    class BinaryAsyncGate
    {
        readonly Queue<AwaitableCompletionSource> m_Waiters = new Queue<AwaitableCompletionSource>();
        bool m_Held;

        /// <summary>
        /// Takes the permit, or queues until whoever holds it releases.
        /// </summary>
        /// <returns>An awaitable that completes once the caller holds the permit.</returns>
        internal Awaitable WaitAsync()
        {
            var completion = new AwaitableCompletionSource();
            if (m_Held)
            {
                m_Waiters.Enqueue(completion);
                return completion.Awaitable;
            }

            m_Held = true;
            completion.SetResult();
            return completion.Awaitable;
        }

        /// <summary>
        /// Hands the permit to the longest-waiting caller, or frees it when nobody is waiting.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the permit was not held.</exception>
        internal void Release()
        {
            if (!m_Held)
            {
                throw new InvalidOperationException($"Released a {nameof(BinaryAsyncGate)} that was not held.");
            }

            if (m_Waiters.Count == 0)
            {
                m_Held = false;
                return;
            }

            // The permit transfers straight to the next waiter, so it stays held.
            m_Waiters.Dequeue().SetResult();
        }
    }
}
