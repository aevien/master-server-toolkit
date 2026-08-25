# Networking

This folder owns MST packet transport abstractions. Higher-level modules should depend on these
interfaces instead of a concrete transport.

## Key Files

- `IPeer`, `BasePeer` - peer abstraction, ack callbacks, response timeouts, peer extensions,
  per-peer properties and send overloads.
- `IClientSocket`, `IServerSocket`, `BaseClientSocket` - socket contracts.
- `IIncomingMessage`, `IncomingMessage`, `IOutgoingMessage`, `OutgoingMessage` - message model.
- `MessageHelper`, `MessageFactory`, `Serializer`, `SerializationExtensions`, `SerializablePacket`
  - packet creation and serialization helpers.
- `PacketHandler`, `AsyncPacketHandler`, dispatcher interfaces - message handler contracts.
- `MstTimer`, `MstUpdateRunner`, `IUpdatable` - frame/tick update infrastructure.
- `Transports/WebSocketsSharp` - WebSocket client/server sockets and peers.
- `Plugins/WebSocketSharp/WebSocket.cs` - Unity/WebGL/native WebSocket adapter.

## Wire Format

`OutgoingMessage.ToBytes()` writes flags, opcode, data length, data, optional ack request id and
optional ack response id/status. Do not change this format without a versioned migration.

`MstNetworkLimits` bounds untrusted data before allocation or collection loops. The MST binary
payload limit is 16 MiB; the WebSocket wire-message limit additionally includes the 16-byte maximum
MST envelope. Each client receive queue is limited to 1,024 messages and two maximum wire messages
of total buffered data; overflow closes the connection with WebSocket code `1009`. WebGL applies
these limits before accepting the browser payload into the Unity-facing queue. Text payloads are
limited to 1 MiB on both send and receive. Packet readers must use the bounded `ReadLength32`,
`ReadCount32` and `ReadBytesExact` helpers for dynamic lengths. Writers paired with bounded count
readers must use `WriteCount32` with the same limit.
Authentication encryption uses separate plaintext and ciphertext limits. Versioned sealed packets
bound every dynamic key, purpose and ciphertext field before allocation. Credential envelopes use
AES-256-GCM with authenticated associated data and RSA-OAEP-SHA256 key wrapping; a server challenge
can be consumed only once.

## Handler Rules

- If a message expects a response and no handler exists, respond with an error.
- Use exact handler unregistering when possible.
- `BasePeer` owns acknowledgement callbacks until response, timeout, failed send, disconnect or disposal.
  Every terminal path removes the pending request before invoking its callback, and callbacks run outside
  the pending-request lock.
- Exceptions from acknowledgement callbacks are logged and isolated from transport/update processing.
- Disconnect and disposal complete remaining acknowledgement callbacks once with `NotConnected`; timeout
  and disconnect callbacks receive a non-null terminal response object.
- `BasePeer` subscribes to `MstTimer.OnTickEvent` for acknowledgement timeout disposal and must be disposed.
- Peer extensions attach runtime identity/state such as security, user, profile and lobby data.

## Transport Rules

- WebGL cannot host a WebSocket server. `WsServerSocket` logs unsupported in WebGL builds.
- `WsClientSocket` is update-driven and must be registered with `MstUpdateRunner` while connected.
- `MstUpdateRunner` executes lower numeric priorities first within each update group. Equal priorities
  keep registration order. Every-frame items remain a separate phase and execute before due interval items.
- Add, remove and parameter changes requested from `DoUpdate` are finalized after the active pass.
  Removing an item before its turn prevents it from running during that pass.
- Runner registrations use object identity, not overridden value equality. Only `Add` creates the
  singleton; cleanup, lookup, parameter-update and statistics calls do nothing when it does not exist.
- Secure mode requires certificate setup on server sockets.
- The native WebSocketSharp transport uses `websocket-sharp v1.3.1`. Sequential
  `SendAsync` calls are FIFO, so client and server peers send immediately without
  fixed post-open delays. Client authentication starts before the first queued
  incoming frame is dispatched on Unity update.
- WebSocket send completion is propagated to `BasePeer`. Client completions are dispatched from
  `WsClientSocket.DoUpdate`, preserving the existing Unity-thread callback context.
- Passive client disconnect drains frames already queued by the transport before peer disposal, allowing
  a final response to complete its acknowledgement before the remaining requests become `NotConnected`.
- Native and WebGL adapters release their bounded receive queues and transport references on disposal.
- The optional `/echo` service uses the same frame and aggregate message limits as the MST endpoint.
- An unexpected `WsClientSocket` update failure closes the transport with private code `4000`, disposes
  the peer, completes pending work and notifies close listeners without rethrowing into the update runner.
- Re-adding an update object during the same `MstUpdateRunner` pass cancels its deferred removal. This keeps
  a reentrant client reconnect registered for subsequent updates.
