# MasterServer

`MasterServer` contains the reusable backend and runtime framework surface of MST.
It owns the static `Mst` facade, server/client module facades, server behaviours, database
interfaces, events, logging, mail/localization, argument parsing, opcodes and all built-in
master modules.

## Important Folders

- `Scripts/Mst` - global facade and shared primitives such as `MstArgs`, `MstProperties`,
  `MstSecurity`, `MstThread`, `MstCreate`, `MstRuntime` and traffic statistics.
- `Scripts/Server` - server lifecycle, peer acceptance, security handshake and module initialization.
- `Scripts/Client` - reusable client behaviours and auto-connect helpers.
- `Scripts/Modules` - authoritative server modules and matching client/server facades.
- `Scripts/Database` - provider-independent accessor registry and accessor factory base.
- `Scripts/Logger` - MST logging infrastructure and appenders.
- `Scripts/Events` - `Mst.Events` event channel and the legacy event channel.
- `Scripts/Keys` - opcodes, packet parameter names, event keys and peer property keys.

## Runtime Shape

`Mst` initializes the framework once per runtime and resets editor static state through
`RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)` plus editor
play-mode callbacks. It owns the default `IClientSocket`, `Mst.Client`, `Mst.Server`,
`Mst.Security`, `Mst.Events`, `Mst.Args`, `Mst.Localization`, `Mst.Thread` and helpers.

Server processes normally use a `ServerBehaviour` implementation with child `BaseServerModule`
components. Client/room/spawner processes connect through `IClientSocket` and use `Mst.Client`
or `Mst.Server` facade methods depending on their role.

## Change Rules

- Keep this layer game-agnostic.
- Add config keys to `MstArgNames` and parse common values through `MstArgs`.
- Add wire opcodes to `MstOpCodes`.
- Keep packet serialization stable.
- Server handlers are authoritative. Do not trust client-supplied account, profile, room,
  lobby or spawn data without server-side checks.
- Do not touch Unity scene objects from async master handlers unless marshalled to the main thread.
