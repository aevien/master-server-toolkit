# World Rooms Module

This module extends the room module for world-zone style room lookup.

## Main Types

- `WorldRoomsModule` derives from `RoomsModule`.

## Request Surface

The module keeps normal room behavior from `RoomsModule` and adds `MstOpCodes.GetZoneRoomInfo`
for zone-based room queries.

## Inspector Configuration

`Zone Scenes` contains the startup scene identifiers requested when a spawner registers. Every array
entry is submitted, so do not leave empty elements. Values must match the scene names understood by
room startup data; localized display names do not belong in this list.

## Rules

- Preserve all room registration/access rules from `RoomsModule`.
- Use stable zone identifiers, not localized display text, for lookups.
- Do not move game-specific world-map policy into MST core; keep this module as generic zone routing.
- When extending room filtering, validate visibility and access server-side.
- Call the base room run lifecycle when overriding start/stop. Stop inherited room state on the server
  owner thread before awaiting pending background zone scheduling, and do not let delayed zone work cross
  into a later server run.
