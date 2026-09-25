using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.Core;
using UnityEngine;

namespace Unity.Services.CloudCode
{
    /// <summary>
    /// Readies the project's module host, the per-project process that runs every C# module and that the
    /// service calls the invoker, for the signed-in player ahead of the first module call, retrying with
    /// exponential backoff while it is still provisioning. One host serves every module, native or Cloud
    /// Behaviour, so there is one shared warm-up per project rather than one per module.
    /// </summary>
    /// <remarks>
    /// Every generated Cloud Behaviour client arms the shared <see cref="Default"/> from a load-time
    /// method, so in any build that contains one the warm-up runs at sign-in, however long before the
    /// first client is enabled. A project without behaviours arms it from its own startup code.
    /// <see cref="Arm"/> runs at once when a player is signed in and otherwise waits
    /// for services to initialize and a player to sign in, then stops listening: the invoker is per
    /// project, so one run per arm is enough whoever signs in later. A client on its own
    /// <see cref="IUnityServices"/> instance owns a private warm-up
    /// instead and resets it in <c>DisableClient()</c>. The scope handler checks <see cref="IsWarm"/>,
    /// joins the run through <see cref="Task"/> and treats a fault as terminal for that enable. A spent
    /// run disarms the instance, so the next <see cref="Arm"/> from a re-enabled client runs again.
    /// </remarks>
    public sealed class ModuleHostWarmup
    {
        internal const int k_MaxAttempts = 5;
        internal static readonly TimeSpan k_InitialDelay = TimeSpan.FromMilliseconds(500);

        static ModuleHostWarmup s_Default;

        /// <summary>
        /// The shared warm-up bound to <see cref="UnityServices.Instance"/>, armed at load by every
        /// generated client. Created on first use.
        /// </summary>
        public static ModuleHostWarmup Default => s_Default ??= new ModuleHostWarmup();

        /// <summary>
        /// Drops the shared instance so the next <see cref="Default"/> starts afresh. Runs at
        /// <c>SubsystemRegistration</c> on every Play Mode entry, because statics survive Fast Enter Play Mode.
        /// </summary>
        public static void ResetDefault()
        {
            s_Default?.Reset();
            s_Default = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDefaultOnLoad() => ResetDefault();

        // Test seam so the backoff can be driven without real time passing.
        internal Func<TimeSpan, CancellationToken, Task> Delay = System.Threading.Tasks.Task.Delay;

        readonly IUnityServices m_Services;
        CancellationTokenSource m_Cancellation;
        bool m_Armed;
        IUnityServices m_InitializingServices;
        IAuthenticationService m_Authentication;

        IUnityServices Services => m_Services ?? UnityServices.Instance;

        /// <summary>Whether a run has completed and an invoker answered. Cleared by <see cref="Reset"/>.</summary>
        public bool IsWarm { get; private set; }

        /// <summary>
        /// The current run, or <c>null</c> before a signed-in player has started one. Completes when
        /// an invoker has answered, faults with the last <see cref="CloudCodeException"/> once the
        /// attempts are spent, and is cancelled by <see cref="Reset"/>.
        /// </summary>
        public Task Task { get; private set; }

        /// <summary>Creates a warm-up bound to <paramref name="services"/>.</summary>
        /// <param name="services">
        /// The registry to resolve authentication and Cloud Code from, or <c>null</c> to use
        /// <see cref="UnityServices.Instance"/>.
        /// </param>
        public ModuleHostWarmup(IUnityServices services = null)
        {
            m_Services = services;
        }

        /// <summary>
        /// Arms the warm-up. Runs at once if services are initialized and a player is signed in;
        /// otherwise waits for both and runs then, once. Stays armed until <see cref="Reset"/>; a run
        /// that spends its attempts disarms the instance, so the next call from a re-enabled client
        /// runs again. Calling this on an armed instance does nothing. Called on <see cref="Default"/>
        /// before Core has created <see cref="UnityServices.Instance"/>, at <c>AfterAssembliesLoaded</c>,
        /// it logs a warning and does nothing: call it from that phase or later.
        /// </summary>
        public void Arm()
        {
            if (m_Armed)
                return;

            var services = Services;
            if (services == null)
            {
                Debug.LogWarning("[ModuleHostWarmup] Arm() was called before UnityServices.Instance exists and was ignored. Call it from AfterAssembliesLoaded or later.");
                return;
            }

            m_Armed = true;
            IsWarm = false;
            Task = null;

            if (services.State != ServicesInitializationState.Initialized)
            {
                m_InitializingServices = services;
                services.Initialized += OnServicesInitialized;
                return;
            }

            AttachToAuthentication(services);
        }

        /// <summary>
        /// Stops listening, cancels a run in flight and forgets its outcome, so the next
        /// <see cref="Arm"/> begins afresh.
        /// </summary>
        public void Reset()
        {
            StopListening();
            CancelRun();
        }

        void OnServicesInitialized()
        {
            var services = m_InitializingServices;
            if (services == null)
                return;

            m_InitializingServices = null;
            services.Initialized -= OnServicesInitialized;
            AttachToAuthentication(services);
        }

        void AttachToAuthentication(IUnityServices services)
        {
            m_Authentication = services.GetAuthenticationService();
            if (m_Authentication == null)
                return;

            if (m_Authentication.IsSignedIn)
            {
                OnSignedIn();
                return;
            }

            m_Authentication.SignedIn += OnSignedIn;
        }

        void OnSignedIn()
        {
            // One run per arm: the invoker is per project, so later sign-ins have nothing to add.
            DetachAuthentication();

            var cloudCode = Services.GetService<ICloudCodeWarmup>();
            if (cloudCode == null)
                return;

            m_Cancellation = new CancellationTokenSource();
            Task = RunAsync(cloudCode, m_Cancellation.Token);
        }

        void CancelRun()
        {
            m_Cancellation?.Cancel();
            m_Cancellation = null;
            Task = null;
            IsWarm = false;
        }

        void StopListening()
        {
            m_Armed = false;

            if (m_InitializingServices != null)
            {
                m_InitializingServices.Initialized -= OnServicesInitialized;
                m_InitializingServices = null;
            }

            DetachAuthentication();
        }

        void DetachAuthentication()
        {
            if (m_Authentication == null)
                return;

            m_Authentication.SignedIn -= OnSignedIn;
            m_Authentication = null;
        }

        async Task RunAsync(ICloudCodeWarmup cloudCode, CancellationToken token)
        {
            var delay = k_InitialDelay;
            for (var attempt = 1; attempt <= k_MaxAttempts; attempt++)
            {
                token.ThrowIfCancellationRequested();

                try
                {
                    await cloudCode.WarmInvokerAsync();
                    token.ThrowIfCancellationRequested();
                    IsWarm = true;
                    return;
                }
                catch (CloudCodeException e) when (e.Reason == CloudCodeExceptionReason.NotFound)
                {
                    // The host does not serve the warm route (the local debugger, an older gateway); module
                    // calls still work there, so skip the warm-up rather than fail the client.
                    return;
                }
                catch (CloudCodeException e)
                {
                    // Reset cannot recall a request in flight; a run it cancelled must not disarm the run that replaced it.
                    token.ThrowIfCancellationRequested();

                    var canRetry = attempt < k_MaxAttempts && IsRetryable(e);
                    if (!canRetry)
                    {
                        // A spent run means no invoker could be started; the next Arm() from a
                        // re-enabled client is the retry, so stop listening until then.
                        StopListening();
                        throw;
                    }
                }

                await Delay(delay, token);
                delay += delay;
            }
        }

        // 500/503 are provisioning failures, 422 an invoker timeout, and an unknown status is the
        // gateway cutting a long provision; a missing player, 401, 404 or 429 will not improve by waiting.
        static bool IsRetryable(CloudCodeException e) =>
            e.Reason == CloudCodeExceptionReason.ServiceUnavailable
            || e.Reason == CloudCodeExceptionReason.ScriptError
            || e.Reason == CloudCodeExceptionReason.NoInternetConnection
            || e.Reason == CloudCodeExceptionReason.Unknown;  // Gateway answers 504 - unknown for provisioning failure
    }
}
