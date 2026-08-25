# Notification Module

This module delivers lightweight server-to-client notifications.

## Main Types

- `NotificationModule` is the authoritative server module.
- `NotificationClient` receives notification messages.
- `NotificationServer` is the server-side request facade.
- `NotificationRecipient` describes a target.
- `NotificationPacket` is the wire payload.

## Request Surface

The module handles subscription, unsubscription and notification send/forward requests through
`MstOpCodes.SubscribeToNotifications`, `MstOpCodes.UnsubscribeFromNotifications` and
`MstOpCodes.Notification`.

## Inspector Configuration

- `Use Auth Module` automatically registers authenticated sessions as notification recipients.
- `Use Rooms Module` enables room-aware routing and requires a `RoomsModule` on the same server.
- `Max Promised Messages` bounds broadcasts retained for later logins. `0` disables retention;
  exceeding the limit removes the oldest retained message.

## Rules

- Validate sender authority before allowing server/room-originated notifications.
- Remove recipients when peers disconnect.
- Do not use notifications for durable data. Store durable state in the owning module/database.
- Keep payloads bounded and serializable.
