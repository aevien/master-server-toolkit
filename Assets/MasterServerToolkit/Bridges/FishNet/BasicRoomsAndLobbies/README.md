# FishNet Basic Rooms and Lobbies Sample

This folder contains the MST room-flow sample built on FishNet. It demonstrates how an MST room process configures and starts FishNet, validates room access, connects a client from `Mst.Client.Rooms`, loads room scenes, and gives the owning character local camera and input control.

The folder is an integration sample. FishNet package internals are outside this folder and are not modified by the sample.

## Runtime Ownership

| Area | Owner | Responsibility |
| --- | --- | --- |
| Room registration and access | MST room process | `RoomServerManager` supplies room options, registers the process, and validates access tokens. |
| Room transport and connections | FishNet server | `RoomNetworkManager` applies the MST room address, port, and player limit, then starts FishNet. |
| Access handshake | FishNet server and connecting client | The room manager receives the client's MST token and returns an access result before gameplay continues. |
| Scene transition | FishNet scene manager on the client | `RoomClientManager` configures the online/offline scenes and reports loading progress. |
| Character input and camera | Owning client | Input and camera components run only for the FishNet owner. |
| Shared character state | Server plus observers | Server-side methods approve shared state and observer RPCs notify clients. |
| Death notifications | Server | `PlayerCharacterVitals` starts death/alive changes and notifies observers. |

## Room Setup

1. Add `RoomServerManager`, FishNet `NetworkManager`, FishNet `DefaultScene`, the selected transport, and this sample's `RoomNetworkManager` to the room-server object.
2. Assign `RoomServerManager` and FishNet `NetworkManager`, or leave either empty when it is available on the same GameObject.
3. Configure FishNet's spawnable player prefab and scene-management components.
4. Add `RoomClientManager` to the client bootstrap scene.
5. Ensure the Offline Room Scene is included in the player build.
6. Keep character input and cameras owner-only. Remote observers must not poll local input.

The room IP, port, maximum players, and online scene come from MST room options and arguments. They are process configuration and are intentionally not duplicated as Inspector settings.

## Room Inspector Settings

### RoomNetworkManager

| Setting | Default | Meaning |
| --- | --- | --- |
| Room Server Manager | Required reference | MST room lifecycle owner. When empty, `Awake` searches the same GameObject. |
| Network Manager | Required reference | FishNet manager used for transport, server lifecycle, scenes, and connections. When empty, `Awake` searches the same GameObject. |
| Log Level | `Info` | Minimum MST log severity. It affects diagnostics only. |

### RoomClientManager

| Setting | Default | Meaning |
| --- | --- | --- |
| Offline Room Scene | `Client` | Scene loaded after a normal client disconnect. Zone changes preserve their own destination. The scene must be available to the build. |

## Character Setup

The FPS and top-down prefabs use the `PlayerCharacter*` components. Required references should point to components on the same network-character hierarchy unless a setting explicitly describes a camera, visual object, or effect prefab.

### PlayerCharacterBehaviour

| Setting | Default | Meaning |
| --- | --- | --- |
| Log Level | `Info` | Minimum MST log severity for the component. It does not alter FishNet ownership or simulation. |

### PlayerCharacterAvatar

| Setting | Default | Meaning |
| --- | --- | --- |
| Remote Parts | Empty array | Meshes or objects hidden for the owning client and shown for remote clients. Assign body parts that would obstruct a first-person camera. |

### PlayerCharacterLook

| Setting | Default | Meaning |
| --- | --- | --- |
| Look Camera | Empty | Camera controlled by the owner. Concrete look components fall back to `Camera.main` or create one. |
| Input Controller | Required | Owner-only input source for look, zoom, and aiming. |
| Movement Controller | Required | Movement component used by the camera readiness check. |
| Reset Camera After Destroy | `true` | Legacy serialized value. The current base implementation restores the camera when ownership changes regardless of this value. |
| Rotation Sencitivity | `3` | Top-down yaw degrees per mouse-axis unit. |
| Collision Dstance Smooth Time | `5` | Collision-distance response multiplier; larger values react faster. |
| Use Collision Detection | `true` | Prevents the owner's top-down camera from passing through scene colliders. |

The misspelled serialized field names are retained to preserve existing prefab data.

### PlayerCharacterFpsLook

| Setting | Default | Meaning |
| --- | --- | --- |
| Camera Point | `(0, 1.75, 0.15)` | Camera offset in character-local world units. |
| Look Sensitivity | `(8, 8)` | Yaw and pitch applied per legacy input-axis unit. |
| Min Look Angle | `-60` | Lowest pitch in degrees. `0` prevents downward rotation. |
| Max Look Angle | `60` | Highest pitch in degrees. `0` prevents upward rotation. |
| Use Smoothness | `true` | Smooths owner camera and character rotation. |
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
| Apply Offset Distance | `true` | Moves the camera pivot ahead of a moving owner. |
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
| Jump Is Allowed | `true` | Allows owner jump input. |
| Jump Power | `8` units/second | Initial upward speed. `0` produces no lift. |
| Jump Rate | `1` second | Minimum delay between jumps. `0` removes the time delay. |
| Input Controller | Required | Owner-only input source. |
| Character Controller | Required | Applies character displacement. |
| Look Controller | Required | Supplies facing direction for top-down movement. |
| Rotation Smooth Time | `5` | Rotation response multiplier; larger values turn faster. |

### PlayerCharacterVitals

| Setting | Default | Meaning |
| --- | --- | --- |
| Character Controller | Required | Disabled on the server and owner when the character dies. |
| Die Effect Prefab | Empty | Optional effect instantiated on every observing client one second after death notification. |

## Validation

- Start the room through MST and confirm that FishNet binds the expected address and port.
- Confirm that maximum clients matches the MST room options.
- Join with a valid MST room token and confirm access.
- Join with an invalid token and confirm disconnect.
- Confirm that the configured online scene loads and the loading view reaches completion.
- Disconnect normally and confirm that Offline Room Scene loads.
- Confirm that only the owner reads input and controls its camera.
- Confirm that remote avatar parts remain visible while first-person obstructing parts are hidden for the owner.
- Test top-down camera collision, zoom limits, and pixel padding at the target resolutions.
