# Bridges

`Bridges` contains optional integrations and reusable demo adapters. Bridge code can depend on MST
interfaces, but MST core should not depend on a specific bridge implementation.

## Main Areas

- `LiteDB` - local embedded database accessors for accounts and profiles.
- `MongoDB` - MongoDB accessors and document models.
- `SqlSugar` - SQL provider accessors for accounts, profiles and analytics.
- `Mirror` - Mirror room/lobby demo integration and networked character examples.
- `FishNet` - FishNet room/lobby demo integration and networked character examples.
- `Shared` - shared UI, matchmaking/profile helper behaviours, demo packets and simple utilities.

## Database Rules

- Provider factories derive from `DatabaseAccessorFactory`.
- Factories register implementations in `Mst.Server.DbAccessors`.
- Account providers implement `IAccountsDatabaseAccessor`, including server-owned account block
  storage through `IAccountBlockData`.
- Profile providers implement `IProfilesDatabaseAccessor`.
- Analytics providers implement `IAnalyticsDatabaseAccessor`.
- Bridge assemblies are opt-in and excluded from WebGL when provider DLLs are not browser-safe.

## Networking Framework Rules

- Do not modify Mirror/FishNet internals from this folder.
- Framework bridge code should adapt MST room access/auth/profile flows to that networking stack.
- Demo character movement/vitals are examples, not required MST runtime policy.

## Shared UI Rules

Shared UI is demo/reference UI. Do not put core server authority or platform-specific secrets here.
