using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// An endpoint invocation that has been described but not yet executed. Run it with
    /// <see cref="ScheduleAsync"/> to defer it, or <see cref="ForScopeAsync"/> to invoke another scope
    /// instance now. The two are alternatives; a descriptor given to neither does nothing.
    /// </summary>
    public interface ICall
    {
        /// <summary>
        /// Registers a timer that invokes this endpoint after <paramref name="delay"/>, on the current
        /// scope instance. The task covers registering the timer, not waiting for it to elapse.
        /// </summary>
        /// <remarks>
        /// The timer is always registered against the scope instance that is executing, so scheduling
        /// another behaviour's endpoint only lands somewhere meaningful when that behaviour shares the
        /// caller's <c>[StateScope]</c>.
        /// </remarks>
        /// <param name="delay">How long to wait before invoking. Must be greater than zero.</param>
        /// <returns>The registered timer's ID.</returns>
        Task<string> ScheduleAsync(TimeSpan delay);

        /// <summary>
        /// Invokes this endpoint immediately against a different instance of the target's scope.
        /// </summary>
        /// <param name="scopeId">
        /// A player ID or multiplayer session ID, matching the target behaviour's <c>[StateScope]</c>.
        /// </param>
        /// <returns>A task that completes once the endpoint has run.</returns>
        Task ForScopeAsync(string scopeId);
    }

    /// <summary>
    /// An endpoint invocation that has been described but not yet executed, returning
    /// <typeparamref name="T"/> when invoked. See <see cref="ICall"/>.
    /// </summary>
    /// <typeparam name="T">The endpoint's return type.</typeparam>
    public interface ICall<T>
    {
        /// <inheritdoc cref="ICall.ScheduleAsync"/>
        Task<string> ScheduleAsync(TimeSpan delay);

        /// <inheritdoc cref="ICall.ForScopeAsync"/>
        /// <returns>The endpoint's return value.</returns>
        Task<T> ForScopeAsync(string scopeId);
    }

    /// <summary>
    /// Builds <see cref="ICall"/> descriptors and carries what they need to execute. Generated Cloud
    /// Behaviour clients hold one and use it to describe each endpoint.
    /// </summary>
    /// <remarks>
    /// Supports generated code only and is not part of the public API: its shape can change in any
    /// release. Call endpoints through the generated <c>I{Behaviour}</c> interface instead.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class CloudCallTransport
    {
        /// <summary>Wire value for <c>[StateScope(Scope.Player)]</c>.</summary>
        public const string PlayerScope = "player";

        /// <summary>Wire value for <c>[StateScope(Scope.MultiplayerSession)]</c>.</summary>
        public const string MultiplayerSessionScope = "multiplayerSession";

        readonly IExecutionContext _ctx;
        readonly IApiClient _client;
        readonly IApiConfiguration _configuration;
        readonly ITimerService _timers;
        readonly string _moduleName;
        readonly string _behaviourName;

        /// <summary>Initializes the transport.</summary>
        /// <param name="ctx">The current execution context.</param>
        /// <param name="client">HTTP client for cross-scope calls. Only <see cref="ICall.ForScopeAsync"/> needs it.</param>
        /// <param name="configuration">API configuration. Only <see cref="ICall.ForScopeAsync"/> needs it.</param>
        /// <param name="timers">Timer service, required for <see cref="ICall.ScheduleAsync"/>.</param>
        /// <param name="moduleName">The deployed module the target endpoints belong to.</param>
        /// <param name="behaviourName">The behaviour these endpoints belong to, used in error messages.</param>
        public CloudCallTransport(
            IExecutionContext ctx,
            IApiClient client,
            IApiConfiguration configuration,
            ITimerService timers,
            string moduleName,
            string behaviourName)
        {
            _ctx = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _timers = timers ?? throw new ArgumentNullException(nameof(timers));
            _moduleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));
            _behaviourName = behaviourName ?? throw new ArgumentNullException(nameof(behaviourName));

            // Checked in ForScopeAsync rather than here: scheduling needs neither, so a behaviour that only
            // defers calls should not require the cross-scope registration to construct.
            _client = client;
            _configuration = configuration;
        }

        /// <summary>Describes a call to a void endpoint.</summary>
        /// <param name="functionName">The target endpoint's deployed function name.</param>
        /// <param name="scopeType">
        /// The target behaviour's scope, as <see cref="PlayerScope"/> or <see cref="MultiplayerSessionScope"/>;
        /// <c>null</c> when it declares no <c>[StateScope]</c>.
        /// </param>
        /// <param name="operation">The operation name reported with the request, for diagnostics.</param>
        /// <param name="parameters">The endpoint's arguments, keyed by parameter name.</param>
        /// <returns>The described call, not yet executed.</returns>
        public ICall Call(string functionName, string scopeType, string operation, Dictionary<string, object> parameters)
        {
            return new CloudCall(this, functionName, scopeType, operation, parameters);
        }

        /// <summary>Describes a call to an endpoint returning <typeparamref name="T"/>.</summary>
        /// <typeparam name="T">The endpoint's return type.</typeparam>
        /// <param name="functionName">The target endpoint's deployed function name.</param>
        /// <param name="scopeType">
        /// The target behaviour's scope, as <see cref="PlayerScope"/> or <see cref="MultiplayerSessionScope"/>;
        /// <c>null</c> when it declares no <c>[StateScope]</c>.
        /// </param>
        /// <param name="operation">The operation name reported with the request, for diagnostics.</param>
        /// <param name="parameters">The endpoint's arguments, keyed by parameter name.</param>
        /// <returns>The described call, not yet executed.</returns>
        public ICall<T> Call<T>(string functionName, string scopeType, string operation, Dictionary<string, object> parameters)
        {
            return new CloudCall<T>(this, functionName, scopeType, operation, parameters);
        }

        /// <summary>
        /// Starts the call chain for an endpoint about to run, so that a <see cref="ICall.ForScopeAsync"/> call back
        /// into a scope waiting upstream fails at once instead of blocking until the request times out.
        /// </summary>
        /// <param name="upstream">The raw <c>__cloudCallChain</c> parameter, or <c>null</c> when the call came from a game or a timer.</param>
        /// <param name="scopeType">The scope type of the endpoint's behaviour, as <see cref="PlayerScope"/> or <see cref="MultiplayerSessionScope"/>.</param>
        /// <param name="scopeId">The id of the scope the endpoint is running in.</param>
        public static void EnterCallChain(string[] upstream, string scopeType, string scopeId)
            => CloudCallChain.Enter(upstream, scopeType, scopeId);

        internal Task<string> RegisterAsync(TimeSpan delay, string functionName, Dictionary<string, object> parameters)
        {
            return _timers.RegisterTimerAsync(delay, functionName, parameters);
        }

        /// <summary>
        /// Reads an endpoint's return value out of the response envelope, reporting a missing or
        /// unreadable payload against the endpoint rather than as a null-reference or JSON error.
        /// </summary>
        internal static T ReadResult<T>(JObject response, string functionName)
        {
            var value = response ? ["output"] ? ["value"];
            if (value == null)
            {
                throw new InvalidOperationException(
                    $"'{functionName}' returned a response with no output value, so its result could not be "
                    + "read. The deployed module may not match the bindings this caller was generated against.");
            }

            try
            {
                return value.ToObject<T>();
            }
            catch (Exception e) when (e is JsonException or ArgumentException or FormatException
                                      or InvalidCastException or OverflowException)
            {
                // Newtonsoft picks its exception type from the conversion path it takes, so the type says
                // nothing useful; every one of them means the payload does not fit T.
                throw new InvalidOperationException(
                    $"'{functionName}' returned a value that could not be read as {typeof(T)}. The deployed "
                    + "module may not match the bindings this caller was generated against.", e);
            }
        }

        internal async Task<JObject> PostAsync(
            string functionName,
            string scopeType,
            string operation,
            Dictionary<string, object> parameters,
            string scopeId)
        {
            if (string.IsNullOrEmpty(scopeId))
            {
                throw new ArgumentException("A scope ID is required to call another scope instance.", nameof(scopeId));
            }

            if (string.IsNullOrEmpty(scopeType))
            {
                throw new InvalidOperationException(
                    $"Cannot call '{functionName}' on another scope: its behaviour declares no [StateScope]. "
                    + "Ensure that behaviour has a [StateScope(...)] attribute defined.");
            }

            // The invoker is not reentrant, so re-entering the scope already executing blocks until the
            // request times out instead of failing.
            if (string.Equals(scopeId, _ctx.ScopeId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Cannot call '{functionName}' on scope '{scopeId}': that is the scope this execution is "
                    + "already running in. Call the method directly rather than through the behaviour interface.");
            }

            CloudCallChain.Check(scopeType, scopeId, functionName);

            if (_client == null || _configuration == null)
            {
                throw new InvalidOperationException(
                    $"Cannot call '{functionName}' on another scope: no IApiClient/IApiConfiguration is "
                    + $"registered. Call Add{_behaviourName}Client() from your ICloudCodeSetup.");
            }

            var payload = new Dictionary<string, object>()
            {
                {
                    "scope", new Dictionary<string, object>()
                    {
                        { "type", scopeType },
                        { "id", scopeId },
                    }
                },
                { "params", CloudCallChain.WithChain(parameters) },
            };

            var options = new ApiRequestOptions(_ctx, _ctx.ServiceToken)
            {
                Data = payload,
                Operation = operation,
            };
            options.HeaderParameters.Add("Content-Type", "application/json");
            options.HeaderParameters.Add("Accept", "application/json");

            var response = await _client.PostAsync<JObject>(
                $"/v1/projects/{_ctx.ProjectId}/modules/{_moduleName}/{functionName}",
                options,
                _configuration);

            return response.Data;
        }
    }

    abstract class CloudCallBase
    {
        readonly CloudCallTransport _transport;
        readonly string _scopeType;
        readonly string _operation;
        readonly Dictionary<string, object> _parameters;

        internal CloudCallBase(
            CloudCallTransport transport,
            string functionName,
            string scopeType,
            string operation,
            Dictionary<string, object> parameters)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            FunctionName = functionName;
            _scopeType = scopeType;
            _operation = operation;
            _parameters = parameters;
        }

        protected string FunctionName { get; }

        public Task<string> ScheduleAsync(TimeSpan delay)
        {
            return _transport.RegisterAsync(delay, FunctionName, _parameters);
        }

        protected Task<JObject> PostAsync(string scopeId)
        {
            return _transport.PostAsync(FunctionName, _scopeType, _operation, _parameters, scopeId);
        }
    }

    sealed class CloudCall : CloudCallBase, ICall
    {
        internal CloudCall(
            CloudCallTransport transport,
            string functionName,
            string scopeType,
            string operation,
            Dictionary<string, object> parameters)
            : base(transport, functionName, scopeType, operation, parameters) {}

        public async Task ForScopeAsync(string scopeId)
        {
            await PostAsync(scopeId);
        }
    }

    sealed class CloudCall<T> : CloudCallBase, ICall<T>
    {
        internal CloudCall(
            CloudCallTransport transport,
            string functionName,
            string scopeType,
            string operation,
            Dictionary<string, object> parameters)
            : base(transport, functionName, scopeType, operation, parameters) {}

        public async Task<T> ForScopeAsync(string scopeId)
        {
            var response = await PostAsync(scopeId);
            return CloudCallTransport.ReadResult<T>(response, FunctionName);
        }
    }

    /// <summary>
    /// Detects and prevents deadlocks between scopes that call each other with <see cref="ICall.ForScopeAsync"/>. A scope
    /// runs one call at a time, so a call back into a scope that is still waiting for this one could never start. The
    /// chain records which scopes are waiting upstream of the running endpoint, so such a call fails at once instead of
    /// blocking until the request times out.
    /// </summary>
    /// <remarks>
    /// Each generated endpoint calls <see cref="CloudCallTransport.EnterCallChain"/>, which stores a call path for that
    /// request's async flow. Each <see cref="ICall.ForScopeAsync"/> checks its target against the call path and sends the
    /// path's scopes to the next endpoint as the reserved <c>__cloudCallChain</c> parameter, as <c>type:id</c> entries
    /// ordered oldest to newest. Kept in the package, so every module in a .ccm shares one call path.
    /// </remarks>
    static class CloudCallChain
    {
        internal const string k_ParameterName = "__cloudCallChain";
        const int k_MaxUpstreamEntries = 16;

        /// <summary>What an endpoint knows about its call: the scopes waiting upstream and its own scope.</summary>
        sealed class CallPath
        {
            internal CallPath(string[] upstream, string current)
            {
                Upstream = upstream;
                Current = current;
            }

            /// <summary>The scopes waiting for this endpoint, ordered oldest to newest.</summary>
            internal string[] Upstream { get; }

            /// <summary>This endpoint's own scope, as <c>type:id</c>.</summary>
            internal string Current { get; }
        }

        static readonly AsyncLocal<CallPath> s_Current = new AsyncLocal<CallPath>();

        /// <summary>
        /// Starts the call path for the endpoint that is about to run: the scopes waiting upstream, taken from the request,
        /// and the endpoint's own scope. It replaces any call path already in this flow, because each request starts from
        /// the chain it was sent. The call path belongs to that endpoint's flow and is gone once it returns.
        /// </summary>
        /// <param name="upstream">The raw <c>__cloudCallChain</c> parameter, or <c>null</c> when the call came from a game or a timer. It is validated before use.</param>
        /// <param name="scopeType">The scope type of the endpoint's behaviour.</param>
        /// <param name="scopeId">The id of the scope the endpoint is running in.</param>
        internal static void Enter(string[] upstream, string scopeType, string scopeId)
        {
            s_Current.Value = new CallPath(KeepNewestWellFormedEntries(upstream), Key(scopeType, scopeId));
        }

        /// <summary>The wire form of a scope, <c>type:id</c>.</summary>
        static string Key(string scopeType, string scopeId) => scopeType + ":" + scopeId;

        /// <summary>
        /// Reads the chain a request carried. It arrives as an ordinary parameter, so it is only ever trusted to
        /// refuse calls, never to allow them: malformed entries are dropped and only the newest
        /// <c>k_MaxUpstreamEntries</c> are kept. Returns the entries oldest to newest.
        /// </summary>
        static string[] KeepNewestWellFormedEntries(string[] upstream)
        {
            if (upstream == null || upstream.Length == 0)
            {
                return Array.Empty<string>();
            }

            // Entries run oldest to newest: take the newest valid ones, so the oldest are dropped past the limit.
            var newestFirst = Enumerable.Reverse(upstream).Where(IsWellFormed).Take(k_MaxUpstreamEntries);
            return Enumerable.Reverse(newestFirst).ToArray();
        }

        /// <summary>Whether an entry is <c>type:id</c> with a non-empty id and a known scope type.</summary>
        static bool IsWellFormed(string entry)
        {
            var separator = entry == null ? -1 : entry.IndexOf(':');
            if (separator <= 0 || separator == entry.Length - 1)
            {
                return false;
            }

            var scopeType = entry.Substring(0, separator);
            return scopeType == CloudCallTransport.PlayerScope || scopeType == CloudCallTransport.MultiplayerSessionScope;
        }

        /// <summary>
        /// Throws when the target scope is waiting upstream, because it cannot take a call while it waits for this one.
        /// An unawaited call is checked too: at call time there is no telling whether it will be awaited.
        /// Does nothing outside an endpoint.
        /// </summary>
        /// <param name="scopeType">The scope type of the call's target.</param>
        /// <param name="scopeId">The id of the call's target scope.</param>
        /// <param name="functionName">The endpoint being called, for the error message.</param>
        /// <exception cref="InvalidOperationException">The target scope is waiting upstream.</exception>
        internal static void Check(string scopeType, string scopeId, string functionName)
        {
            var path = s_Current.Value;
            if (path == null)
            {
                return;
            }

            var target = Key(scopeType, scopeId);
            if (Array.IndexOf(path.Upstream, target) >= 0)
            {
                var cycle = Enumerable.Concat(path.Upstream, new[] { path.Current, target });
                throw new InvalidOperationException(
                    $"ForScopeAsync cycle: {string.Join(" → ", cycle)}. Cannot call '{functionName}' on {target}: "
                    + "it is earlier in this chain of calls, and a scope runs one call at a time, so calling back into it "
                    + "is not supported, even if the earlier call was not awaited. "
                    + "Return the result to the caller instead of calling back into it.");
            }
        }

        /// <summary>
        /// Returns the parameters with the chain added for the next endpoint: the upstream scopes, then this
        /// endpoint's own scope. It returns a copy, so the caller's parameters stay unchanged and can be reused, for
        /// example by <c>ScheduleAsync</c>, which sends no chain. Outside an endpoint it returns the parameters as they are.
        /// </summary>
        /// <param name="parameters">The endpoint parameters the author's code supplied.</param>
        internal static Dictionary<string, object> WithChain(Dictionary<string, object> parameters)
        {
            var path = s_Current.Value;
            if (path == null)
            {
                return parameters;
            }

            var chain = new string[path.Upstream.Length + 1];
            path.Upstream.CopyTo(chain, 0);
            chain[path.Upstream.Length] = path.Current;
            return new Dictionary<string, object>(parameters) { [k_ParameterName] = chain };
        }
    }
}
