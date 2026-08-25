# Shared Bridge Helpers

This folder contains reusable demo UI, small demo packets and bridge helper behaviours.

## Main Areas

- `UI` - sample MST views for auth, rooms, lists, chat, notices and simple dialogs.
- `Manager` - helper behaviours such as matchmaking/profile loading wrappers.
- `Profile` - bridge/demo observable profile helpers.
- `Messages` and `Packets` - small demo network payloads.
- `Generics` and `Utils` - tiny helper interfaces/behaviours used by demos.

## Rules

- Shared bridge UI is sample/reference UI, not core MST authority.
- Do not place project-specific game policy here.
- Keep UI scripts tolerant of unsupported service modules and missing optional modules.
- Prefer core module facades over direct server state access from UI.

## Manager Settings

`MatchmakingBehaviour` wraps the client room-start flow:

- `Match Creation Timeout` is measured in seconds; `0` disables the client-side timeout.
- `Custom Spawn Options` are room properties sent with every spawn request in addition to options
  supplied by the current action.
- `On Room Started Event` runs after the room starts and client access is received.
- `On Room Start Failed Event` runs when creation, startup, or access acquisition fails.

`ProfileLoaderBehaviour.Populators Database` defines the local observable profile shape requested from
the master. Its success and failure events run only after the corresponding server response.

## Utility Settings

- `DelayedDestroyBehaviour.Delay Time` uses scaled seconds; `0` destroys the object at the end of the
  current frame.
- `HideInSceneBehaviour.Scene Name` disables its object only when the active scene has that exact name.
- `OpenURLInBrowser.URL` is the absolute target URL. `Delay Time` uses realtime seconds and `0` opens
  through the next immediate timer callback.
