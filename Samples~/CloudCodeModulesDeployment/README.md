# Cloud Code Modules

This sample shows how to write a Cloud Code Module in C# in the Unity Editor, deploy it, and call it from a scene through generated client bindings. It covers two things most games need from a backend: state that persists per player, and work that runs later on a timer.

A module is server-side C# that Cloud Code runs for you. The game calls its functions on behalf of a signed-in player; players cannot see or change the code.

## Requirements

- Unity 6000.7 or newer.
- `Edit > Project Settings > Player > Api Compatibility Level` set to `.NET Standard 2.1`.

## What is in the sample

| Path | Role |
| --- | --- |
| `CloudCode/Cloud/HelloSample.cs` | The module. Runs on Cloud Code. |
| `CloudCode/Client/HelloSampleClient.cs` | Empty partial class the source generator fills with one typed method per module function. Do not edit. |
| `CloudCode/HelloSample.ccmu` | The deployable asset the Deployment window uploads. |
| `CloudCodeModulesHello.cs` | The scene script. Signs in, then calls the module. |

The module and the client are separate assemblies on purpose: the module must only contain code that can run on Cloud Code, so it has no engine references.

## Upgrading from the `.unitypackage` sample

Earlier versions of this sample installed as an embedded package under `Packages/com.unity.samples.ccm-deployment`. This version imports under `Assets/Samples` and keeps the same asset GUIDs and most assembly names, so delete the old folder before importing or duplicate assemblies such as `HelloSampleClient` will fail to compile.

## Using the sample

### Deploy the module

1. Link your Unity project in `Project Settings > Services`.
2. Select an environment in `Project Settings > Services > Environments`.
3. Deploy `CloudCode/HelloSample.ccmu` from the [Deployment window](https://docs.unity3d.com/Packages/com.unity.services.deployment@1.7/manual/deployment_window.html).

The module is live as soon as the upload finishes. The first call after a deploy can take longer while Cloud Code loads it.

### Play the scene

Open the `CloudCodeModules` scene and click `Play`. The scene signs in anonymously before enabling the buttons.

**Immediate greeting.** Enter a name and click `Invoke Cloud Code Module`. The module returns `Hello, [name]! You have been greeted N time(s).` Click again and N goes up: `GreetingCount` lives in the module and Cloud Code stores it per player between calls.

**Delayed greeting.** Set a delay and click `Schedule Greeting`. The module registers a one-shot timer. The scene polls `GetGreetingStatus` once a second, showing the seconds left, then the greeting once Cloud Code has run `DelayedGreeting`.

### Iterate

Edit `HelloSample.cs`, deploy again, and play. The generator updates `HelloSampleClient` when the module compiles, so a changed function shows up as a compile error in the scene rather than a runtime failure.

## How it works

- **Functions.** Each `[CloudCodeFunction("Name")]` method represents an RPC. `client.SayHello(name)` in the scene sends a request to Cloud Code naming `SayHello`, and Cloud Code runs the method and returns the result. The generated client gives each function a typed method so the request is never built by hand.
- **State.** `[StateScope(Scope.Player)]` means Cloud Code stores the module's public fields separately for each player. When a player calls a function, their saved values are loaded into a fresh `HelloSample`, the function runs, and the fields are saved back under that player. Two players calling `SayHello` see two independent counters, and a timer registered by one player later runs against that player's fields. Without a scope, fields reset on every call.
- **Timers.** `ScheduleGreeting` receives `ITimerService` through `[CloudCodeService]`; Cloud Code injects it. When the timer elapses, Cloud Code calls `DelayedGreeting` as the service, which is why that function is `Access.Service` and the generated client has no method for it.
- **Logs.** `HelloSample` asks for `ILogger<HelloSample>` in its constructor and Cloud Code supplies it. After a call, open `Services > CloudCode > Observability Logs` to see the lines it wrote. The delayed greeting has no client waiting on it, so its log line is the way to confirm the timer fired.
- **Polling.** A module cannot push to the game, so the scene polls for the result.

## Learn more

- [Cloud Code Modules manual](https://docs.unity.com/en-us/cloud-code/3.0.0/modules/overview)
- [Stateful Cloud Code and scopes](https://docs.unity.com/en-us/cloud-code/3.0.0/modules/stateful-cloud-code)
- [State serialization](https://docs.unity.com/en-us/cloud-code/3.0.0/modules/stateful-cloud-code/state-serialization)

## Troubleshooting

**The Deployment window is empty.** Link the project in `Project Settings > Services` and select an environment.

**`Could not call module endpoint` in the console.** The module is not deployed to the environment the scene uses, or is still loading. Check the Deployment window and retry after a few seconds.

**The status label never leaves `Pending`.** The timer fires on Cloud Code, so stay in play mode and keep polling. If it is still pending long after the delay, redeploy and try again.
