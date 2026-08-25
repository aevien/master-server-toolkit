# Master Server Toolkit Agent Contract

This file is for coding agents and maintainers working inside `Assets/MasterServerToolkit`.
It describes the current framework boundary and rules that are not obvious from a single file.

## Scope

MST is reusable framework code. It must stay independent from the game that embeds it.
Do not reference project gameplay classes, economy data, scene-only objects, or project-only
ScriptableObjects from MST core.

Use this ownership split:

- generic networking, modules, server/client flows, config and persistence contracts live in MST;
- platform-specific behavior lives in `GameServiceBridge` or a matching service folder;
- database-provider code lives in `Bridges/<Provider>`;
- demo scene behavior lives in `Demos` or bridge demo folders;
- game-specific policy belongs outside `Assets/MasterServerToolkit`.

## Before Changing Code

1. Read this file and the nearest README in the target folder.
2. Inspect the local pattern before editing.
3. Identify whether the change is generic MST, a bridge, a service implementation, or demo-only.
4. Preserve binary/wire/config compatibility unless a breaking change is explicitly requested.
5. Do not run Unity compilation, batchmode, or builds unless the user explicitly asks.

Documentation-only updates may be made directly when requested.

## Runtime Rules

- Use `MasterServerToolkit.Logging` for runtime/server output.
- Prefer an existing module logger, `Mst.Create.Logger(GetType().Name)`, or `Logs` for static helpers.
- Do not use raw `Debug.Log` in runtime/server code except editor tooling or third-party fallback paths.
- Message opcodes belong in `MstOpCodes`.
- MST config/argument keys belong in `MstArgNames`; read them through `Mst.Args`.
- Use `MstProperties` for lightweight string metadata and packet options.
- Treat message handlers on the master server as potentially background/async code. Do not touch
  Unity scene objects, ScriptableObjects, or main-thread-only APIs from those handlers unless the
  call is explicitly marshalled to the Unity thread.
- Treat static state, static events, sockets, timers, pending callbacks, singleton references and
  global registries as unsafe with disabled Domain Reload.
- Fix lifecycle issues by unregistering/disposal/re-registration, not by broad static clearing that
  can break live objects when Scene Reload is disabled.

## Networking Contracts

- `IIncomingMessage.Respond(...)` answers the current request and carries the original ack id.
- `IPeer.SendMessage(..., ResponseCallback)` allocates an ack id and times out through `MstTimer`.
- Do not change packet serialization order without a migration plan.
- Do not introduce module-local opcode containers for core MST modules.
- Always validate authority server-side before mutating auth, profile, room, lobby, spawn or admin state.

## Module Contracts

- Server modules derive from `BaseServerModule`, register handlers during `Initialize(IServer)`,
  and declare hard/optional dependencies in `Awake()`.
- Client/server facades deriving from `MstBaseClient` store handlers and can rebind to another
  `IClientSocket` through `ChangeConnection`.
- Database implementations must register accessors through `Mst.Server.DbAccessors`.
- Service modules derive from `BaseServiceModule` and must explicitly set `IsSupported = true`
  only when the feature is available for the active service.
- `IPlayerModule.IsAuthenticationSupported` means platform authentication support only. It does
  not mean regular MST username/password login.

## Cleanup Requirements

When adding subscriptions or long-lived work, define the matching cleanup path in the same change:

- MST events: keep the returned `IDisposable` token and dispose it.
- Socket handlers: unregister by exact handler/token when available.
- Unity events and static events: unsubscribe in `OnDestroy`, `Dispose`, or equivalent lifecycle.
- Coroutines/timers/throttle/debounce dispatchers: cancel or dispose on owner teardown.
- Database, file, HTTP and process resources: dispose or close explicitly.

## Folder Navigation

- Start with `MasterServer/README.md` for backend/runtime architecture.
- Start with `Networking/README.md` for packet, peer, socket and transport behavior.
- Start with `GameServiceBridge/README.md` for platform service behavior.
- Start with `Bridges/README.md` for database/network-framework integrations.
- Start with `Tools/README.md` for shared utility and UI/editor helper behavior.
