# Lobbies Module

The lobbies module manages pre-game groups, teams, ready states, lobby chat and optional game
server spawning.

## Key Files

- `LobbiesModule.cs` - authoritative lobby registry and lobby request handlers.
- `LobbiesClient.cs` and `JoinedLobby.cs` - client-side lobby facade and live lobby event state.
- `LobbiesServer.cs` - server-side facade for lobby info/member lookup.
- `ILobby`, `ILobbyFactory`, `BaseLobby`, `BaseLobbyAuto` - extension surface for lobby behavior.
- `LobbyConfig`, `LobbyTeam`, `LobbyMember`, `LobbyUserPeerExtension` - lobby state.
- `Packets` - lobby state, team, member, property and chat packets.

## Flow

Clients create lobbies through registered factories, join by id, update lobby/member properties,
switch teams, toggle ready state, send lobby chat, start game manually or receive room access after
the lobby spawns/finalizes a room.

## Extension Points

- Add lobby variants by implementing `ILobbyFactory` and returning a custom `ILobby`.
- Override `BaseLobby` methods for team selection, player admission, editable properties,
  generated room options and start rules.
- Use `LobbiesModule.AddFactory` before clients create that lobby type.
- Use `ILobby.GetMembersSnapshot()` when enumerating members outside a lobby operation.

## Inspector Configuration

- `Create Lobbies Permission Level` is the minimum numeric peer level allowed to create a lobby.
  `0` permits normal connected clients.
- `Dont Allow Creating If Joined` prevents a user from creating another lobby while `CurrentLobby`
  is occupied and should remain enabled with the current single-lobby user state.
- `Joined Lobbies Limit` is reserved for a future multi-lobby model. MST5 currently stores one
  `CurrentLobby` and does not enforce values above `1`.

## Rules

- Lobby state is authoritative on master.
- `LobbyUserPeerExtension.CurrentLobby` must be kept in sync on join/leave/disconnect.
- Broadcast packets only to subscribers.
- If using spawners/rooms, keep those modules optional but verify they exist before starting games.
- Lobby ids are allocated atomically because creation handlers may execute concurrently.
