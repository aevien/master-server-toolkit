# Demos

`Demos` contains sample scenes and scripts that show how MST can be used. It is not the framework
authority layer.

## Included Samples

- `BasicAuthorization` - account/login flow.
- `BasicConnection` and `BasicNetworking` - socket and message basics.
- `BasicHTTPServer` - built-in HTTP server example.
- `BasicProfiles` - observable profile and UI examples.
- `BasicRoomsAndLobbies` - room/lobby flow examples.
- `BasicChat` - chat module example.
- `BasicWorlds` - world/room sample.
- `BasicTelegramBotLogger` - custom logger/demo integration.

## Rules

- Demo code may be simpler and more scene-bound than framework code.
- Do not copy demo authority assumptions into `MasterServer/Scripts/Modules` without review.
- Keep demos useful for users, but place reusable framework fixes in the owning MST subsystem.
