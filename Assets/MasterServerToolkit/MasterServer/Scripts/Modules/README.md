# Server Modules

This folder contains built-in MST modules. Most modules include an authoritative server component
and one or more client/server facades that send requests over `IClientSocket`.

## Common Pattern

- The authoritative component derives from `BaseServerModule`.
- It declares dependencies in `Awake()`.
- It registers request handlers in `Initialize(IServer)`.
- It exposes state through `Details()` for dashboard/admin views.
- Matching facades derive from `MstBaseClient` and live next to the module.
- Packet classes derive from `SerializablePacket` and must keep serialization order stable.

## Built-In Modules

- `Authentication` - account registration, login, guest login, token login, password reset,
  email confirmation, account metadata and `IUserPeerExtension`.
- `Profiles` - observable profile schema, profile loading/saving, dirty updates between master,
  client and room servers.
- `Rooms` - room registration, public room listing, access token flow, room-player tracking.
- `Spawner` - spawner registration, spawn queues, room process launch/kill/finalization.
- `Lobbies` - lobby factories, teams, members, ready states, chat and optional room spawning.
- `Matchmaker` - aggregate public games/regions from room/lobby/spawner providers.
- `Chat` - channels, messages, permissions, invites, bans and user channel state.
- `Notification` - targeted and room-wide notification delivery.
- `RemoteConfig` - small remote key/value set exposed to clients/rooms.
- `WebServer` and `Dashboard` - HTTP listener, routes, controllers and admin UI/API pages.
- `Achievements`, `Leaderboards`, `Quests`, `AnalyticsModule`, `Censor`, `Ping`, `WorldRooms` - optional gameplay
  support modules that must stay generic.
- `Common` - shared packet types.

Profile collection updates received from a room remain forwardable on the master. Applying an
`ObservableDictionary` update records the same set/remove operations and marks the property dirty,
so the regular profile pipeline can persist the value and deliver it to the connected client. The
terminal client clears those transport updates after applying them because its loaded profile is
read-only and must not accumulate another outbound update queue.

## Authority Rules

Client packets are requests, not truth. Modules must verify:

- the peer is authenticated when user identity is required;
- permission level is sufficient for server/room/spawner operations;
- room/spawner/lobby ownership matches the mutating peer;
- payload sizes are bounded before applying profile or binary updates;
- game-specific policy is delegated through overrides/hooks, not hard-coded in MST.
