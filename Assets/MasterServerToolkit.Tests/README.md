# Master Server Toolkit Tests

## Running The EditMode Suite

1. Open `Window > General > Test Runner` in Unity.
2. Select the `EditMode` tab.
3. Run the `MasterServerToolkit.Tests.EditMode` assembly.

The reporter writes the latest result to:

- `Logs/MstTests/latest.txt`
- `Logs/MstTests/latest.xml`

Timestamped copies are kept in the same directory. A test-run startup failure is written to `latest.txt` and a timestamped `*-error.txt` file.

The framework suite targets Unity `2022.3.62f3`. Run the complete assembly after importing
or updating MST because the standalone repository intentionally excludes game-specific tests.

## Current Coverage

- Authentication request completion, duplicate sign-in protection, malformed responses, send failures, and successful account loading.
- Server access challenge validation, access proof creation, permission clamping, permission reset on
  disconnect, authenticated sealed requests, replay rejection, persistent key-ring compatibility and
  concurrent first-run key-ring creation.
- Strict JSON validation and decoding of standard escaped slash, backslash and Unicode sequences.
- Room-server account lookup by username and peer ID, including disconnected, failed, malformed, and successful responses.
- Client and game-server profile loading, error handling, profile update registration, and callback completion.
- Achievement client/room routing, exact `room_server` authority, authoritative locked-to-unlocked
  transitions, replay-safe and retryable rewards, logout grace-period updates, worker-to-main-thread
  dispatch, disconnect forwarding, and single-source unlock notifications.
- Master profile restore ordering and client/server profile fetch responses for missing users, logout, timeout, permission denial, and success.
- Initial profile single-flight behavior, ready-only publication, concurrent login ownership, logout during restore, retry cleanup, stale auth-session logout, pending-save ownership, failed-save retry, and save-confirmed unload/reconnect behavior.
- Strict and atomic `ObservableProfile.FromBytes` validation, including rollback after a property decoder fails.
- MongoDB binary/document profile save snapshot validation and detachment from later profile mutations,
  without requiring a live MongoDB instance.
- LiteDB accounts/chat file isolation, independent disposal, and chat history reopening.
- Persisted email-confirmation and password-reset code expiration, attempt exhaustion, concurrent
  validation, and account-token revision invalidation after a password change.
- Chat client/trusted-server packet round trips, including canonical routing usernames, transient sender
  and receiver display-name snapshots, message type, message text, and selected recipients.
- Server module initialization failure isolation, dependency blocking, optional fallback, and failed-module retry prevention.
- MST facade reinitialization after Unity returns from Play Mode to Edit Mode.
- Timer subscriber exception isolation and duplicate event subscription removal semantics.
- Concurrent logger identity, pre-initialization pooling, lifecycle mutation and reentrant appender behavior.
- Update-runner priority ordering, phase boundaries, deferred mutations, reference identity and
  non-creating cleanup/read behavior.
- WebSocket client fatal-update cleanup, including close notification, pending connection waits,
  acknowledgement completion and reentrant runner registration.
- Concurrent room, lobby, spawner, spawn-task and process-port allocation without duplicate values, including
  synchronized reuse of released room and redirect ports.
- Room capacity reservation/cancellation, module-owned direct destruction, late access ACK destruction
  barriers, delayed leave preservation of a newer room assignment, destroyed-lobby finalization rejection,
  stale lobby-task callback rejection and owner-thread world-room base cleanup before background drain.
- Spawner capacity races, abort-before-dispatch, ambiguous spawn-ACK cleanup, close-before-publication,
  retryable kill failure, kill-confirmation watchdog, background-worker policy, controller-registry cleanup,
  custom-connection handler ownership, default/custom connection ID ambiguity, full-shutdown supervision
  abandonment without false `Killed`, watchdog termination after abandonment, duplicate process notifications,
  terminal callback exactly-once behavior and `Finalized -> Killed` process lifecycle.
- Server-run module cancellation, reverse sequential shutdown, partial-start rollback and preservation of
  `handler -> module -> transport` ordering after the bounded synchronous timeout, startup admission and
  active-run module mutation rejection.
- Analytics shutdown ownership, including an insert/stop interleaving, failed-batch retry, inactive-run
  rejection and final queued-batch flush.

Most client-module EditMode tests use an in-memory `IClientSocket` implementation. The WebSocket
lifecycle tests exercise `WsClientSocket` and `WsClientPeer` without opening a real network connection.
They do not prove real WebSocket transport, ACK timeout scheduling, master database integration,
Mirror room authentication, or scene lifecycle behavior. Those flows require focused
PlayMode/integration tests against the current production room implementation.
