# Basic Rooms And Lobbies Demo

This demo shows the MST master, spawner, room, lobby, and client flow with a small Mirror room
scene. It is intended to demonstrate registration and access data, not production gameplay.

## Scene Roles

- `Scenes/Master/Master.unity` starts the master modules used by lobbies, rooms, and spawning.
- `Scenes/Spawner/Spawner.unity` registers a spawner and launches the configured room executable.
- `Scenes/Room/Room.unity` runs the room server and registers it with the master.
- `Scenes/Client/Client.unity` connects, joins the flow, and requests room access.
- `Prefabs/--ROOM_SERVER.prefab` contains the room-server setup used by the room scene.
- `Prefabs/Arena.prefab` is the small networked gameplay sample.

## Room Info Panel

The panel displays room information after either client room access data or room registration data
becomes available.

### Room ID Text

Required `TMP_Text` reference. Its content is replaced with the registered room ID.

### Room Scene Name Text

Required `TMP_Text` reference. On a client it displays the scene name received in room access data.
On a room server it displays `-mstRoomOnlineScene`, falling back to the active scene name.

### Room Max Players Text

Required `TMP_Text` reference. It displays the maximum player count from access data or the
registered room options.

## Room HUD View

The HUD becomes visible when the local Mirror player character exists. Pressing Escape opens the
MST players list for the room identified by the current room access data.

## Setup

1. Configure and start the master scene.
2. Configure the spawner with a room executable built from `Scenes/Room/Room.unity`, or run the room
   scene directly for local testing.
3. Ensure the room uses the same master address, port, and permission credentials as the master.
4. In the room scene, assign all three `RoomInfoPanel` text references.
5. Start the client scene, connect, and enter a lobby or room through the demo UI.

## Validation

1. Confirm that the spawner and room register without permission errors.
2. Confirm that the room panel shows a non-empty room ID, the expected scene, and the configured
   maximum player count.
3. Join with a client and confirm that the same access data is displayed.
4. Enter the arena and confirm that the room HUD appears after the local player spawns.
5. Press Escape and confirm that the players list opens for the current room.

## Limitations

- The room info panel waits up to 10 seconds for client room access data.
- Missing UI references are not recovered at runtime.
- The sample uses simple scene-bound UI and input intended for demonstration.
- Production projects must provide their own room lifecycle, authorization policy, failure UI, and
  gameplay authority rules.
