# Yandex Games Service

This service integrates Yandex Games WebGL SDK features.

## Main Types

- `YandexGamesService` initializes Yandex Games modules.
- `YandexGamesPlayerModule` obtains player identity and authorization state.
- `YandexGamesPlayerIdentityValidator` and `YandexGamesSignatureVerifier` validate signed player data.
- Yandex modules cover ads, purchases, storage, leaderboards, analytics and share where supported.
- `YandexGamesKeys` stores service-specific keys.
- `YandexGamesService.Referrer` reads `ysdk.environment.referrer` and exposes it as the generic `ReferrerInfo` contract.

## Rules

- WebGL SDK calls must stay behind service modules and `.jslib` bindings.
- Server-side identity validation must use signed Yandex data, not client-trusted fields alone.
- Respect unsupported/unauthorized states through `IsSupported`, `IsReady` and authentication flags.
- Do not block regular MST guest/login flows when Yandex auth is unavailable or denied.
- The player remains a guest until `player.isAuthorized()` reports an authorized Yandex account.
  A successful authorization dialog is followed by a fresh `getPlayer({ signed: true })` request;
  the authentication callback succeeds only when the refreshed player is not a guest and has an ID.
- Invalid JavaScript callback JSON falls back to guest player state and completes any pending
  authentication callback with an error instead of leaving the caller waiting.
- Use `ReferrerInfo` for client launch-source handling; do not treat referrer values as signed identity data.
- Initialization waits for a valid `ysdk.environment` object before reporting the service as ready.
  Loading is retried until the configured service timeout; an early invalid or empty response is not cached.
- `AppId`, `Lang`, `Payload`, and `Referrer` are populated once during initialization. Reading these
  properties does not call JavaScript or parse JSON, so UI access order cannot affect service state.

## Leaderboard Mirror

MST remains authoritative. After the room receives a successful MST leaderboard response, the owning
client mirrors the returned canonical score through `YandexGamesLeaderboardsModule.SetScore`.
The JavaScript binding checks `leaderboards.setScore` availability, awaits the SDK promise and returns
success or the SDK error to Unity using a request identifier. The caller serializes updates, so the
Yandex limit of one score request per second is not violated by overlapping writes.

Yandex accepts only authorized platform players, non-negative values and JavaScript safe integers.
Failure to mirror is diagnostic only and never changes the MST entry. The technical leaderboard name
and score presentation must be configured in the Yandex developer console to match the MST key and
meaning.
