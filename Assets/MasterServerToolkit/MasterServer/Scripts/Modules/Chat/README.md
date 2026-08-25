# Chat Module

This module provides generic chat channels, user names, messages, invites, bans and permissions.

## Main Types

- `ChatModule` is the authoritative server module.
- `ChatClient` is the client facade used by game/UI code.
- `ChatServer` is the server-side network facade used by trusted server processes through `Mst.Server.Chat`.
- `ChatChannel`, `ChatChannelOptions` and `ChatChannelInfo` model server channel state.
- `ChatUserPeerExtension` stores per-peer chat state.
- `Packets` contains wire payloads for channels, users and messages.

## Request Surface

`ChatModule` registers handlers for username selection, joining/leaving channels, channel lists,
messages, user lists, default channel changes, channel creation, invites, kicks, bans and permissions.
Server-side commands use separate opcodes and go through `Mst.Server.Chat`; caller code must not mutate
`ChatModule` collections or `ChatChannel` state directly.

Trusted server processes can send chat on behalf of an existing chat user through `Mst.Server.Chat`.
Use `SendMessageToUsers(senderUsername, recipientUsernames, text, callback)` for delivery to a bounded
user list, `SendPrivateMessage(senderUsername, receiverUsername, text, callback)` for direct messages, and
`SendChannelMessage(senderUsername, channelName, text, callback)` for a full channel broadcast. The master
still validates that the sender exists, can send to the channel, and that online recipients exist.

Trusted overloads can also carry `SenderDisplayName`, `ReceiverDisplayName`, `SenderAvatar`, and
`ReceiverAvatar` snapshots for client presentation. These values never participate in routing,
permissions, moderation identity, or user lookup: `Sender` and `Receiver` remain canonical chat
usernames. The module trims display snapshots and limits each one to 64 characters. Avatar snapshots
must be absolute HTTPS URLs no longer than 512 characters; malformed, non-HTTPS, and overlong values
are normalized to an empty string. Display names and avatars submitted through the ordinary client
message API are always cleared so a client cannot spoof another user's visible identity.

Server-side chat opcodes require the exact `MstPermissionKeys.RoomServer` permission. Room
registration automatically obtains that permission through the one-time HMAC challenge, so the same
connection can use `Mst.Server.Chat` afterward. A normal client receives only `default` and cannot
call trusted chat APIs. An authenticated account administrator remains an explicit server-side
override.

## Persistence

Chat persistence is optional. `ChatModule` first invokes its optional `DatabaseAccessorFactory`, then
resolves `IChatDatabaseAccessor` from `Mst.Server.DbAccessors` and saves accepted `Private`, `Channel`,
and trusted `Users` messages when a registered accessor exists.
SqlSugar, LiteDB, and MongoDB bridges store messages in the `chat_messages` table/collection. The module
must continue delivering messages even if no chat database accessor is registered.

Each database bridge provides a dedicated chat factory: `ChatLiteDbAccessorFactory` for LiteDB and
`ChatDatabaseAccessorFactory` for MongoDB or SqlSugar. Assign the matching factory to `ChatModule`.
LiteDB uses a dedicated `chat.db` file and does not store chat history in `accounts.db`.

Accepted messages are persisted as raw submitted text for moderation and audit. When `CensorModule` is
enabled, `ChatModule` creates a censored outgoing copy for delivery to clients and does not mutate the
raw packet used for persistence.

Display-name and avatar snapshots are currently transient packet presentation data. Persistent chat
history remains keyed by canonical sender/receiver usernames and stores the raw message text; historical
presentation data must not be used as stable account identity.

## Inspector Configuration

- `Database Accessor Factory` is optional. Without it, accepted chat is delivered but not persisted.
- `Use Auth Module` creates and removes chat identities with authenticated sessions. Disable it only
  when project code owns chat identity lifecycle.
- `Use Censor Module` checks channel names and outgoing message copies. Raw persisted text is retained.
- First/last local-channel settings control automatic default channel selection as membership changes.
- `Allow Username Picking` exposes client-selected chat names. Channel name length limits apply to
  client-created channels and should form a valid minimum/maximum pair.

## Rules

- Validate user identity and channel permissions on the server before mutating channel state.
- Keep moderation/censorship policy generic; use `Censor` integration or extension points for content checks.
- Keep raw chat storage separate from censored client delivery.
- Do not assume chat display name is the same as account username unless the calling flow explicitly maps it.
- Resolve avatar snapshots in an authoritative trusted server process. Never accept a client-provided
  avatar URL as identity or authorization data.
- Clean up peer channel membership when a peer disconnects.
- Server processes that need chat channels should send server-side chat requests through `Mst.Server.Chat`.
- Server-side directed messages are for trusted server peers only. Do not expose them as a public client command.
- `GetUsersSnapshot()`, `GetInvitedUsersSnapshot()`, and `GetBannedUsersSnapshot()` return detached lists under the channel lock. Later channel changes are not reflected in an existing snapshot.
