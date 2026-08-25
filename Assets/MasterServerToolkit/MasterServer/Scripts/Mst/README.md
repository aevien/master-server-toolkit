# Mst Facade And Core Primitives

This folder contains the global framework facade and cross-cutting primitives used by every
other MST subsystem.

## Key Files

- `Mst.cs` - static entry point. Creates and resets `Mst.Client`, `Mst.Server`, `Mst.Args`,
  `Mst.Events`, `Mst.Security`, `Mst.Thread`, `Mst.Create`, `Mst.Runtime` and the default socket.
- `MstArgs.cs` - command-line, environment and `application.cfg` parser. Supports `@import`
  directives, JSON args through `AsJson`, invariant numeric parsing and derived environment keys.
- `MstArgNames.cs` - canonical names for MST arguments. Add new reusable MST keys here.
- `MstProperties.cs` - string-based property bag used in packets, room/spawn/lobby options and
  lightweight account metadata.
- `MstErrorParser.cs` - shared client-side registry that converts structured non-success responses
  into localized callback messages. Module clients register their own stable error codes; errors
  with parameters can register a dedicated formatter.
- `MstErrorResponseExtensions.cs` - server helper that serializes a stable error code and optional
  parameters as `MstProperties`.
- `MstSecurity.cs` - password hashing, permission-key HMAC handshakes, authenticated data
  protection and client-to-master sealed requests.
- `Security` - versioned AES-256-GCM/RSA-OAEP-SHA256 envelopes, persistent key ring handling,
  legacy token compatibility and the WebGL Web Crypto bridge.
- `MstCreate.cs` - factory methods for sockets, messages and loggers.
- `MstThread.cs` - helper for main-thread/action execution.
- `MstLogController.cs` - logging initialization and file appender lifetime.

## Config Rules

Priority is: environment value lookup, command-line args, then config file args. Config files are
loaded from `application.cfg` by default or from `-mstConfigFile`. Main config values win over
imported defaults. Duplicate values inside imports are warned and the first value wins. Missing
imports warn and continue.

When adding a key:

1. Add it to `MstArgNames` with XML docs and an example.
2. Parse it in `MstArgs` only if it is a common framework option.
3. Use `Mst.Args.Names.<Key>` instead of string literals.
4. Redact secret-bearing arguments from diagnostics and document which process owns each value.

Permission credentials use one JSON argument:
`-mstPermissionCredentials={"default":"client-secret","room_server":"room-secret"}`. The master
uses it only to override secrets for permission keys already declared in its Inspector. Connecting
processes use the entries needed by their modules. All connections request `default`; room and
spawner modules request their additional exact permission before protected registration. Argument
diagnostics must use the built-in redaction helpers.

`ConnectionHelper` can provide one connection-scoped permission key and credential through
`IConnectionPermissionCredentials`. Resolution priority is: a non-empty matching connection-scoped
entry, a matching runtime `-mstPermissionCredentials` entry, then the built-in credential. This does
not mutate `MstArgs` and does not combine JSON maps. When the connection entry does not match, a
malformed runtime credential map still fails closed.

`MstSecurity` loads or creates the persistent file configured by `-mstSecurityKeyRingFile`. It uses
AES-256-GCM for authenticated data protection, HKDF-SHA256 purpose separation and a 2048-bit
RSA-OAEP-SHA256 seal key for one-request client envelopes. Keep previous key-ring entries when
rotating the active key so existing protected data remains readable. Native and Editor players use
BouncyCastle; WebGL uses the browser Web Crypto API.

New auth tokens use the versioned `v2` authenticated envelope. Legacy CBC+HMAC tokens remain
readable and are replaced after a successful token sign-in. New passwords use versioned
PBKDF2-HMAC-SHA256 hashes; legacy PBKDF2-SHA1 hashes are validated only for compatibility and
upgraded by `AuthModule` after a successful login. Production legacy token secrets are validated by
`AuthModule` and must remain configured while old tokens can still exist.

## Structured Errors

Socket handlers send successful payloads in their normal packet format. Every non-success response
uses a different, uniform payload: serialized `MstProperties` containing a required `CODE` and only
the safe parameters needed to format the message. Server and room processes keep technical details
in English MST logs; exception messages, stack traces and database details are never returned to a
client.

`Mst.Errors` owns the single runtime `MstErrorParser` used by client, server and security facades.
Modules access this registry directly and register their codes during construction; the parser is not
duplicated or passed through module constructors. Simple codes use the conventional
localization key `ui.error.<serialized-code>.message`. Parameterized errors, such as an account block
with a reason and expiry date, register a formatter. Existing callbacks continue to receive a string,
but that string is produced locally in the currently selected language. Unknown codes and missing or
damaged payloads fall back to the localized `ResponseStatus` message.

## Lifecycle Risks

`Mst` is static and survives disabled Domain Reload unless explicitly reset. Any new static state
must either be reset through subsystem registration or be safe across repeated play sessions.
`Mst.Errors` is recreated before module construction on every MST initialization and remains available
until the active connection has been closed during reset.
Do not add static event subscriptions without an explicit cleanup path.
