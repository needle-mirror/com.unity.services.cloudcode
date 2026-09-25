using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Services.CloudCode.Core;
using Unity.Services.CloudCode.Shared;

namespace Unity.Services.CloudBehaviours
{
    /// <summary>
    /// An endpoint invocation that has been described but not yet executed. Run it with
    /// <see cref="ScheduleAsync"/> to defer it, or <see cref="ForScope"/> to invoke another scope
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
        Task ForScope(string scopeId);
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

        /// <inheritdoc cref="ICall.ForScope"/>
        /// <returns>The endpoint's return value.</returns>
        Task<T> ForScope(string scopeId);
    }

    /// <summary>
    /// Builds <see cref="ICall"/> descriptors and carries what they need to execute. Generated Cloud
    /// Behaviour clients hold one and use it to describe each endpoint.
    /// </summary>
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
        /// <param name="client">HTTP client for cross-scope calls. Only <see cref="ICall.ForScope"/> needs it.</param>
        /// <param name="configuration">API configuration. Only <see cref="ICall.ForScope"/> needs it.</param>
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

            // Checked in ForScope rather than here: scheduling needs neither, so a behaviour that only
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
                { "params", parameters },
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

        public async Task ForScope(string scopeId)
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

        public async Task<T> ForScope(string scopeId)
        {
            var response = await PostAsync(scopeId);
            return CloudCallTransport.ReadResult<T>(response, FunctionName);
        }
    }
}
