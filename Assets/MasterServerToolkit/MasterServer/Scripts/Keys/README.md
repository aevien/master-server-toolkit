# Keys

This folder stores shared constants used across MST modules.

## Files

- `MstOpCodes` - network message opcodes. These are wire contract ids.
- `MstParamKeys` - common parameter and query-string keys.
- `MstEventKeys` - names for local `Mst.Events` UI/framework events.
- `MstPeerPropertyCodes` - peer property ids.
- `MstErrorCodes` - stable machine-readable codes for non-success socket responses.
- `MstErrorPropertyKeys` - names of structured error parameters stored in `MstProperties`.
- `MstLocales` - localized response strings.
- `MstSearchCondition` and `MstSortDirection` - common query helpers for admin/data APIs.

## Rules

- Add core message ids to `MstOpCodes`; do not create hidden module-local opcode sets.
- Do not renumber existing opcodes without an explicit protocol migration.
- Argument/config keys belong in `MstArgNames`, not here.
- Every non-success socket response must contain `MstProperties` with
  `MstErrorPropertyKeys.CODE`. Do not send user-facing text or exception messages over the wire.
- Error-code constants use `UPPER_SNAKE_CASE`; their serialized values are stable dotted identifiers.
