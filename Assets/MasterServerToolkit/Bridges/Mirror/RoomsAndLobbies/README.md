# Mirror Rooms and Lobbies Sample

This folder contains the MST room-flow sample built on Mirror. It demonstrates how an MST room process starts Mirror, validates room access, connects a client from `Mst.Client.Rooms`, and gives the owning player local camera and input control.

The code is sample integration code, not Mirror framework internals. Files under `Assets/Mirror` are not part of this sample and must not be modified to configure it.

## Runtime Ownership

| Area | Owner | Responsibility |
| --- | --- | --- |
| Room registration and access | MST room process | `RoomServerManager` supplies room options, registers the process, and validates access tokens. |
| Room transport and connections | Mirror server or host | `RoomNetworkManager` starts the server, applies the MST room address, port, and player limit, and reports peer lifecycle events. |
| Authentication handshake | Mirror server and connecting client | `RoomAuthenticator` validates the client's MST room token before accepting the Mirror connection. |
| Character input and camera | Owning client | Input and camera components must not be driven for remote players. |
| Character state | Server plus observing clients | Server-side methods approve shared state; Mirror messages and network components propagate it to clients. |
| Death notifications | Server | `PlayerCharacterVitals` starts death/alive changes and notifies observers. |

## Room Setup

1. Add `RoomServerManager`, `RoomNetworkManager`, the selected Mirror transport, and `RoomAuthenticator` to the room-server object.
2. Assign the same `RoomServerManager` to `RoomNetworkManager` and `RoomAuthenticator`.
3. Assign `RoomNetworkManager` to `RoomAuthenticator`, or leave it empty when both components are on the same object.
4. Configure Mirror's player prefab and scene fields on `RoomNetworkManager`.
5. Add `RoomClientManager` to the client bootstrap scene. It reads the target address, port, and secure-connection flag from `Mst.Client.Rooms.ReceivedAccess`.
6. Keep the character's input and camera components local-owner only. Do not let remote clients poll input.

`RoomNetworkManager` obtains the room port, address, maximum players, and online scene from MST room options and command-line arguments. These values are process configuration, not duplicated Inspector settings.

## Room Inspector Settings

### RoomNetworkManager

| Setting | Default | Meaning |
| --- | --- | --- |
| Room Manager | Required reference | MST room lifecycle owner. When empty, the component searches the same GameObject during `Awake`. |
| Log Level | `Info` | Minimum MST log severity. It affects diagnostics only. |

### RoomAuthenticator

| Setting | Default | Meaning |
| --- | --- | --- |
| Log Level | `Info` | Minimum authenticator log severity. It does not change access policy. |
| Room Manager | Required reference | Server-side source for room activity and token validation. |
| Network Manager | Same GameObject | Mirror room manager associated with the authenticator. |

### RoomClientManager

| Setting | Default | Meaning |
| --- | --- | --- |
| Log Level | `Info` | Minimum client-room log severity. |
| Room Manager | Legacy reference | Retained for existing sample prefabs. The current connection path reads access from `Mst.Client.Rooms`. |

## Character Samples

The folder contains two related sample character surfaces:

- `PlayerCharacter*` is the basic FPS/top-down sample.
- `Player`, `PlayerMovement`, and `PlayerLook` demonstrate buffered input, client prediction, and server reconciliation.

Use one complete surface consistently on a prefab. Every required component reference must point to the same network-character hierarchy unless a setting explicitly describes a camera or visual object.

## Shared Character Settings

### PlayerCharacterBehaviour and PlayerBehaviour

| Setting | Default | Meaning |
| --- | --- | --- |
| Log Level | `Info` | Minimum MST log severity for the component. It does not change networking or simulation. |

### PlayerCharacterAvatar and PlayerAvatar

| Setting | Default | Meaning |
| --- | --- | --- |
| Remote Parts | Empty array | Meshes or objects hidden for the owning client and shown for remote clients. Assign body parts that would obstruct a first-person camera. |

### PlayerCharacterLook

| Setting | Default | Meaning |
| --- | --- | --- |
| Look Camera | Empty | Camera controlled by the owning client. Concrete look components fall back to `Camera.main` or create one. |
| Input Controller | Required | Local input source for look, zoom, and aiming. |
| Movement Controller | Required | Movement component used by the camera readiness check. |
| Reset Camera After Destroy | `true` | Legacy serialized value. The current base implementation restores the camera when authority stops regardless of this value. |
| Rotation Sencitivity | `3` | Top-down yaw degrees per mouse-axis unit. |
| Collision Dstance Smooth Time | `5` | Collision-distance response multiplier; larger values react faster. |
| Use Collision Detection | `true` | Prevents the local top-down camera from passing through scene colliders. |

The misspelled serialized field names are retained to preserve existing prefab data.

### PlayerCharacterFpsLook

| Setting | Default | Meaning |
| --- | --- | --- |
| Camera Point | `(0, 1.75, 0.15)` | Camera offset in character-local world units. |
| Look Sensitivity | `(8, 8)` | Yaw and pitch applied per legacy input-axis unit. |
| Min Look Angle | `-60` | Lowest pitch in degrees. `0` prevents downward rotation. |
| Max Look Angle | `60` | Highest pitch in degrees. `0` prevents upward rotation. |
| Use Smoothness | `true` | Smooths local camera and character rotation. |
| Smoothness Time | `0.1` seconds | Approximate `SmoothDamp` response time; lower is faster. |

### PlayerCharacterTopDownLook

| Setting | Default | Meaning |
| --- | --- | --- |
| Look At Point | `(0, 0, 0)` | World-space offset added to the character position for the camera pivot and collision ray. |
| Follow Smooth Time | `2` | Follow response multiplier, not a duration; larger values follow faster. |
| Min Distance | `5` units | Minimum camera zoom distance. |
| Max Distance | `15` units | Maximum zoom distance. A value not greater than Min Distance is raised at runtime. |
| Start Distance | `15` units | Initial distance, clamped to the configured minimum and maximum. |
| Distance Smooth Time | `5` | Zoom/collision response multiplier. |
| Distance Scroll Power | `1` | Distance change per mouse-wheel input unit. |
| Apply Offset Distance | `true` | Moves the camera pivot ahead of a moving local character. |
| Max Offset Distance | `5` units | Maximum forward pivot offset. |
| Pitch Angle | `65` degrees | Fixed top-down camera pitch. |
| Min Horizontal Padding | `100` pixels | Left-edge safety area. |
| Min Vertical Padding | `100` pixels | Bottom-edge safety area. |
| Max Horizontal Padding | `100` pixels | Right-edge safety area. |
| Max Vertical Padding | `100` pixels | Top-edge safety area. |
| Use Padding | `false` | Suppresses forward offset when the character enters any configured screen-edge area. |

### PlayerCharacterMovement

| Setting | Default | Meaning |
| --- | --- | --- |
| Gravity Multiplier | `3` | Multiplies `Physics.gravity` while airborne. `0` disables airborne gravity. |
| Stick To Ground Power | `5` units/second | Downward grounded speed. `0` removes the extra grounding force. |
| Walk Speed | `5` units/second | Owning-client walking speed. |
| Run Speed | `10` units/second | Owning-client running speed. |
| Jump Is Allowed | `true` | Allows local jump input. |
| Jump Power | `8` units/second | Initial upward speed. `0` produces no lift. |
| Jump Rate | `1` second | Minimum delay between jumps. `0` removes the time delay. |
| Input Controller | Required | Local-owner input source. |
| Character Controller | Required | Applies character displacement. |
| Look Controller | Required | Supplies facing direction for top-down movement. |
| Rotation Smooth Time | `5` | Rotation response multiplier; larger values turn faster. |

### PlayerCharacterVitals

| Setting | Default | Meaning |
| --- | --- | --- |
| Character Controller | Required | Disabled on the server and owning client when the character dies. |
| Die Effect Prefab | Empty | Optional effect instantiated on observing clients one second after death notification. |

## Predicted Player Settings

### Player

Assign `PlayerInput`, `PlayerMovement`, `PlayerAvatar`, `PlayerLook`, and `CharacterController`. The owning client reads input and camera data; prediction and server reconciliation use the movement and controller references.

### PlayerLook

| Setting | Default | Meaning |
| --- | --- | --- |
| Look Camera | Required | Owning client's gameplay camera. |
| Player | Required | Player facade with input and movement references. |
| Camera Container | Required | Pivot used for camera position, rotation, and synchronized look direction. |
| Reset Camera After Destroy | `true` | Restores the camera's original hierarchy when the player is destroyed. |
| Look Sensitivity | `(8, 8)` | Yaw and pitch per input-axis unit. |
| Min/Max Look Angle | `-60` / `60` degrees | Local pitch limits. |
| Use Smoothness | `true` | Smooths local look input. |
| Smoothness Time | `0.1` seconds | Approximate local `SmoothDamp` response time. |
| Network Update Threshold | `1` degree | Minimum angle change before another update. `0` sends every detected change. |
| Network Interpolation Speed | `15` | Remote rotation response multiplier. |

### PlayerMovement

| Setting | Default | Meaning |
| --- | --- | --- |
| Player | Required | Facade providing input and `CharacterController`. |
| Gravity Multiplier | `3` | Airborne gravity multiplier; `0` disables it. |
| Stick To Ground Power | `5` units/second | Grounding speed; `0` removes it. |
| Walk/Run Speed | `5` / `10` units/second | Speeds used by prediction and server simulation. |
| Jump Is Allowed | `true` | Allows jump input. |
| Jump Power | `8` units/second | Initial upward speed. |
| Jump Rate | `1` second | Minimum jump delay. |
| Reconciliation Threshold | `0.1` units | Position error that triggers correction. `0` corrects every non-zero error. |
| Server Tick Rate | `0.1` seconds | Interval between movement inputs sent to the server. |
| Max Stored Inputs | `60` | Prediction samples kept for replay; larger values cover more latency and use more memory. |
| Enable Smoothing | `true` | Interpolates remote movement and corrections instead of snapping. |
| Smoothing Speed | `1` | Correction response multiplier. |
| Show Prediction Gizmos | `false` | Draws editor-only prediction diagnostics in the Scene view. |

## Validation

- Start the room through MST and confirm that it registers with the expected port and player limit.
- Join with a valid MST room token and confirm that Mirror accepts the connection.
- Join with an invalid token and confirm rejection.
- Confirm that only the owning client reads input and controls its camera.
- Confirm that remote avatar parts remain visible while first-person obstructing parts are hidden locally.
- Test camera collision, zoom limits, and screen padding in the top-down prefab.
- Test prediction with latency and confirm that reconciliation does not visibly oscillate.
