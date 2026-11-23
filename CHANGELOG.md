# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [3.2.0-exp.1] - 2026-11-23

### Added
- `ConnectionStateChanged` on `PlayerConnection`, `PlayerScopeProvider`, `MultiplayerScopeProvider` and generated Cloud Behaviour clients reports the player's message channel dropping (`Unsynced`), recovering (`Subscribed`) or failing to resubscribe (`Error`). Subscription errors are now logged as warnings instead of being ignored.
- New Cloud Behaviour modules set a root namespace on their cloud and client assembly definitions, prefixed with the project's root namespace when one is set in Project Settings. Scripts added to a module use its assembly definitions' root namespaces.
- Cloud Behaviours now report warning CCSG159 for a get-only property that saved state does not fully restore.
- `MultiplayerScopeProvider.UseScopeChannel(string module, string tokenFunction)`, which generated Cloud Behaviour clients call so that the provider also listens on its session's own push channel, using the subscription token that the named module function returns. It is hidden from IDE autocomplete and is not meant to be called from game code. While that channel is opening, `Scope` stays `null`. Messages on the channel are raised through `MessageReceived` and its connection state changes through `ConnectionStateChanged`. A failure to open it or the channel being closed remotely raises `Failed` and disables the provider. An error on it is reported through `ConnectionStateChanged` and does not disable the provider.
- `CloudCodeExceptionReason.ScopeChangedDuringCall`, the reason a generated Cloud Behaviour client's endpoint call or `Hydrate()` throws when the client's scope changed before its result returned.
- A public number, `bool`, `char`, `string` or enum `const` on a Cloud Behaviour is now generated on its client with the same value, so rules such as a board size can be shared without a separate assembly. A const isn't synchronized state, so it's never part of the sync message. If the client class already declares a member of the same name, such as a const copied in by hand, the client keeps its own declaration, and warning CCSG167 reports it unless it is the same const with the same value.
- Cloud Behaviours now report warning CCSG168 when an endpoint awaits a `ForScopeAsync` call into a scope that the source shows is still waiting for it, such as passing `Context.ScopeId` to a behaviour that calls back with it. The warning names the calls in between. Such a call previously waited until the request timed out; if it runs now, it fails when it is made, as described under Fixed.

### Changed
- New Cloud Behaviour modules' assembly definitions now require `UNITY_6000_7_OR_NEWER` instead of `UNITY_6000_5_OR_NEWER`, matching the package's minimum Unity version.
- A Cloud Behaviour property with a non-public setter, such as `{ get; private set; }`, now synchronizes to clients, matching the members that persist. Get-only and computed properties still report CCSG113, whose message now points to `[SyncView]` for computed values.
- Cloud Behaviour saved state no longer reports CCSG146 for computed properties, or for get-only collections and objects that can be refilled in place. Get-only collections that can't be refilled, indexers and set-only properties still report it.
- Generated Cloud Behaviour clients now ignore fields the result type doesn't declare instead of throwing `DeserializationException`, so a server that returns extra fields no longer breaks existing clients.
- A Cloud Behaviour data type member you can read but not assign now reports warning CCSG131 instead of an error, and a computed property on the module's own data types reports warning CCSG163. Each message says how to send the value to the client or keep it on the server.
- Messages a Cloud Behaviour client receives before it is ready are now delivered after `ScopeChanged`, each with the state it carried.
- Cloud Behaviour warnings about a type or base class declared in another assembly, such as a shared assembly, now appear in the Unity Console and in Rider's problem list, pointing at the class that uses it. They were previously only written to `Editor.log`.
- Cloud Behaviour session support now requires Multiplayer package 2.2.0 or later. With an older Multiplayer package it is excluded from the build, instead of failing to compile.
- The **Target Player ID** and **Target Session Type** fields in a generated Cloud Behaviour client's Inspector now say what leaving them empty does, with placeholder text and a tooltip.
- The API documentation for `Access.SessionHost`, `ISession.HostId` and `ISession.PlayerIds` now explains host migration: `Access.SessionHost` checks the current host on every call, a host ID stored in a behaviour does not follow the host when it leaves, and `PlayerIds` has no order.
- A Cloud Behaviour endpoint with `AutoSynchronize` on now pushes to a session member only when what that member would receive (synced fields and `[SyncView]` projections) changed during the call, so a read-only endpoint pushes nothing. Synced members and projections now also run before the call. If a synced member throws, a warning is logged and every member is pushed after the call. If a `[SyncView]` projection throws, a warning is logged and that player is pushed after the call. An endpoint that raises an event still pushes every member afterwards. `SynchronizeAsync` still pushes to every member. The caller's own response still raises `Synchronized` on its client.
- A session-scoped Cloud Behaviour without `[SyncView]` projections now publishes each state change and event once, to its session's own channel, instead of once to every member, and its generated client subscribes to that channel when it is enabled. Behaviours with a projection, and player-scoped behaviours, push as before. Redeploy the module and regenerate its client together: a client for the new behaviour fails to subscribe to a module deployed before it, and a client generated before it doesn't receive the new module's pushes. Such a client's `Scope` is set only once that channel is open as well as the player signed in and the session active, and a failure to open it raises `Fatal`.
- **Assets > Create > Services > Cloud Behaviour Script** in a Cloud Code module's Cloud folder now adds the Cloud Behaviour references to the module, as the `create_cloudbehaviour` CLI command does, instead of reporting that the folder isn't part of a Cloud Behaviour module. The new script is selected in the Project window, and a failure is shown in a dialog instead of only in the Console.

### **Breaking Changes**:
- `SendMessageAsync` and `SynchronizeAsync` on a Cloud Behaviour now throw `CloudBehaviourException` with `Reason` `PushFailed` when a push fails, after trying every recipient, instead of only logging it. Its `FailedPlayers` lists the players that missed it and why; for a session behaviour without `[SyncView]` projections, which publishes once to the session's channel, it holds a single entry keyed by the session ID. If `SynchronizeAsync`'s exception is caught, the automatic synchronization at the end of the call tries those players again. The automatic synchronization after an endpoint and event pushes still only log a failed push, and no longer fail the call if the push throws.
- Generated Cloud Behaviour clients no longer include endpoints marked `[CloudCodeAccess(Access.Service)]`, which clients could only call to get a `403`. Behaviours still reach them through the generated `I{Behaviour}` interface.
- A generated Cloud Behaviour client's `Scope` is now set only once the client is ready, and becomes `null` while it switches scope. Endpoint calls made before then throw `InvalidOperationException`.
- A Cloud Behaviour endpoint call or `Hydrate()` whose result returns after the client's scope changed now throws `CloudCodeException` with reason `ScopeChangedDuringCall` and leaves the synchronized fields alone. It previously applied the earlier scope's state and raised `Synchronized`.
- `PlayerConnection` and `PlayerScopeProvider` are now sealed.
- `ICall.ForScope` and `ICall<T>.ForScope` are renamed to `ForScopeAsync`, matching `ScheduleAsync`.
- `CloudBehaviourError` and `CloudBehaviourErrorReason` are removed. A generated Cloud Behaviour client's `Error` event now passes a `CloudBehaviourClientException`, and `MessageError` is now a `CloudBehaviourClientExceptionReason`.
- `CloudBehaviourException` and `CloudBehaviourExceptionReason`, which generated Cloud Behaviour clients and scope providers pass to `Error`, `Fatal` and `Failed`, are renamed to `CloudBehaviourClientException` and `CloudBehaviourClientExceptionReason`. `CloudBehaviourException` is now the exception module code catches.
- `CloudBehaviour` is now abstract. Behaviours that derive from it are unaffected.
- `CloudBehaviour.Logger`, `Context`, `Push` and `Synchronizer` can no longer be set from user code. The framework sets them.
- `ICloudCodeScopeProvider` is now internal. Use `PlayerScopeProvider` or `MultiplayerScopeProvider` directly.
- A Cloud Behaviour `ForScopeAsync` call back into a scope earlier in the chain of calls now throws `InvalidOperationException` at once, even when that scope didn't await its call. Such a call back previously waited for the earlier call to finish, or until the request timed out if that call was waiting for it. Return the result to the caller instead.
- Cloud Behaviour endpoints can no longer have a parameter named `__cloudCallChain`: the generated code reserves it, and error CCSG165 reports it.
- A Cloud Behaviour member named `OnDestroy` is now left out of its generated client with warning CCSG119, because the client uses that name.
- `ConnectionStateChanged` is now a member of every generated Cloud Behaviour client. A behaviour member with that name is left out of its client with warning CCSG119, and a client class that declares its own `ConnectionStateChanged` no longer compiles. Rename the member.
- A Cloud Behaviour endpoint, synchronized field, event, const or `[SyncView]` name that matches a Unity message, such as `Start`, `Update` or `OnDestroy`, or a member that `MonoBehaviour` inherits, such as `name`, `enabled`, `tag`, `SendMessage` or `Invoke`, is now ignored and reported by warning CCSG119, because the generated client is a `MonoBehaviour` and the name would collide with it. An endpoint marked `[CloudCodeAccess(Access.Service)]` is not affected, since clients never include it. Rename the member to include it.
- A method that overrides or hides an `object` method, such as `ToString()` or `Equals(object)`, is no longer a Cloud Behaviour endpoint, and a `public` one that hides it without overriding reports warning CCSG171. Previously it was generated as an endpoint that clients could call.
- Redeploy your Cloud Behaviour modules and rebuild your players together. A client reports a push from a module built with an older version through `Error`, and a client built before this change reads a push from a redeployed module as an empty state, without an error. A `MessageReceived` handler that reads the `Message` of a behaviour's own state or event push now sees `{"scope": {"kind", "id"}, "behaviour", "data"}` where it used to see the bare state.

### Fixed
- The package now compiles without C# compiler warnings on Unity 7000.0 and later.
- A Cloud Behaviour endpoint, synchronized field, event, const or `[SyncView]` named `GetComponentData`, `SetComponentData` or `isDataComponent` is now left out of its generated client with warning CCSG119, because `MonoBehaviour` inherits those names on Unity 7000.0 and later. Rename the member to include it.
- Creating a Cloud Behaviour module now fails when an assembly definition with the same name in a different case already exists, such as `foo` and `Foo`. The two would collide in `Library/ScriptAssemblies` on macOS and Windows.
- A generated Cloud Behaviour client's synchronized fields no longer stay stale after its message channel drops and reconnects. The client fetches the current state and raises `Synchronized`, or raises `Error` with the new `CloudBehaviourClientExceptionReason.HydrationFailedOnReconnect` if it can't, and stays connected.
- A Cloud Behaviour push from the automatic synchronization or an event that the push service rejects is now logged as an error in the module's logs, naming the push and the player who didn't receive it. The call still succeeds.
- A Cloud Behaviour `ForScopeAsync` call into a scope that is still waiting for it, such as player A calling player B and B calling A back, now fails at once with an error naming the scopes involved, including when the calls go through a client generated by another module. It previously waited until the request timed out. Modules must be rebuilt with this version for the check to apply.
- Source generator warnings and errors for Cloud Behaviours about a type in another assembly, such as a shared assembly definition, or that had no source location, such as two clients of one cloud class (CCSG307), now show in the Unity Console instead of only in the Editor log. They point at a class in your module and still name the type or member they're about, so projects with shared assemblies may see warnings they didn't see before.
- The local Cloud Code server no longer keeps a Cloud Behaviour's scope busy after a call to it hangs or fails.
- A generated Cloud Behaviour client now applies a push only when it came from the client's own scope and behaviour, and raises `MessageReceived` for it only then. Previously, a player in two sessions, or one switching between sessions, could get one session's state applied to the other, and a behaviour could pick up pushes from a same-named behaviour in another module. Messages your own module code sends are still delivered through `MessageReceived` unchanged.
- Cloud Behaviours whose endpoints, parameters, events, synchronized fields or data type members are named with a C# keyword, such as `@class`, `@event` or `await`, now generate code that compiles. Previously the generated code failed with error CS1001. A `[SyncView]` name can now be a C# keyword, such as `[SyncView("lock")]`, instead of reporting error CCSG144.
- Generated Cloud Behaviour clients now work in IL2CPP players built with Medium or High managed stripping. Stripping removed the constructors and setters of the types they read results and messages into, so endpoint calls, `Hydrate()` and synchronization failed, and results holding the module's data types arrived with empty fields.
- A Cloud Behaviour that implements `IStateSerializer` now has its `OnSerialize` and `OnDeserialize` called for state persistence, instead of the interface being ignored and state saved with the default serializer.
- Cloud Behaviours now report warning CCSG158 when a public `static` field or property, or a public `const` of a class or interface type, never reaches clients. Previously there was no diagnostic.
- Cloud Behaviours now report error CCSG118, instead of a warning, when members of different kinds share a name. Every colliding member was dropped while the build stayed green.
- Cloud Behaviours now report warnings CCSG160, CCSG161 and CCSG162 for data types with subclasses that could be sent in their place. Values are read as their declared type, so fields that only a subclass declares are ignored.
- A Cloud Behaviour client whose warm-up or hydrate fails when its first scope resolves now raises only `Fatal`. It previously raised `ScopeChanged` with a `null` scope first. If it fails while switching scope, the previous scope is withdrawn through `ScopeChanged` before `Fatal`.
- A Cloud Behaviour assembly whose name isn't a valid Cloud Code module name, such as an assembly definition renamed to `Game.Cloud` or `2Game` after creation, now reports error CCSG164 instead of generating code that doesn't compile.
- Module names entered when creating a Cloud Code or Cloud Behaviour module, in the creation window or with the `create_module` CLI command, are now limited to letters, digits and underscores, up to 50 characters, and must start with a letter. The CLI command rejects other names instead of changing them. Names such as `My.Module` or `My-Module` previously failed on deploy, and Cloud Behaviour modules named like `3DGame` generated code that didn't compile.
- Script names entered when creating Cloud Code or Cloud Behaviour scripts now drop `/ ? < > \ : * | " . -` on every platform.
- Adding a script to a Cloud Code or Cloud Behaviour module folder whose assembly definition can't be read now shows only the error that the assembly definition couldn't be read. Cloud Behaviour previously also said the folder wasn't part of a module, and Cloud Code opened the window to create a new module.
- A data type nested in a Cloud Behaviour or Cloud Code module class no longer causes warning CS0436 on the client's `[CloudBehaviour]` or `[CloudModuleClient]` attribute in the Editor. The warning came from generated code, so builds that treat warnings as errors failed. A type you declare yourself with the same name as a type from another assembly still reports CS0436.
- A Cloud Behaviour named after its root namespace, such as the scaffold's `MyGame.Cloud.MyGame`, no longer fails to compile its generated client with CS0234 when it uses a data type nested in it, including as a field of a data type in another namespace.
- A Cloud Code or Cloud Behaviour module that fails to create no longer leaves files behind in a `Cloud` or `Client` folder that already existed, so retrying with the same name no longer reports an assembly name conflict. Creating a module also no longer overwrites or adds an assembly definition in a `Cloud` or `Client` folder that already has one.
- Adding a Cloud Behaviour script no longer leaves the cloud script without its client when a script template is missing. The failure is now logged as an error.
- A Cloud Behaviour endpoint parameter named like something the generated code uses, such as `value` or `sync`, no longer produces code that doesn't compile. Cloud Behaviours now report error CCSG165 for the four parameter names the generated code reserves: `__ctx`, `__push`, `__value` and `__sync`. Source-generated Cloud Code module clients no longer fail when a function parameter is named `k_Service`. A class named `JsonConvert`, `ModuleHostWarmup`, `CloudBehaviourServices` or `CloudCallTransport` in a Cloud Behaviour's namespace no longer hides the type the generated code refers to.
- An abstract Cloud Behaviour now reports error CCSG169, and one without a public or internal parameterless constructor reports error CCSG170. A generic data type declared in the module, such as `Box<int>`, reports error CCSG104 in a Cloud Behaviour and CCSG211 in a Cloud Code module; one from an assembly the client also references still works. Each of these used to generate code that didn't compile, with no diagnostic saying why.
- A Cloud Behaviour that derives from another Cloud Behaviour now reports error CCSG166. The base class's synced fields and endpoints were silently left out of the generated client, while the server still persisted them. Derive each behaviour from `CloudBehaviour` directly.
- An endpoint parameter whose default is a negative enum value, such as `Colour c = Colour.Red` where `Red = -1`, no longer produces generated client code that doesn't compile (CS0075).
- A Cloud Behaviour client no longer rolls its synchronized fields back to an older state when an event raised during its own endpoint call arrives after that call's result. The event's listeners still run, and the state the event carries is applied only if it arrives before the result. A synchronized field or `[SyncView]` named `__CallId` now reports CCSG119 or CCSG144, since the synchronization message reserves that name.
- A Cloud Behaviour client or `PlayerScopeProvider` enabled for one player now drops its scope when a different player signs in without the first one signing out, and gets it back when that player signs in again. It previously kept the first player's scope and passed on the new player's messages. A `PlayerScopeProvider` or `MultiplayerScopeProvider` also drops its scope when the player signs out while a new sign-in is still opening its subscription.
- A generated Cloud Behaviour client now calls `DisableClient()` when it's destroyed, so it releases its subscription even without an `OnDisable`. A client class that declares its own `OnDestroy` keeps it, and one that inherits a virtual `OnDestroy` gets an override that calls the base method first. A client whose base class has an `OnDestroy` it can't override keeps it and reports warning CCSG172, since nothing then calls `DisableClient()` when it's destroyed.
- A list item you collapse in a generated Cloud Behaviour client's Inspector now stays collapsed, along with any data type fields you collapse inside it. Previously the Inspector recreated the list's items several times a second, which expanded them again. A list of data types now refreshes when a synchronization assigns new state, so changes your own code makes to that list in place show after the next synchronization.

## [3.1.1] - 2026-11-23

### Added
- `CloudCodeException.ScriptErrorMessage`: for a script error from a C# module, the message of the exception the function threw, without server type names or stack frames. Use it to show a rejection reason to players instead of parsing `Message`, whose text is unchanged apart from no longer including the stack trace.

### Changed
- Script errors from C# modules no longer include the server stack trace, so `CloudCodeException.Message` no longer ends with stack lines. The stack trace is in the Cloud Code logs in the Unity Dashboard and, for the local Cloud Code server, in its log.
- Cloud Code warnings about a type or base class declared in another assembly, such as a shared assembly, now appear in the Unity Console and in Rider's problem list, pointing at the class that uses it. They were previously only written to `Editor.log`.
- The documentation for `ITriggersEventClient.PublishEventAsync` now says that the task returns the full event type of the published event, such as `com.unity.services.cloud-code.custom.match-ended.v1`. It previously said the task returns the event's ID.

### Fixed
- The local Cloud Code server now supports `ITriggersEventClient`. It applies the same checks to the event as Cloud Code, then logs it instead of sending it to Triggers. A module function that used the client previously failed on the local server.
- The local Cloud Code server now rejects push messages over 51,200 bytes (50 KiB) and message types over 128 bytes, as the cloud does, and logs why. The `IPushClient` documentation now states these limits; it previously said 10 kilobytes.
- Fixed an Editor memory leak on CoreCLR (Unity 6.7+).
- Creating a Cloud Code module now fails when an assembly definition with the same name in a different case already exists, such as `foo` and `Foo`. The two would collide in `Library/ScriptAssemblies` on macOS and Windows.
- Source generator warnings and errors for Cloud Code modules about a type in another assembly, such as a shared assembly definition, or that had no source location, now show in the Unity Console instead of only in the Editor log. They point at a class in your module and still name the type or member they're about, so projects with shared assemblies may see warnings they didn't see before.
- The local Cloud Code server now counts the time a function spends producing a lazily returned result against the call's timeout.

## [3.1.0] - 2026-11-16

### Added
- Added Unity services CLI commands for Cloud Code, available when com.unity.pipeline is installed.
- The local server popup has a Show Logs Folder item that opens the folder the local server writes its logs to, and a Show Modules Folder item that opens the folder it loads deployed modules from.
- Clients can now receive the messages a module sends with `SendScopeMessageAsync` by calling `ICloudCodeService.SubscribeToScopeMessagesAsync`. The subscription token comes from a module function in that scope that returns `GenerateScopeTokenAsync`'s reply, so that function's access rules decide who can subscribe. Subscribers to the same scope share one channel. If the token function fails, the subscription throws that call's `CloudCodeException`, such as `Forbidden`.
- `ITriggersEventClient` in `Unity.Services.CloudCode.Apis` (Apis 0.0.29), which a module function uses to publish a custom event that the Triggers service can respond to.

### Changed
- New Cloud Code modules' assembly definitions now require `UNITY_6000_7_OR_NEWER` instead of `UNITY_6000_5_OR_NEWER`, matching the package's minimum Unity version.
- The local Cloud Code server now writes its logs to the project's `Logs/CloudCodeLocalServer` folder instead of a machine-wide `UnityCloudCode/Logs` folder, and keeps the last five runs. If that folder can't be written, the server starts without a log file and logs a warning. Logs from earlier versions stay in the old folder.
- The local Cloud Code server's deployed modules and local state are now stored in the project's `Library/CloudCodeLocalServer` folder instead of a machine-wide `UnityCloudCode/Modules` folder in your local application data. After updating, the first start of the local server redeploys every module and starts with empty local state, and a server that was running during the update is stopped and must be started again. Deleting the project's `Library` folder also clears the local state. The old `UnityCloudCode/Modules` folders stay on disk, and you can delete them.
- Source generator warnings about a type or base class declared in another assembly, such as a shared assembly, now appear in the Unity Console and in Rider's problem list, pointing at the class that uses it. They were previously only written to `Editor.log`.

### **Breaking Changes**:
- `ModuleHostWarmup.ResetDefault` is no longer public.
- `ICloudCodeService` has two new `SubscribeToScopeMessagesAsync` members. Classes that implement the interface must add them.

### Fixed
- Cloud Code modules no longer drop a referenced assembly definition whose file name differs from its `name` when they are packaged and auto-deployed. A reference by name must now match the assembly definition's `name` exactly, including case.
- Local auto-deploy now warns when a module references an assembly definition it can't read, instead of silently skipping the module.
- Data types generated for Cloud Code module clients now keep their properties in IL2CPP players built with Medium or High managed stripping, instead of arriving with empty fields.
- Starting the local Cloud Code server no longer fails with `Connection refused` when the server takes more than a few seconds to start listening, such as on a slow or busy machine. The Editor now waits up to 30 seconds while the server process is running.
- A script reload while the local Cloud Code server is starting no longer stops the server when it hasn't started listening yet.
- The local Cloud Code server's state is now scoped to its project and Editor. Two Editors on the same machine no longer read or overwrite each other's server status, PID, or log position, stop or kill each other's server, route Play Mode to another project's server, or share deployed module zips.
- Module names entered when creating a Cloud Code module, in the creation window or with the `create_module` CLI command, are now limited to letters, digits and underscores, up to 50 characters, and must start with a letter. The CLI command rejects other names instead of changing them. Names such as `My.Module` or `My-Module` previously failed on deploy.
- Script names entered when creating Cloud Code scripts now drop `/ ? < > \ : * | " . -` on every platform.
- Cloud Code modules in your project, not Module References, whose functions, parameters or data type members are named with a C# keyword, such as `@class` or `await`, now generate client bindings that compile. Previously the generated code failed with error CS1001. A `[CloudCodeFunction]` name can now be a C# keyword, such as `[CloudCodeFunction("lock")]`, instead of reporting error CCSG204.
- A Cloud Code module data type no longer fails to compile with CS0234 when one of its fields is a type nested in a class that has the same name as the root of its namespace, such as `Shop.Models.Shop.Item`.
- Stopping or restarting the local Cloud Code server no longer logs "Local server health check failed" afterwards, or stops a server started straight after, when a health check failed just before the stop.
- Adding a script to a Cloud Code module folder whose assembly definition can't be read now shows only the error that the assembly definition couldn't be read, instead of also opening the window to create a new module.
- A Cloud Code module that fails to create no longer leaves files behind in a `Cloud` or `Client` folder that already existed, so retrying with the same name no longer reports an assembly name conflict. Creating a module also no longer overwrites or adds an assembly definition in a `Cloud` or `Client` folder that already has one.
- Source generator warnings and errors for Cloud Code modules that had no source location, such as two clients of one cloud class (CCSG307), now show in the Unity Console instead of only in the Editor log. They point at a class in your module and still name the type or member they're about.
- A data type nested in a Cloud Code module class no longer causes warning CS0436 on the client's `[CloudModuleClient]` attribute in the Editor. The warning came from generated code, so builds that treat warnings as errors failed. A type you declare yourself with the same name as a type from another assembly still reports CS0436.
- Source-generated Cloud Code module clients no longer fail to compile when a function parameter is named `k_Service`.
- A Cloud Code module function parameter whose default is a negative enum value, such as `Colour c = Colour.Red` where `Red = -1`, no longer produces generated client code that doesn't compile (CS0075).
- The local Cloud Code server now counts the time a function spends producing a lazily returned result against the call's timeout.

## [3.0.0] - 2026-10-23
### Added
- Added the Cloud Code Observability Logs window (`Services > CloudCode > Observability Logs`) for querying the logs your scripts and modules emit.
- Modules are now redeployed to the running local Cloud Code server automatically when their source changes, so you no longer need to stop and start the server to pick up a code change. The Console lists the modules each redeploy reloaded. Module References are redeployed when the Editor regains focus, and Cloud Code Modules after Unity recompiles them. Nothing is redeployed during Play mode: the Console and a notification in the Game and Scene views list the changed modules, and they are redeployed when Play mode ends.
- A `404` from the local Cloud Code server on a module call now logs a hint that the module may not be deployed yet.
- Entering Play mode without the local Cloud Code server running now logs a warning when the remote environment is stale relative to local module source.
- The Module Reference (`.ccmr`) Inspector now shows a last successful deployment summary, matching the Cloud Code Module (`.ccmu`) Inspector. The summary survives domain reloads and follows the active project and environment.
- The Deployment window now shows "Modified locally, deploy to update" for a Module Reference when its external solution has changed since the last recorded deploy. Previously the status came from the timestamp of the `.ccmr` file itself.
- Cloud Code push-message subscriptions now support multiple subscribers on the same channel, each with its own callbacks and its own unsubscribe.
- Added `CloudCodeExceptionReason.ScopeStateIncompatible`, raised when a scope's saved state no longer fits the deployed module (server error code 9052).
- Added `ModuleHostWarmup`, which warms the project's module host at sign-in so the first module call is not delayed by provisioning. Call `ModuleHostWarmup.Default.Arm()` from your startup code, at `BeforeSceneLoad` or later; a client on its own `IUnityServices` instance creates its own `ModuleHostWarmup`, arms it, and stops it with `Reset()`.
- Cloud Code Module data types now capture public properties with a public getter and setter, as well as fields. Properties you can read but not assign report warning CCSG216, because the client never receives their value. Computed properties on the module's own data types report warning CCSG229, because the client's copy of the type leaves them out. Write-only properties and properties with a non-public getter are skipped without a diagnostic.
- Cloud Code Modules now report warning CCSG224 for a get-only property that saved state does not fully restore.
- A Play Mode scenario now warns in its Additional Configuration section when a Local Instance's build profile has Development Build disabled, and links to the Build Profiles window. Release builds ignore `--cloud-code-local-debugger`, so that instance would call the deployed service instead of the local Cloud Code server.
- A Play Mode scenario that deploys Cloud Code now fails validation before any instance builds or launches if the project is not linked or no environment is selected. With Deploy Target set to Local, it also fails if another process holds the local Cloud Code server's port, or if the running local server uses a different port from the one in the Cloud Code Local Server Settings asset.
- Editor analytics now cover Cloud Code authoring: creating, deploying and publishing JS scripts; creating and deploying Module References (`.ccmr`) and Cloud Code Modules (`.ccmu`); generating solutions and bindings; and the local Cloud Code server lifecycle. Automatic local redeploys are not reported. Collection follows the Editor's analytics setting, and no script or module source is sent.

### Changed
- Cloud Code Modules can now share code through a third assembly definition.
- Local debugging no longer redirects other Wire consumers to the local Cloud Code server. Cloud Code now opens its own connection to the local server through `IWireFactory`, so Multiplayer, Friends and the Editor status service stay connected to their environment while you debug.
- You can now enable local Cloud Code debugging in a development player build by launching it with `--cloud-code-local-debugger[=<port>]`. Release builds ignore the argument.
- Saved state no longer reports CCSG222 for computed properties, or for get-only collections and objects that can be refilled in place. Get-only collections that can't be refilled, indexers and set-only properties still report it.
- Typed endpoint results (`CallEndpointAsync<T>`, `CallModuleEndpointAsync<T>` and generated Cloud Code Module clients) now ignore fields the result type doesn't declare instead of throwing `DeserializationException`, so a server that returns extra fields no longer breaks existing clients.
- Manual deploys to the local Cloud Code server are now refused during Play mode. Changed modules are redeployed when Play mode ends.
- Updated the Cloud Code Module samples, which now include a timer round trip and require Unity 6000.7 or newer. Package samples now ship as source instead of `.unitypackage` archives.
- A Play Mode scenario with Deploy Target set to Remote now deploys only the Cloud Code Modules and Module References that changed since their last deploy to the active environment, instead of all of them on every Play. Before instances launch, it waits until 30 seconds have passed since the last module upload finished, so they call the new code.
- Increased the memory limit for each Cloud Code worker from 256 MB to 512 MB.

### **Breaking Changes**:
- The package now requires Unity 6000.7 or newer.
- Cloud Code Modules now target .NET 10. The module template references `Com.Unity.Services.CloudCode.Core` 0.0.9.
- The local Cloud Code server and the bindings generator now run on .NET 10, so you must install the .NET 10 SDK.
- Generated Cloud Code Module clients no longer include functions marked `[CloudCodeFunction(..., Access.Service)]`, which clients could only call to get a `403`.
- `readonly` fields on Cloud Code Module data types are no longer captured and now report warning CCSG216.
- `[StateScope]` is no longer inherited by subclasses. A class that derives from a stateful class must declare `[StateScope]` itself to be stateful.
  - Functions inherited from a base class now belong to the concrete subclass and use its scope, matching the Cloud Code service.
  - Abstract classes are no longer listed on their own.
  - A subclass without its own `[StateScope]` reports warning CCSG228.
- `CloudCodeFunctionAttribute`, `StateScopeAttribute`, `CloudCodeScope`, `CloudCodeService` and `SubscriptionEventCallbacks` are now `sealed` and can no longer be subclassed.
- Calls to the obsolete `CloudCode.CallEndpointAsync(string, object)` and `CloudCode.CallEndpointAsync<TResult>(string, object)` no longer compile. Use `CloudCodeService.Instance.CallEndpointAsync` instead.
- The service API classes in `Unity.Services.CloudCode.Apis`, such as `CloudSaveDataApi`, are now internal. Use the interfaces from `IGameApiClient` and `IAdminApiClient` instead.

### Fixed
- Closing a Multiplayer Play Mode player no longer stops the local Cloud Code server that the main Editor started.
- A local Cloud Code server paused for more than 30 seconds, for example at a debugger breakpoint, is still monitored afterwards, so the Editor notices when it later stops.
- A Cloud Code Module whose cloud assembly name contains a dot, such as `Company.Game.Cloud`, can now be called on the local Cloud Code server.
- Cloud Code Modules now report error CCSG204 for a `[CloudCodeFunction]` name that isn't a valid C# identifier, such as `do-thing` or `class`. The generated client used the name as a method name and didn't compile.
- Two Cloud Code Module clients with the same name in different namespaces, or a client named like a global data type, no longer stop every client in the assembly from generating.
- The local Cloud Code server no longer starts unless valid Unity Services environments are set.
- Starting the local Cloud Code server no longer fails when its port check stalls or cannot run. The check now connects to the port on localhost, the address the server listens on, so it detects a port held by another user's process and no longer flags a port used only on another network address.
- The local Cloud Code server port in Project Settings > Services > Cloud Code no longer accepts `0`. A `0`, whether typed or already stored in the settings asset, is replaced by the default port, `14750`.
- Requests to the local Cloud Code server no longer time out after 30 seconds.
- The local Cloud Code server now formats error responses the same way as the Cloud Code service.
- On the local Cloud Code server, a timer callback on a Player scope now runs with the scope owner's `PlayerId`, so `IPushClient.SendPlayerMessageAsync(context, ...)` from the callback reaches that player instead of being dropped.
- While the local Cloud Code server is running, a Cloud Code Module whose source changed while script compilation is failing now shows a Compilation Failure status naming the failing assemblies, instead of only `Modified locally`. It is redeployed once scripts compile again.
- With no environment selected, the local server deploy, the Observability Logs window, the local auto-redeploy, the stale deployment warning and the last deployment record now treat the environment as unset, instead of using an all-zero environment ID.
- A Cloud Code Module deploy now fails with an error status when script compilation failed, when scripts are still compiling, when the API compatibility level is unsupported, or when the module has no cloud assembly definition. Previously it uploaded nothing and appeared to succeed. When compilation failed, the status names the assemblies that failed to compile, if the Editor knows them.
- A Cloud Code Module deploy refused from the Deployment window no longer shows its error twice in the status panel, or again in the Console.
- A Cloud Code Module or Module Reference deploy now settles on `Deployed` after publishing to the remote service and on `Deployed to Local Server` after a local deploy, instead of `Up to date` for both. A module last deployed only to the local Cloud Code server no longer reads as up to date.
- Each compiler diagnostic from a module that fails to build is now its own Console entry at its own severity, instead of one entry holding the whole `dotnet publish` transcript. The full output is still available with Cloud Code verbose logging enabled.
- Building a Cloud Code Module no longer emits the .NET SDK warning about a solution-level output path (NETSDK1194). The published output is unchanged.
- "Open in Dashboard" and the Inspector dashboard link now open the deployed module: the cloud assembly name for a Cloud Code Module and the solution's main project for a Module Reference. Module and script names that end in the letters of their extension are no longer truncated.
- JavaScript files shipped by other packages no longer appear in the Deployment window as Cloud Code scripts and are no longer deployed by Deploy All. Scripts under `Assets` or in embedded and local packages are still listed.
- Opening a Cloud Code script no longer logs `JsAssetHandler:OpenAsset (int,int) does not match any of [OnOpenAssetAttribute] expected signatures` on Unity 6000.4 and newer, and the script opens in the configured external editor again.
- Cloud Code Module data types now report warning CCSG212 when a derived declaration hides an inherited public member from the generated client type.
- Cloud Code Modules now report error CCSG225 when saved state has two members with the same name, such as a field hidden with `new` in a data type held by a `[StateScope]` class.
- Cloud Code Modules now report warnings CCSG226 and CCSG227 for data types with subclasses that could be sent in their place. Values are read as their declared type, so fields that only a subclass declares are ignored.
- An assembly with Cloud Code Module bindings no longer fails to compile with CCSG902 and CCSG908 when other packages, such as Netcode for Entities 7.0, add their own Roslyn additional files to it. A missing project settings file is now reported as a warning, and generation continues with default settings.
- Generated Cloud Code Module clients now compile when two function parameter or return types share a name across namespaces, or when a project type has the same name as a type the client uses, such as `Task`.
- The `Unity.Services.CloudCode.Apis` assembly is no longer auto-referenced, so its types no longer clash with another package's types of the same name. Module code is unaffected because it references the assembly explicitly.
- Cloud Code calls that fail with an empty or unrecognised error body now throw a `CloudCodeException` with a reason and error code, instead of a `NullReferenceException` or an `Unknown` reason. `403` responses now map to `CloudCodeExceptionReason.Forbidden`, and `502` and `504` to `ServiceUnavailable`.
- A Cloud Code result that cannot be deserialized into the requested type now throws a `CloudCodeException` with reason `DeserializationFailed`.
- Cloud Code APIs: `MatchmakingResults` now exposes the `MatchId` sent by the matchmaker.
- A failed .NET check before starting the local Cloud Code server no longer overwrites the .NET Path set in **Edit** > **Preferences** > **Cloud Code**.
- The Editor now reports a failed stop of the local Cloud Code server the same way as a failed start.
- With Multiplayer Play Mode, starting or stopping the local Cloud Code server just after a domain reload no longer runs it twice.
- The Editor now logs a warning with the reason when it can't reconnect to a running local Cloud Code server after a domain reload or Editor restart.
- Cloud Code now logs an error when a push-message subscription made during local debugging can't reach the local Cloud Code server. It previously failed silently.
- A long-running local Cloud Code server no longer loses access to Unity services.
- Clearing the local Cloud Code server's state now removes all saved scope state.

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

### Changed
- New Cloud Behaviour scripts are now created with `[StateScope(Scope.Player)]` instead of `[StateScope(Scope.MultiplayerSession)]`, and carry a comment linking to the scope documentation.
- Generated Cloud Behaviour data types are emitted into their own namespace, with nested types kept inside their containing type instead of flattened into the client's namespace. Data types that share a simple name across different namespaces are now generated separately rather than one overwriting the other.
- Player scoped Cloud Behaviours now send events.
- Cloud Behaviours now support sharing code via a third asmdef.

### **Breaking Changes**:
- Generated Cloud Behaviour clients no longer mark synchronized members with `[field: SerializeField]`. Unity's serializer cannot represent most backend-legal types, so per-member serialization silently persisted only a subset of the synchronized state. The custom Inspector is unaffected (it reads live members directly).
- `readonly` fields on Cloud Behaviour data types are no longer captured and now report CCSG131.
- `HydrationFailedOnConnect` moved from `Error` to `Fatal` as a `CloudBehaviourExceptionReason`: a hydrate that fails when the scope resolves disables the client and raises no `ScopeChanged`. Call `EnableClient()` from outside the handler to try again.
- Cloud Behaviours: removed the `[VisibilityFilter]` attribute. Per-viewer synchronization is now expressed with `[SyncView]` projection methods (see Added); migrate a filtered field to a projection that returns the value each viewer should see.

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
- A long-running local Cloud Code server no longer loses access to Unity services.
- Clearing the local Cloud Code server's state now deletes the state the server actually writes. It targeted `Orleans/GrainState/v1`, a path the server stopped using, so the Cloud Code toolbar's clear-state action left every scope's persisted state in place.

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

## [2.10.4] - 2026-06-23

### Fixed
- Added retries with a longer request timeout when checking whether a Cloud Code module exists during deployment.

### Changed
- Generating bindings will now includes enum values alongside their names.
- Generating bindings dictionary keys now retain their original types instead of being converted to strings.

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
