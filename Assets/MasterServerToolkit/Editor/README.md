# Editor

This folder contains Unity Editor-only helpers for MST.

## Main Areas

- `Build` - generic project build helper and demo bridge builders.
- `Drawers` - inspector drawers for MST attributes/types.
- `Windows` - editor windows and package welcome tooling.
- `Addons` - optional editor integration helpers.
- `Demos` and `Bridges` - editor menu/build helpers for demo scenes.
- `MstConfigScriptedImporter.cs` - imports `.cfg` assets under `Assets` as `TextAsset` instances.

## Rules

- Editor scripts may use `UnityEditor` and raw `Debug.Log`.
- Do not move runtime code into this folder.
- Build helpers may write `application.cfg` for demos, but production project build scripts can live
  outside MST when they are game-specific.
- Demo master builders persist a generated token secret next to their room/spawner secret files and
  reuse it across rebuilds, so standalone examples pass production startup preflight without embedding
  the shared MST placeholder secret.
- Do not run builds automatically from agent tasks unless explicitly requested.
