# Networking Scripts

This folder contains the transport-independent networking core used by MST modules.

## Main Areas

- `IPeer`, `BasePeer` - connected peer abstraction, peer properties, extensions and response callbacks.
- `IClientSocket`, `IServerSocket`, `BaseClientSocket` - socket lifecycle contracts.
- `IncomingMessage`, `OutgoingMessage` - opcode, status, flags, ack id and binary payload handling.
- `MessageFactory`, `Serializer`, `SerializablePacket` - packet creation and binary serialization.
- `PacketHandler`, `AsyncPacketHandler` - message handler wrappers.
- `MstTimer`, `MstUpdateRunner` - timeout/update helpers used by response callbacks.
- `Transports/WebSocketsSharp` - WebSocketSharp runtime transport implementation.
- `Utils` - endian readers/writers and conversion helpers.

## Rules

- Keep module authority out of the transport layer.
- Do not change ack/response semantics without auditing every `SendMessage(..., callback)` caller.
- Packet classes must write/read fields in the same order.
- Validate every network-provided length or item count before allocation or iteration. Use
  `MstNetworkLimits` and the bounded `EndianBinaryReader` helpers instead of direct dynamic
  `ReadBytes` calls.
- Pair every bounded `ReadCount32` contract with `WriteCount32` using the same limit.
- Keep transport receive queues bounded by both message count and total bytes. Preserve already
  queued frames until the disconnect drain completes, then release the transport.
- Socket implementations must cleanly close, unregister handlers and release timers/callbacks.
- Client transports that support connection-scoped MST permission credentials implement
  `IConnectionPermissionCredentials`; WebSocketSharp preserves the configured pair across reconnects.
- Keep transport-specific code under `Transports` or `Plugins`.
