# MasterServer Scripts

This folder is the main runtime layer of MST. It contains the facade, server/client bases,
module implementations, logging, events, database contracts, keys and shared runtime helpers.

## Folder Roles

- `Mst` - global facade, argument parsing, factory helpers, localization, security helpers.
- `Server` - master server lifecycle, module base class, peer extensions and server behaviour.
- `Client` - client connection helpers and base client behaviours.
- `Modules` - built-in authoritative server modules and their request facades.
- `Database` - generic accessor registry and provider-independent database contracts.
- `Mail` - pluggable mailer base and SMTP implementation used by authentication email flows.
- `Logger` - MST logging abstraction, appenders, global log manager and channel filtering.
- `Events` - in-process event bus used by UI and framework components.
- `Keys` - shared string/int constants for opcodes, properties, events and query params.

## Rules

- Keep this layer independent from a specific game.
- Prefer existing facade APIs (`Mst`, `Mst.Args`, `Mst.Client`, `Mst.Server`) before exposing new globals.
- Do not put provider-specific database code here; add it under `Bridges/<Provider>`.
- Do not put platform SDK logic here; add it under `GameServiceBridge`.
- Preserve serialization order and opcode values unless a breaking migration is accepted.

## SMTP Inspector

`SmtpMailer` configures the SMTP host, TCP port, credentials, SSL/TLS mode, send timeout in seconds,
From address and sender display name. Matching `-mstSmtp...` arguments override those Inspector values
at startup. The port and SSL mode must match the provider. The optional email body template wraps
caller-supplied content; leave it unassigned to send the supplied body directly.
