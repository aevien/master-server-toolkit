# Matchmaker Module

This module exposes generic game search and region listing over registered game providers.

## Main Types

- `MatchmakerModule` is the authoritative server module.
- `MatchmakerClient` is the client facade for finding games and regions.
- `IGamesProvider` is the provider contract used to collect visible games.
- `GameInfoPacket`, `PlayersPacket`, `RegionInfo` and `RegionsPacket` are wire payloads.

## Request Surface

The module handles `MstOpCodes.FindGamesRequest` and `MstOpCodes.GetRegionsRequest`.

## Inspector Configuration

`Use Spawner Module` makes `SpawnersModule` the source for region-list requests. Disable it when the
installation has game providers but no process spawner; game search continues, while region requests
return an error.

## Rules

- Providers must return only rooms/games visible to the requesting user.
- Keep filtering deterministic and bounded; avoid unbounded scans on hot paths.
- Region labels may be localized in config, but wire ids should remain stable.
- Do not hard-code project-specific map/mode policy into MST matchmaker core.
