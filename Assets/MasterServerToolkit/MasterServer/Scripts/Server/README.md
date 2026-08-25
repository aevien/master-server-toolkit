# Server Runtime

This folder owns the generic master/server runtime: listening sockets, peer validation,
message dispatch, security handshake and module initialization.

## Key Files

- `ServerBehaviour.cs` - base server component. Creates the server socket, handles secure
  connection options, validates peers, accepts/rejects clients, tracks peers and initializes modules.
- `BaseServerModule.cs` - base class for authoritative modules. Provides logger, module id,
  dependency declarations, per-run lifecycle hooks and `Info`/`Details` JSON.
- `IServer.cs` and `IBaseServerModule.cs` - contracts consumed by modules.
- `SecurityInfoPeerExtension.cs` - per-peer permission state, connection/account permissions,
  GUID and bounded one-time encryption challenges.
- `PermissionEntry.cs` - inspector-defined permissions, secrets and built-in key/level constants.
- `Packets` - security/access packet contracts.

## Module Initialization

`ServerBehaviour` discovers modules from the scene/children, then initializes them in dependency
order. Hard dependencies must be declared with `AddDependency<T>()`; optional dependencies with
`AddOptionalDependency<T>()`. Optional dependencies are respected first, then skipped when they
block initialization.

`Initialize(IServer)` runs once for module configuration and handler registration. Work owned by a
specific transport run belongs in `StartServerRun(CancellationToken)` and `StopServerRunAsync()`.
The server stops run modules sequentially in reverse initialization order before closing the transport,
so a dependent module finishes before its dependency is stopped.

The public `AddModule`, `AddModuleAndInitialize` and `InitializeModules` APIs are configuration-time
operations. They reject calls while a server run is starting, running or stopping; otherwise a newly
initialized module would miss that run's `StartServerRun` ownership and matching shutdown callback.
The mutation check and registry update share the lifecycle lock, so a concurrent start cannot pass
between validation and insertion. A bound socket does not admit peers or messages until all initialized
run modules have started and the run reaches `Running` state.

## Inspector Configuration

- `Look For Modules` enables automatic `BaseServerModule` discovery. Disable it only when application
  code registers every module explicitly.
- `Look In Children Only` restricts that discovery to the server object's hierarchy; otherwise the
  loaded scene is searched.
- `Permissions` defines accepted permission keys, levels from `0` to `999`, and handshake secrets.
  Built-in entries are restored when removed.
- `Target Frame Rate` is the server update rate in frames per second and is overridable from command
  line configuration.
- `Server Ip`, `Server Port` and `Service` form the listening endpoint that clients must match.
- `Max Connections = 0` removes the MST connection-count limit.
- `Inactivity Timeout` and `Validation Timeout` are seconds. Keep them positive in production;
  non-positive values make peers immediately eligible for timeout handling.
- Secure mode requires a readable PFX file, its password and a compatible TLS protocol. Certificate
  command-line settings override the Inspector values.
- A server's `Log Level` and every `BaseServerModule.Log Level` are independent filters. Raising one
  does not change diagnostics emitted by the other components.

## Handler Rules

Use `server.RegisterMessageHandler(MstOpCodes.X, Handler)` in `Initialize`. Short synchronous handlers
may keep the `Task Handler(IIncomingMessage)` signature. Any handler that awaits external work should
use `Task Handler(IIncomingMessage, CancellationToken)` and pass that token through its dependency
chain. Always respond when the client expects a response and validate peer permissions or ownership
before mutating state.

The run token may be canceled from a worker thread so a blocking cancellation callback cannot freeze the
Unity lifecycle thread. Token registrations must only signal thread-safe state; Unity work belongs in the
awaited handler flow or in the stopped callbacks dispatched to the server owner thread.

`ServerBehaviour` creates one cancellation and task-ownership context per server run. It tracks the
complete task returned for every accepted message, including access rejection. Once shutdown starts,
new peers and messages are rejected and the run token is canceled. Expected cancellation is not logged
as a handler error and does not send a late error response.

`StopServerAsync()` is the controlled shutdown API: all callers share one completion task. It cancels
and drains tracked handlers, stops run modules, closes the socket and disposes peers, then publishes
stop callbacks on the server owner thread. Await it when code must know that module cleanup and final
persistence have completed.

`StopServer()` is the non-blocking Unity lifecycle fallback used by `OnDestroy` and
`OnApplicationQuit`. It immediately rejects new work, requests cancellation and starts transport
cleanup, then returns without waiting on the Unity thread. The remaining cleanup continues on the
shared shutdown task, but application quit, Editor Stop or a process crash provides no final-save
guarantee. Periodic persistence and purchase recovery must remain the primary data-safety mechanisms.
A new run cannot start while the old run context is still alive. Shutdown must be initiated outside a
message handler, because a handler cannot await a stop that is itself waiting for that handler.

If one or more run modules fail during shutdown, the server still stops the remaining modules, closes
the transport and publishes stop callbacks. After cleanup finishes, the shared `StopServerAsync()` task
faults with an `AggregateException` containing the module failures.

## Permission Rules

Permission levels are resolved by key through `IServer.TryGetPermissionLevel`. Code should use
`MstPermissionKeys` instead of hardcoded strings. `default`, `admin`, `room_server` and `spawner` are
built-in entries and are restored in that order if removed from the Inspector list. Each
`PermissionEntry` contains its key, numeric level and HMAC secret. Runtime peer permission levels are
clamped to the `0..999` range.

`-mstPermissionCredentials` is a JSON object that overrides secrets for permission keys already
declared in the master Inspector. It cannot create a permission or change its numeric level. Every
connection proves `default` first. A connected process can then request multiple exact permissions;
rooms request `room_server`, and spawners request `spawner` before their protected registration call.
The master stores all granted keys on the peer, and role-sensitive module APIs use
`SecurityInfoPeerExtension.HasPermission(key)` instead of inferring a role from the maximum numeric
level. `admin` remains account-assigned by `AuthModule` and cannot be requested through the HMAC
permission handshake.

Configured credential values are used literally in every runtime environment. If a key is omitted,
the server keeps the secret stored in its Inspector entry, while a connecting process uses the
built-in credential for that key. MST does not silently replace credentials based on whether the
process runs in the Editor or a standalone build.

This permission-key handshake is protocol version 2. Master, client, room and spawner binaries must
be updated together; mixed binaries fail the version check instead of silently interpreting the old
access-profile payload as a permission request.

Reserved level ranges:

- `0` - public/default client access.
- `1..20` - optional trusted user roles.
- `100..998` - trusted service and server processes.
- `999` - full admin access.

## Cleanup Rules

Cancellation-aware handlers must check the run token after every awaited provider call and before
mutating in-memory state or responding. Database and mail operations that already started are awaited
by controlled shutdown; cancellation suppresses subsequent handler work. The non-blocking fallback
may close the transport while an uncooperative handler is still finishing, so handlers must not rely
on sending a response after cancellation. If a module subscribes to server events, peer events, timers
or auth/profile events, it must unsubscribe in `StopServerRunAsync`, `OnDestroy`, or the matching
deterministic teardown method. Destroyed Unity modules are skipped if deferred cleanup reaches them
after scene teardown.
