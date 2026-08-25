# Rooms Module

The rooms module manages registered room servers and player access to those rooms.

## Key Files

- `RoomsModule.cs` - authoritative master-side room registry and access flow.
- `RoomsClient.cs` - player-side facade for requesting room access.
- `RoomsServer.cs` - room/server-side facade for registering rooms and validating access.
- `RoomServerManager.cs` - room-process component that registers the room and validates joining players.
- `RoomClientManager.cs` - sample client component that connects to a room after receiving access.
- `RoomController.cs` - room-process controller returned after registration.
- `RegisteredRoom.cs`, `RoomOptions.cs`, `RoomPlayer.cs` - runtime room state and options.
- `Packets` - room access, validation and option packets.

## Flow

A room process connects to master, optionally registers a spawned process, registers room options,
and receives a room id. A client requests access to a room. Master asks the room registrar through
`ProvideRoomAccessCheck`; the room returns a `RoomAccessPacket` with address, port, token and scene.
The client connects to the room and the room validates the token with master.

## Rules

- `Clean Rate` is the realtime interval in seconds for invalid-room and unconfirmed-access cleanup.
  Keep it positive; reducing it increases cleanup frequency and master-side scan work.
- `Player Session Release Timeout Seconds` is the maximum realtime wait for an active room to persist
  and release a player during a validated account takeover. It must cover profile persistence and
  leave acknowledgement; the default is 30 seconds.
- `RoomServer.Room Server Manager` owns room registration and joined-player state. If unassigned, the
  server searches the same GameObject.
- `RoomServerManager.Auto Load User Profile` attaches the authoritative profile before the joined
  event. Disable it only for rooms without MST profiles or with a custom load flow.
- Room manager Unity events report registration success/failure and completed player join/leave
  transitions. The registration failure reason remains in the MST log.
- `RoomClient.Room Connection Timeout` is realtime seconds. Its Editor auto-start credentials and
  guest mode are ignored by standalone builds.
- Only the room registrar peer may destroy or mutate its room options.
- Access tokens are short-lived and must be validated by the room.
- Room extra params use the `-room.` prefix and are carried through `MstProperties`.
- `RoomServerManager` bridges master identity/profile to room-local player ids.
- Blocking a logged-in account is propagated from the master to its active room by stable account id.
  `RoomsServer.OnAccountBlockedEvent` lets the room networking integration disconnect the matching
  room-local connection without coupling MST to Mirror, FishNet, or another gameplay transport.
- `RoomsModule.ReleasePlayerSessionAsync` is the confirmed boundary used before an external identity
  takeover. Missing or disconnected rooms clear only the matching stale `JoinedRoomID`. An active room
  receives the exact account id and old master peer id through `RoomPlayerSessionPacket` and must
  complete `RoomsServer.PlayerSessionReleaseHandler` only after its authoritative profile save and
  `RoomController.NotifyPlayerLeft` acknowledgement have succeeded. A success response is accepted by
  the master only when the old room no longer owns that user; a newer room assignment is preserved.
- Room release requests are idempotent. If the exact local player has already gone, the room should
  repeat `NotifyPlayerLeft` and wait for its acknowledgement. If the master peer id now belongs to a
  different account, the stale request must fail instead of disconnecting the newer player.
- Destroy invalid rooms when their registrar peer disconnects.
- A room unregister request that reaches a transport already closing with `NotConnected` completes
  local destruction successfully. Master removes the authoritative room through registrar-peer
  disconnect cleanup; other unregister failures remain errors.
- Room ids are allocated atomically because registration handlers may execute concurrently.
- `RegisteredRoom` serializes capacity reservation, token confirmation, player membership, option
  replacement and destruction through one state owner. Consumers enumerate immutable player snapshots
  through `GetPlayersSnapshot()` and receive independent `RoomOptions` copies through
  `GetOptionsSnapshot()`. Each options call clones the current value, so cache one snapshot for a single
  logical operation instead of requesting individual fields repeatedly.
- `RoomOptions.Clone()` uses a shallow CLR object clone plus a deep copy of `ExtraParameters`. The room
  registry clones options on input, replacement and output, so callers cannot mutate registered state.
- Module-owned rooms pass through `Registering`, `Active` and `Destroyed`. Registration and destruction
  publication share the room event queue, so the complete registration callback always precedes the
  destruction callback, including reentrant destruction from a registration subscriber.
- Registration is closed atomically when `StopServerRunAsync()` begins. A registration that linearized
  before the barrier is destroyed by that stop; later registrations are rejected.
- Destroying a room is a one-way barrier: late access ACKs cannot recreate tokens or players.
- Each pending access request owns a unique reservation identity. A canceled request's late ACK cannot
  release a newer reservation made by the same peer.
- Room destruction resets a player's `JoinedRoomID` before callbacks only when it still points to that
  room; a newer room assignment is preserved.
- A delayed leave from an older room follows the same rule and cannot clear the player's newer
  `JoinedRoomID`.
- `GetPublicGames` builds address, capacity and public properties from one `RoomOptions` snapshot. MST5
  custom modules must override the four-argument `GetPublicRoomOptions` overload that receives this
  snapshot. The three-argument overload remains a direct-call compatibility entry point and is not the
  listing override point.
- Canceling a pending access request releases its reserved capacity only when cancellation wins the
  completion race. Once the room ACK owns completion, late cancellation cannot remove its reservation.
- `RegisteredRoom.Destroy()` delegates to its owning `RoomsModule` when registered, so the global room
  index, registrar index, player state and destruction events are removed through one path.
- Lobby room binding accepts status/finalization only from its current `SpawnTask`; a callback already
  captured by a replaced task cannot mutate the new lobby run.
- The generic `RoomServerManager`/`RoomPlayer` sample path remains available but must be reassessed
  against the current game room implementation before it is treated as the MST5 production example.
