# Groups Module

`GroupModule` is a generic MST social/group module. It is not tied to a specific game concept such as a clan.

Use it for any account-owned group gameplay feature: clans, settlements, squads, alliances, parties, or future community systems.

## Ownership

- Module, packets, opcodes, client/server facades, and persistence contracts belong to MST.
- Game projects should use `Mst.Client.Groups` and `Mst.Server.Groups`.
- Game-specific wording such as "clan" or "settlement" belongs in UI, localization, and gameplay rules above this module.

## Runtime Flow

- Authenticated clients can create a group, get their current group, invite a member, list pending invites, accept an invite, and leave a group.
- Trusted room/server peers can query a player's group through `Mst.Server.Groups.GetPlayerGroup(...)`.
- The module uses `IGroupsDatabaseAccessor` for persistence.
- `GroupModule.DatabaseAccessorFactory` creates the configured groups accessor before the module resolves it.
- The module creates persistent private MST Chat channels for groups and restores online group members into those channels after login.

## Persistence Setup

Assign a provider factory that creates only `IGroupsDatabaseAccessor` to `GroupModule`. The SqlSugar
bridge provides `GroupsDatabaseAccessorFactory`. Providers without a groups accessor cannot persist
groups until that provider implements the contract and its own factory.

## Current Limits

- The current storage contract treats an account as belonging to one group at a time.
- Owner transfer and group disband are intentionally left for a follow-up before owners can leave their own groups.
