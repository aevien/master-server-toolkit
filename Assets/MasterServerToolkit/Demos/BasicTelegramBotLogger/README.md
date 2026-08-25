# Basic Telegram Bot Logger Demo

This demo validates a Telegram bot and forwards Unity errors and exceptions to configured Telegram
chat IDs.

It is an integration example only. It stores credentials in the Unity scene and does not implement
production log delivery controls.

## Setup

1. Create a Telegram bot through BotFather and obtain its Bot API token.
2. Start a conversation with the bot from every target account or add the bot to the target group.
3. Obtain the numeric chat IDs that should receive messages.
4. Open `Scenes/MasterServer/MasterServer.unity`.
5. Select the object with `TelegramBotLogger`.
6. Enter the token in `Bot Api Token`.
7. Add each target ID to `Chat Ids`.
8. Run the scene. The component first calls Telegram `getMe`; log forwarding starts only after that
   request succeeds.

## Inspector Settings

### Bot Api Token

Required Telegram Bot API token used in `getMe` and `sendMessage` requests. The value is serialized
in the scene. Do not use this storage pattern when distributing a client build or sharing a scene
with untrusted users.

### Chat Ids

List of Telegram chat IDs that receive Unity errors and exceptions. An empty array allows bot
validation but sends no log messages.

## Validation

1. Run the master demo scene with a valid token and at least one reachable chat ID.
2. Confirm that no bot validation error appears in the MST log.
3. Press `E`. `TelegramBotDemo` intentionally throws a `NullReferenceException`.
4. Confirm that the exception condition and stack trace arrive in each configured chat.
5. Repeat with an invalid token and confirm that the failure is logged without enabling forwarding.

## Limitations

- Only Unity `Error` and `Exception` log types are forwarded.
- There is no batching, rate limiting, retry queue, persistence, or message-size handling.
- Every configured chat receives every forwarded error.
- The Bot API token is stored as scene data.
- Telegram availability and platform networking restrictions can prevent delivery.
- The intentionally thrown exception is part of the demo validation path and must not be copied
  into production code.
