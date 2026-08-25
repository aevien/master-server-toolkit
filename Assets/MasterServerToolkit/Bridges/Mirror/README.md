# Mirror Bridge

This bridge contains Mirror-based demo integration for MST rooms and lobbies.

## Scope

- Room/client/master demo scenes and prefabs.
- Mirror room authentication and network managers.
- Demo network messages for room access validation.
- Demo character movement/profile/vitals scripts.

## Rules

- Do not modify Mirror framework internals from here.
- Treat gameplay character scripts as demos, not MST core policy.
- MST room access validation should stay server-authoritative.
- Keep Mirror-specific code in this bridge; do not leak Mirror types into MST core modules.
