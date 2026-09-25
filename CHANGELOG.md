# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [3.1.0-exp.1] - 2026-09-25
### Added
- Cloud Behaviour data types now capture public properties with a public getter and setter, in addition to fields. Readable but unassignable properties report CCSG131; unreadable ones (write-only, or a non-public getter) are excluded silently.
- Add creation flow for Cloud Behaviour Scripts through the Unity Editor
- Cloud Behaviours supports `[CloudCodeSerializeProperty]` and `[CloudCodeIgnoreProperty]` on fields to control state persistence.
- Cloud Behaviours supports the `[CloudCodeAccess(Access)]` attribute to restrict who can access which endpoints, as in Stateful Cloud Code.
- Cloud Behaviours supports `struct` data types for endpoint parameters and returns, synchronized fields, and event payloads.
- Cloud Behaviours: new `[SyncView("wireName")]` attribute for per-viewer synchronized projections — a `TWire Method(string playerId)` whose value is computed per recipient, so each player receives their own view of the shared state.
- Cloud Behaviours supports timers and cross-scope requests via the generated `I{Behaviour}` interface. Once injected, in the calling function, it can be used with `ScheduleAsync()` to schedule a deferred execution of that function, or `ForScope()` to invoke a function in a different scope than the current one.
- Added `ModuleHostWarmup`, which warms the project's module host at sign-in so the first module call is not delayed by provisioning. Generated Cloud Behaviour clients arm it at load and await it before hydrating; a failed warm-up raises `Fatal` with the new `InvokerUnavailable` reason.
- Added `CloudBehaviourErrorReason.MessageError`: a generated client raises `Error` with it when a synchronization or push-event message cannot be read or applied. The message is dropped and `MessageReceived` is not raised for it.

- Add a Cloud Code observability logs window (`Services > CloudCode > Observability Logs`) to query the logs your scripts and modules emit
- Cloud Code data types authored natively in-editor now capture public properties with a public getter and setter, in addition to fields. Readable but unassignable properties report CCSG216; unreadable ones (write-only, or a non-public getter) are excluded silently.
- A module whose source changed is now redeployed onto the running local Cloud Code server automatically, instead of having to be redeployed by hand, so picking up a code change no longer requires stopping and starting the server. Each redeploy names the modules the server reloaded in the Console. Module references are redeployed when the Editor regains focus, including while in play mode. Cloud Code Modules are redeployed after Unity recompiles them, outside play mode only; changing one while playing logs which modules need play mode to be re-entered.
- Cloud Code push-message subscriptions now support multiple subscribers on the same channel, each with its own callbacks and an independent unsubscribe.
- Entering Play mode without the local Cloud Code server running now logs a warning when the remote environment is stale relative to local module source.
- The Cloud Code Module Reference (.ccmr) inspector now shows a last successful deployment summary, matching the Cloud Code Module (.ccmu) inspector. The summary reads the editor session's per-user deployment record, so it survives domain reloads and follows the active project and environment.
- Module References (.ccmr) now show "Modified locally, deploy to update" in the Deployment window when their external solution changed since the last recorded deploy, instead of a status derived from the .ccmr asset file's own timestamp.
- Editor analytics now cover the Cloud Code authoring flows: creating, deploying and publishing JS scripts; creating and deploying module references (`.ccmr`) and modules (`.ccmu`); generating solutions and bindings; and the local server lifecycle. Automatic local redeploys are not reported. Collection follows the Editor's own analytics setting, and no script or module source is sent.

### Changed
- New Cloud Behaviour scripts are now created with `[StateScope(Scope.Player)]` instead of `[StateScope(Scope.MultiplayerSession)]`, and carry a comment linking to the scope documentation.
- Generated Cloud Behaviour data types are emitted into their own namespace, with nested types kept inside their containing type instead of flattened into the client's namespace. Data types that share a simple name across different namespaces are now generated separately rather than one overwriting the other.
- Player scoped Cloud Behaviours now send events.
- Cloud Behaviours now support sharing code via a third asmdef.

- Cloud Code Modules now support sharing code via a third asmdef.
- A running Cloud Code local debug session no longer redirects every other Wire consumer to the debugger. Cloud Code now opens its own connection to the debug server through `IWireFactory`, so Multiplayer, Friends and the editor status service stay connected to their environment while debugging.
- Local Cloud Code debugging can now be enabled in a development player build by launching it with `--cloud-code-local-debugger[=<port>]`. The argument is ignored in release builds.

### **Breaking Changes**:
- Generated Cloud Behaviour clients no longer mark synchronized members with `[field: SerializeField]`. Unity's serializer cannot represent most backend-legal types, so per-member serialization silently persisted only a subset of the synchronized state. The custom Inspector is unaffected (it reads live members directly).
- `readonly` fields on Cloud Behaviour data types are no longer captured and now report CCSG131.
- `HydrationFailedOnConnect` moved from `Error` to `Fatal` as a `CloudBehaviourExceptionReason`: a hydrate that fails when the scope resolves disables the client and raises no `ScopeChanged`. Call `EnableClient()` from outside the handler to try again.
- Cloud Behaviours: removed the `[VisibilityFilter]` attribute. Per-viewer synchronization is now expressed with `[SyncView]` projection methods (see Added); migrate a filtered field to a projection that returns the value each viewer should see.

- `readonly` fields on Cloud Code data types authored natively in-editor are no longer captured and now report CCSG216.

### Fixed
- Cloud Behaviours now report error CCSG156 when saved state has two members with the same name, such as a field hidden with `new` in a data type. The server can't save that state, so every call to the behaviour failed.
- Cloud Behaviours now report error CCSG157 when a behaviour implements `IStateSerializer`, which Cloud Behaviours don't support yet. Its `OnSerialize` and `OnDeserialize` were never called, and state was saved with the default serializer.
- Cloud Behaviour data types now warn (CCSG212 / CCSG105) when a derived declaration hides an inherited public member out of the generated client type.
- Cloud Behaviour script creation now surfaces clear error dialogs, matching the Cloud Code Module creation flow.
- Generated Cloud Behaviour client bindings now compile when the cloud class name matches the module name; the client's `[CloudBehaviour(typeof(...))]` is globally qualified so the module namespace can't shadow the class.
- Cloud Behaviours now resolve client-to-cloud bindings in player builds from a generated binding manifest instead of scanning module manifests by simple type name. This fixes binding failures when two modules declare behaviours with the same simple type name; each client now resolves to the correct behaviour by full type identity.
- A Cloud Behaviour exposing an enum backed by `ulong` with a value above `long.MaxValue` no longer silently fails to generate a module manifest.
- Cloud Behaviour events declared with `Action<T>` now deliver their payload type to the generated client; previously only `EventHandler<T>` payloads were emitted, so an `Action<T>` payload had no client-side type.
- Cloud Behaviours now generate enum bindings that preserve the enum's underlying type, matching the fix already made for Cloud Code Modules authored natively in-editor.
- A Cloud Behaviour event push that fails to send is now caught and logged server-side instead of being silently swallowed as an unobserved task exception. Synchronizing before any endpoint has run (e.g. manually during hydration) no longer risks a `NullReferenceException` and is skipped instead.
- `CloudBehaviour.SynchronizeAsync()` now throws an `InvalidOperationException` explaining that no synchronizer is attached, instead of returning a null `Task` that failed the caller's `await` with an opaque `NullReferenceException`.
- A local Cloud Code server stop that fails now records the failure, as a start already did, so it is visible rather than only logged, and the failure shown always belongs to the last start or stop. The log line for a failed stop no longer claims the start failed.
- In a project using Multiplayer Play Mode, a local Cloud Code server start or stop issued just after a domain reload is no longer repeated by the deferred restore of the previous session. The restore runs a moment after the reload there, and it read the new operation's state as an interrupted one to resume, running a second start or stop alongside it.
- The local Cloud Code server now logs a warning when a running local session is not restored after a domain reload or an Editor restart, naming the reason and stating that play mode has returned to the deployed service. Previously the only trace was a process id of -1 in the local server status. A start that never launched is not warned about, since its recorded failure outlives the reload and would otherwise be reported again on every later one; it stays readable in the local server status.
- Cloud Code now logs an error when local debugging is active but no `IWireFactory` is registered, instead of silently subscribing push messages against the cloud, which rejects the local server's channel tokens.
- Clearing the local Cloud Code server's state now deletes the state the server actually writes. It targeted `Orleans/GrainState/v1`, a path the server stopped using, so the Cloud Code toolbar's clear-state action left every scope's persisted state in place.

- Cloud Code data types now warn (CCSG212 / CCSG105) when a derived declaration hides an inherited public member out of the generated client type.
- Fixed bug ensuring that the local debugger only starts when valid Unity Services environemnts are set.
- Each compiler diagnostic from a module that fails to build is now its own Console entry, at its own severity, instead of one entry holding the whole `dotnet publish` transcript. The full output is still available with Cloud Code verbose logging enabled.
- Building a Cloud Code Module no longer emits the .NET SDK warning about a solution-level output path (NETSDK1194). The published output is unchanged.
- Opening a Cloud Code script no longer logs `JsAssetHandler:OpenAsset (int,int) does not match any of [OnOpenAssetAttribute] expected signatures` on Unity 6000.4 and newer. The asset-open handler now takes an `EntityId` and resolves the asset from it, so Cloud Code scripts open in the configured external editor again.
- Cloud Code requests made against a local debug server no longer time out after 30 seconds. The timeout override existed but was never applied.

## [3.0.0-exp.9] - 2026-08-17
### Added
- Updated the Cloud Code Module (.ccmu) inspector with deployment actions and a last successful deployment summary.
- Support default parameters in generated bindings for Cloud Code modules scripts.
- The local Cloud Code server settings can now create and assign a placeholder secrets JSON file, and open the assigned file in your external editor.

### Changed
- The package now targets Unity 6000.0. The local debugger requires Unity 6000.3 or newer, and Cloud Code Module authoring requires Unity 6000.5 or newer.
- The local Cloud Code server now defaults to port `14750` instead of `5000` by default. Configurable in the `CloudCodeLocalServerSettings` asset. Existing projects keep the port already stored in that asset.
- The local Cloud Code server's infrastructure now defaults to non-verbose logging. This has been made configurable on the `CloudCodeLocalServerSettings` asset.
- The local Cloud Code server's secrets file is now validated for parseable JSON object content and surfaces an error dialog when invalid.
- The local Cloud Code server settings can now create and assign a placeholder secrets JSON file, and open the assigned file in your external editor.
- New Cloud Code Module scripts are now created with `[StateScope(Scope.Player)]` instead of `[StateScope(Scope.MultiplayerSession)]`, and carry a comment linking to the scope documentation.
- Cloud Code Modules now report a compile-time error when a function's return type, a parameter, or a data type field is a custom collection type (a user type implementing `IEnumerable<T>` other than `List<T>`, an array, or `Dictionary<,>`), which can't be serialized to or from JSON. Use `List<T>` or `T[]` instead.

### **Breaking Changes**:
- CloudCodeModuleScope renamed to CloudCodeScope and moved to the main Unity.Services.CloudCode namespace.

### Fixed
- Cloud Code Modules (`.ccmu`) no longer show "Modified locally" after routine re-imports (entering Play Mode, domain reloads, or reimporting) when their source is unchanged. A module's status is now derived from its content compared to the last deployment, and a remotely-deployed module keeps its status regardless of the local server's state.
- Cloud Code Modules authored natively in-editor now generate enum bindings that preserve the enum's underlying type (`byte`, `short`, `long`, `ulong`, etc.) instead of always emitting `int`, which truncated or failed to compile values outside `int` range. (The separate module-reference `.ccmr` bindings generator is unchanged.)
- The local debug server now starts on Linux with default preferences. The default .NET path pointed at the `/usr/share/dotnet` directory instead of an executable; it now resolves `dotnet` from the system PATH, with a fallback if the configured path is invalid.
- A local Cloud Code server that dies during startup now reports why. It also checks two things up front on start:
  - Port conflicts for any ports the local server needs.
  - The .NET runtime the server needs.
- Fixed a `NullReferenceException` when deploying a Cloud Code Module after a domain reload.

### Dependencies
- Cloud Code Module authoring now targets Cloud Code Core `v0.0.7` (was `v0.0.4`) and Cloud Code APIs `v0.0.27` (was `v0.0.26`). The changes to these packages are listed below.

#### Cloud Code Core `v0.0.7`
- **Breaking**: Redefined the Cloud Code `Access` enum. The values are renamed and renumbered,
  and default is now `Access.Unspecified`. Validation will be done at load time and incompatible values will throw an exception.
- **Breaking**: Renamed `[Service]` attribute to `[CloudCodeService]` to avoid confusion with other service attributes.
- **Breaking**: Renamed `ITimerService.RegisterTimerAsync` and `ITimerService.GetTimerAsync`, for consistency. The previous `Register` and `Fetch` names still work but are now obsolete.

#### Cloud Code APIs `v0.0.27`
- Added `AddAdminApiClient()` extension method to register the Admin API client through dependency injection, matching `AddGameApiClient()`.

## [3.0.0-exp.8] - 2026-06-16
### Added
- Author Cloud Code Modules natively in the Unity Editor, with your cloud source code compiled automatically and fully
  debuggable right where you work. There's no longer any need to regenerate bindings or maintain a separate solution
  outside your Unity workspace - everything lives in one place. Writing, compiling, debugging, and deploying now happen
  in a single workflow, enabling rapid iteration and faster deployment. Highlights include:
  - Create new Cloud Code Modules quickly with a guided creation flow.
  - Iterate rapidly and catch issues early with modules that compile in-editor as part of your project.
  - Skip manual bindings regeneration - strongly-typed client bindings are generated and kept in sync automatically.
  - Scoped Cloud Code is supported alongside standard modules.

### Fixed
- Added retries with a longer request timeout when checking whether a Cloud Code module exists during deployment.
- Fixed Cloud Code Module deployment resolving required precompiled assemblies using Editor-loaded assembly paths on Unity 6000.5 or newer.

### Changed
- Renamed Native Modules to Cloud Code Modules; they now use the `.ccmu` file extension instead of generic `.asset` files. Recreate any existing modules.
- Generating bindings will now includes enum values alongside their names.
- Generating bindings dictionary keys now retain their original types instead of being converted to strings.

## [3.0.0-exp.7] - 2026-04-28
### Changed
- Updated templates setup and dependencies to use Cloud Code APIs v0.0.26 and Core v0.0.4.
- Updated the local server debugger.

## [3.0.0-exp.6] - 2026-04-23
### Fixed
- Fixed an issue where .net path checks were not account for dotnet cli commands.

### Added
- Added Cloud Code Debugger that needs to be enabled by adding `UNITY_SERVICES_CLOUDCODE_EXPERIMENTAL` under Project Settings > Player > Scripting Define Symbols.

## [3.0.0-exp.5] - 2026-04-22
### Added
- Added custom icon form Cloud Code Module Reference (.ccmr) and Cloud Code Javascript (.js) files
- Added a console log with a "View on Dashboard" link upon successful deployment of Cloud Code Modules.
- Added "Deployment Window" shortcut to the Local Cloud Code Server toolbar popup.
- Scoped invocations are supported for Local Cloud Code Debugging and remote.
- Added retries with a longer request timeout when checking whether a Cloud Code module exists during deployment.

### Changed
- Updated the "Go to Dashboard" link in Project Settings for Cloud Code to point to Cloud Code Overview page.
- Improve discoverability of Cloud Code in the Unity Package Manager.

### Fixed
- Fixed an issue where rapidly starting and stopping the local Cloud Code server incorrectly produced console error logs.
- Added checks to prevent Users from setting invalid Ports for the local Cloud Code server.
- Added .Net path setting validation when starting the local Cloud Code server.
- Fixed usage of disallowed `Assembly.Location` API in Unity 6.5+

## [3.0.0-exp.4] - 2026-03-11
### Added
- Implement the Resetting of Scope State and persistence for Local CC Debugging.

### Fixed
- Fixed local server start failure due to unescaped parameters.
- Fixed Cloud Code fields in Project Settings to show their own descriptive tooltips.
- Fixed debugger tooltip to indicate debug fields are read-only once the local server starts.

## [3.0.0-exp.3] - 2026-02-25
- Releasing public experimental exp-3.

## [3.0.0-exp.2] - 2026-02-12
### Fixed
- Enable support for passing complex types for C# modules deployed onto the local Cloud Code server.

## [3.0.0-exp.1] - 2026-01-19
### Added
- Implemented Local Cloud Code debugging, enabling Users to rapidly iterate and debug C# modules on a local server
  running on their machine. This has a requirement of Unity 6.3 or higher. Users can:
  - Configure local server settings via Cloud Code Project Settings (File > Project Settings > Services > Cloud Code)
  - Enable the Local Cloud Code Toolbar via (Top right Toolbar Context Menu > Services > Cloud Code)
  - Start or stop the local server via the toolbar and attach local debuggers through Visual Studio or Rider.
  - Deploy C# modules to the local server via the deployment window (Services > Deployment) to be executed.
- Implemented support for Stateful Cloud Code with Player and Multiplayer Session scoped invocations, enabling users
  to simplify state management for event-driven games. Note that this is only supported for Local Cloud Code Debugging and
  not remote.

### Changed
- Cloud Code event subscription only exposes event registration
  from within the returned subscription object

### Removed
- Removed deprecated CloudCode API

## [2.10.3] - 2026-01-05

### Fixed
- Updated versions for cloud apis
- Added Unit test that is not published as part of the template
- Added .gitignore as part of the template

## [2.10.2] - 2025-09-16

### Fixed
- Signing package
- Fixed assets being loaded despite being of the incorrect type

## [2.10.0] - 2025-07-03

### Changed
- Generating bindings will generate one file and class per cloud code class instead of one class for all functions
- Before generating a solution, the path will be validated so that it corresponds to a valid module name
- Bumped dependency of Core package

### Fixed
- [Tentative fix to dotnet hang](https://discussions.unity.com/t/cloud-code-deployment-status-stuck-at-0/906556/29)
- Changed exit condition , it seems [only indefinite wait for exit will always work](https://github.com/dotnet/runtime/issues/18789)
- Added timeout
- Tentative fix for occasional hang of dotnet when redirecting std output
- Fixed documentation that still used `import` over `require` for bundling

## [2.9.0] - 2024-10-29
### Changed
- Updated the minimum supported Editor version to 2021.3.

### Fixed
- Fixed Help URLs for Cloud Code Module and Cloud Code Script
- Fixed inspector loading for service assets, below Unity 6
- Fixed an issue that might cause the CloudCode scripts inspector to spam calls to the admin API
- Fixed an issue that causes the progress bar to revert during Cloud Code Module deployment

## [2.8.1] - 2024-10-29

### Fixed
- Fixed compatibility wih Deployment 1.3

## [2.8.0] - 2024-10-18

### Added
- View in Deployment Window button in `.ccmr` and `.js` files, dependent on Deployment package version 1.4.0.
- View in Dashboard button in inspector for `.ccmr` and `.js` files.
- View in Dashboard context menu in Deployment Window for `.ccmr` and `.js` files.
- Add `Open Solution` button to `.ccmr` inspector.
- Add Enum support for Cloud Code Bindings generation.

### Fixed
- Fixed support for various primitive types in Cloud Code Modules binding generation
- In-script parameters analysis throws an exception in Unity 6
- `Browse...` button in `.ccmr` inspector now opens the current solution folder properly.
- Fixed Cloud Code Binding generation of primitive types
- Binding Generation will attempt to run in the latest available runtime.
  - This can be disabled with CLOUD_CODE_AUTHORING_DISABLE_VERSION_DETECT flag

## [2.7.1] - 2024-06-10

### Added
- A MessageBytesReceived callback has been added to the available subscription event callbacks
- Adding service registration to the core services registry
- Adding service access through the core services registry (`UnityServices.Instance.GetCloudCodeService()`)
- Added a button to browse your files when choosing a path for a Cloud Code Module

### Changed
- The MessageReceived callback will no longer be fired upon receiving bytes via the event subscription

### Fixed
- Bindings generation is broken when ILogger dependency injection is used
- Cloud Code modules now cleans up compilation artifacts after deploying or generating bindings
- Cloud Code runtime timeout increased to 30 seconds
- Moved create Cloud Code Asset menu items under "Services"

## [2.6.2] - 2024-05-03

### Added
- Added privacy manifest

### Fixed
- An issue that would cache Npm and Node path at startup instead of reading them from the settings

## [2.6.1] - 2024-03-25

### Fixed
- Fixed JS script import when Node project is not initialized

## [2.6.0] - 2024-03-21

### Added
- Improved in-script parameter parsing error feedback
- Added references of the latest javascript services SDKs for autocompletion
- Cloud Code bindings generation

### Fixed
- Fixed error when selecting CloudCodeModuleReference assets in the Project window

## [2.5.1] - 2023-10-19

### Fixed
- Fixed Cloud Code C# modules authoring support for solutions with multiple projects.

## [2.5.0] - 2023-09-21

### Added
- Editor support for Cloud Code C# Modules deploy.

## [2.4.0] - 2023-05-12

### Added
- Added subscription methods for player-specific and project-wide push messages from Cloud Code C# Modules.

## [2.3.2] - 2023-03-24

### Changed
- Increased timeout from 10 seconds to 25 seconds.
- Scripts are no longer cached, which would previously prevent deployments without a local change.

### Fixed
- When using JS Bundling, modifying an imported file will enable re-deployment for the main script.
- Selecting multiple .js files using in-script parameters, the inspector will now remain disabled for editing.
- When selecting multiple .js files or deployment definitions, the inspector will now properly refer to their actual types.
- Deployable assets (.js) not appearing on load in the Deployment Window with Unity 2022+.

## [2.3.1] - 2023-03-21

### Fixed
- Fixed an issue with `null` paths on cloud code scripts.

## [2.3.0] - 2023-03-14

### Added
- Added the ability to bundle JS scripts that are deployed from the editor.
- Added CallModuleEndpointAsync to the Cloud Code Service for calling C# Modules

## [2.2.4] - 2023-02-07

### Fixed
- Fixed corrupted npm libraries used for services.

## [2.2.2] - 2022-12-07

### Fixed
- Missing logs in some failure cases are now handled
- Added more verbose logging for diagnostics behind a preprocessor directive

## [2.2.1] - 2022-12-07

### Fixed
- Duplicate file in the deployment window now appear as a warning instead of an error
- Updated the com.unity.services.deployment.api version to be used for config as code

## [2.1.2] - 2022-10-27

### Fixed
- Rate limiting triggered in some cases

## [2.1.1] - 2022-09-27

### Fixed
- Void type now allowed as return type for CloudCode scripts
- Removed requirement for function arguments when calling an endpoint. Now, it's possible to provide either null or omit them

### Added
- Integration with the `Deployment`  package for config-as-code which allows to edit and configure
  CloudCode scripts directly from the editor

## [2.0.1] - 2022-06-13

### Fixed
- Missing XmlDoc on public ICloudCodeService interface

## [2.0.0] - 2022-06-01

- Moving out of Beta!

## [2.0.0-pre.4] - 2022-04-16

### **Breaking Changes**:
- The interface provided by CloudCode has been replaced by CloudCodeService.Instance, and should be accessed from there instead. The old API will be removed in an upcoming release
- Cloud Code methods now take a Dictionary<string, object> containing the script parameters instead of an object with named fields (the dictionary can still be null if the script does not have any parameters). The old API will be removed in an upcoming release
- When a rate limit error occurs, a specific CloudCodeRateLimitedException will now be thrown which includes the RetryAfter value (in seconds)
- Clarity and structure of some error messages has been improved
- Some classes that were accidentally made public are now internal

### Fixed
- Installation and Analytics IDs not being forwarded to Cloud Code server (causing incorrect tracking downstream)

### Added
- Project Settings tab with link to Cloud Code dashboard
- Cloud Code exceptions now include a Reason enum which is machine-readable

## [1.0.0-pre.7] - 2021-12-07

### Fixed
- NullReferenceException being thrown instead of some service errors
- Documentation URL in package manifest
- Deprecated some elements that should not have been public, these will be deleted in a later release

## [1.0.0-pre.6] - 2021-09-22
- Fixes a crash that could occur with certain exceptions returned from the API

### Known Issues
- When a cloud code function that hasn't been published yet is called from the SDK, the SDK will throw a Null Reference Exception rather than a normal CloudCodeException

## [1.0.0-pre.5] - 2021-09-17
- No longer throws on null function parameter values
- No longer throws on null api return values
- Corrected exception types
- Removed tests from public package
- Fixed code examples in documentation

## [1.0.0-pre.4] - 2021-08-19
- Updated readme and changelog to be more descriptive.
- Updated package description to better highlight the usages of Cloud Code.

## [1.0.0-pre.1] - 2021-08-10

- Updated documentation in preperation for release.
- Updated dependencies (Core and Authentication) to latest versions.
- Updated internals for more stability.
- Added a new API that returns string, in order to support custom user serialization of return values.

## [1.0.0-pre.1] - 2021-08-10

- Updated documentation in preperation for release.
- Updated dependencies (Core and Authentication) to latest versions.
- Updated internals for more stability.
- Added a new API that returns string, in order to support custom user serialization of return values.

## [0.0.3-preview] - 2021-06-17

- Updated depedencies of Core and Authentication to latest versions.

## [0.0.2-preview] - 2021-05-27

- Update documentation and license

## [0.0.1-preview] - 2021-05-10

### Package Setup for Cloud Code.

- Creating the package skeleton.
