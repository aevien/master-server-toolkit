# Master Server Toolkit

Master Server Toolkit 5 is a reusable Unity framework for multiplayer backend flows:
master servers, authenticated clients, rooms, spawners, profiles, lobbies, matchmaker
queries, HTTP/admin tooling, database accessors, platform service bridges, and shared UI/tools.

This folder is the package boundary. Keep it generic: MST must not depend on any
specific game's gameplay data, economy rules, scenes, or project-only types.

## Main Areas

- `MasterServer` - high-level MST facade, server/client modules, auth, profiles, rooms,
  spawners, lobbies, web/admin modules, logging, database interfaces, opcodes and config args.
- `Networking` - transport-independent packet/message model, peers, sockets, dispatchers,
  timers, serializers, WebSocketSharp and WebGL socket adapters.
- `GameServiceBridge` - platform service abstraction for Editor, Desktop, Web, Itch,
  VK Play, Yandex Games and feature modules such as player, ads, storage, purchases,
  leaderboards, analytics and share.
- `Bridges` - optional integrations for database providers, Mirror/FishNet room examples,
  reusable demo UI and bridge-level helpers.
- `Tools` - shared utility layer: JSON, UI views, singleton helpers, object databases,
  terminal, debounce/throttle dispatchers and editor-window helpers.
- `Editor` - Unity Editor-only build helpers, property drawers, demo builders and package tools.
- `Demos` - sample scenes and scripts. Do not treat demo code as framework policy.
- `docs` - public guides that cover complete framework workflows. Module ownership,
  lifecycle rules and configuration details also live in README files beside the code.

## Assemblies

- `MasterServerToolkit.asmdef` is the main runtime assembly. It currently references
  Unity TextMeshPro, Mirror, FishNet, SimpleWebTransport and Mirror.Transports.
- `Editor/MasterServerToolkit.Editor.asmdef` is Editor-only and references the runtime assembly.
- `Bridges/LiteDB`, `Bridges/MongoDB`, and `Bridges/SqlSugar` each have opt-in bridge assemblies.
  These bridge assemblies are not auto-referenced and are excluded from WebGL.
- VK Play can optionally use Steamworks.NET when the package, assembly reference and
  `VKPLAY_STEAMWORKS` scripting define are added by the integrating project.

## Agent Contracts

Start with `AGENTS.md`, then read the nearest README in the folder being changed.
These files are meant for future maintainers and coding agents. They describe ownership,
extension points, lifecycle rules and sharp edges discovered from the current code.

## Editing Rules

- Prefer existing MST abstractions before adding new ones.
- Use `MstArgNames`, `Mst.Args`, `MstOpCodes`, `MstProperties`, `Mst.Create.Logger`
  and existing module patterns.
- Use MST logging for runtime/server output.
- Preserve wire contracts unless a migration is explicitly accepted.
- Account for disabled Domain Reload and disabled Scene Reload in Unity Enter Play Mode.
- Keep game policy outside MST core. Put service-specific behavior in service bridges.
