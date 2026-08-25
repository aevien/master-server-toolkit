# MstJson

`MstJson` is MST's mutable JSON value, parser, validator, and serializer. Runtime MST code must use
this API instead of `UnityEngine.JsonUtility`.

## Creating Values

- `MstJson.CreateObject()` creates a new mutable JSON object.
- `MstJson.CreateArray()` creates a new mutable JSON array.
- `MstJson.CreateNull()` creates a new JSON null value.
- `MstJson.Create(value)` creates a JSON value from a supported primitive, timestamp, collection, or
  callback.

Every factory call returns a separate instance. Store the returned object when it is reused; do not
assume the factories expose shared immutable constants.

## Dates

`MstJson.Create(DateTime)` serializes with the invariant ISO 8601 round-trip (`O`) format.
`GetDateTimeValue()` parses that exact format and preserves `DateTime.Kind`. Parsing is explicit
because it can fail with `FormatException` when external JSON contains an incompatible timestamp.

## Validation And Parsing

Use `MstJson.IsJson` when an input must be a complete JSON object or array before parsing it. Creating
`new MstJson(text)` parses a value but does not replace policy validation for configuration files or
network input. Keep maximum-size checks at the owning transport/module boundary.

## Designer And Developer Checks

1. Use object factories for key/value payloads and array factories for ordered payloads.
2. Verify required fields and value types before reading external data.
3. Keep timestamps in round-trip format and UTC where the owning domain requires UTC.
4. Run `MstJsonValidationTests` after changing parsing, factories, escaping, or timestamp handling.
