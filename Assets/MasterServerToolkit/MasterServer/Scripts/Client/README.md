# Client Runtime

This folder owns reusable Unity client behaviours and socket binding helpers.

## Key Files

- `BaseClientBehaviour.cs` - MonoBehaviour client base. Creates/rebinds an `IClientSocket`,
  stores registered packet handlers and initializes child `IBaseClientModule` components.
- `BaseClientModule.cs` - child-module base used by client-side behaviours.
- `ClientToMasterConnector.cs` - auto-connect component that reads master IP/port from `Mst.Args`.
- `ConnectionHelper.cs` - generic connect/close/reconnect helper with lifecycle hooks.
- `IConnectionPermissionCredentials.cs` - optional socket contract for one connection-scoped
  permission key and credential pair.
- `IMstBaseClient.cs` and `IBaseClientModule.cs` - common contracts for facades/components.

## Rules

- Handler registration must go through the owner connection so handlers can be removed/rebound.
- Unregister connection listeners and message handlers on destroy.
- Do not assume `Mst.Connection` is always the room connection. Room flows can replace/reuse sockets.
- UI code should subscribe to client events and unsubscribe in `OnDestroy`.
- `ConnectionHelper` owns retry and reconnect scheduling. `OnFailedConnectEvent` and
  `OnDisconnectedEvent` are notification hooks; do not wire them back to `StartConnection`
  in the Inspector as the reconnect mechanism.

## Inspector Configuration

- `BaseClientBehaviour` and `BaseClientModule` log levels are independent. `Init Modules At Start`
  initializes every child `IBaseClientModule`; disable it only when project code performs that step.
- `ConnectionHelper` needs a host, WebSocket TCP port and service path matching the server. Secure mode
  selects WSS and can be overridden by `-mstUseSecure`.
- `Permission Key` and `Permission Credential` configure an override for this helper's socket only.
  A non-empty connection value takes priority over runtime `-mstPermissionCredentials` for its exact
  key. Use `default` for the initial handshake; other exact keys are used only when code explicitly
  requests that permission. The credential is serialized into the build and must not be treated as
  protected storage. An empty value restores runtime-config and built-in fallback behavior.
- Auto-connect delay, attempt timeout and reconnect delays are realtime seconds. `0` means immediate
  where the Inspector allows it. One connection cycle performs `Max Attempts To Connect` attempts.
- `Reconnect On Disconnect` handles a lost established connection. `Reconnect On Failed Connect`
  starts another cycle after every attempt in the current cycle fails.
