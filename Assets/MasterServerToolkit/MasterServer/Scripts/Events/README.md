# Events

This folder contains MST's local in-process event channels.

## Main Types

- `MstEventsChannel` is the current event bus exposed through `Mst.Events`.
- `EventPayload` wraps optional event data and provides typed conversion helpers.
- `OldEventsChannel` is legacy support. Avoid using it for new code.

## Contracts

- `AddListener(...)` and `AddListenerOnce(...)` return an `IDisposable` subscription token.
- Store that token for every long-lived listener and dispose it during owner teardown.
- Listener registration is not a substitute for authority validation; it is only local process messaging.
- `MstEventsChannel` can enforce main-thread access. Do not call it from arbitrary background code
  unless the caller explicitly posts/marshals to the expected thread.

## Duplicate Listener Rule

Adding the same handler to the same event creates another subscription. Avoid duplicate registration
by owning the token and making the lifecycle explicit.
