# Leaderboards Module

The Leaderboards module provides authoritative, cross-platform score storage on the MST master
server. It stores every participating account, returns sorted pages and player ranks, and can accept
scores either from trusted room servers or directly from authenticated clients when a definition
explicitly permits that flow.

Platform leaderboards are not authoritative. A game may mirror the confirmed MST score to Yandex,
VK Play or another service after `SubmitScore` succeeds, but a platform failure must not roll back the
MST entry.

## Main Types

- `LeaderboardsModule` is the authoritative master-server component.
- `LeaderboardsModuleClient` is exposed as `Mst.Client.Leaderboards`.
- `LeaderboardsModuleServer` is exposed as `Mst.Server.Leaderboards` for trusted room processes.
- `LeaderboardDefinition` describes one leaderboard season in the Inspector.
- `LeaderboardEntry` is the persisted and transferable score record, including the current public
  player name and optional HTTPS avatar snapshot.
- `ILeaderboardsDatabaseAccessor` owns atomic score comparison and persistence.

All module code, models and packets live in this folder. Provider-specific persistence remains under
the matching `Bridges/SqlSugar`, `Bridges/MongoDB` or `Bridges/LiteDB` folder.

## Setup

1. Add `LeaderboardsModule` to the master server module hierarchy.
2. Assign one leaderboards database accessor factory.
3. Add one or more definitions to `Leaderboards`.
4. Keep every `Key + Season Id` pair unique.
5. When several seasons use the same key, keep their time intervals non-overlapping.
6. Give room processes the normal `room_server` permission before calling the trusted API.

The module currently uses Inspector definitions only. External configuration overrides are deliberately
deferred; no `-mstLeaderboards` argument is read by this implementation.

The project `Master.unity` scene includes a SqlSugar-backed `LeaderboardsModule` with the all-time
`survivalTime`, `distanceTraveled` and `zombiesKilled` definitions. They are descending, keep the best
score, accept guests, require trusted room submission and accept non-negative `long` values. Distance
uses two fixed-point decimal places; the other definitions use whole units. Titles are configured for
`en`, `ru` and `tr`.

## Module Inspector

- `Database Accessor Factory` creates and registers the provider-specific
  `ILeaderboardsDatabaseAccessor`. Without it the module still returns configured definitions, but
  returns `leaderboards.database_unavailable` for entry reads and score submissions.
- `Leaderboards` contains the authoritative definitions and seasons.
- `Default Page Size` is used when a request supplies `0` or a negative limit. Default: `20`.
- `Maximum Page Size` caps one response to protect database and network work. Default: `100`.

## Leaderboard Definition

- `Key` is the stable API and persistence identifier, for example `survivalTime`. Maximum length:
  `64` characters.
- `Title` contains language/value pairs. Language matching is case-insensitive. Resolution order is
  requested language, `en`, first configured non-empty value, then `Key`.
- `Season Id` separates stored seasons. Maximum length: `64` characters. Empty means `all_time`.
  Changing it starts an empty season and does not delete old entries.
- `Sort Order` is `Descending` when larger scores are better and `Ascending` when smaller scores are
  better.
- `Keep Best` keeps the existing score unless the submitted value is better according to Sort Order.
  When disabled, each accepted submission replaces the previous score.
- `Decimal Places` describes fixed-point display only. MST always stores a signed `long`. For example,
  `1234` with `2` decimal places is displayed by the game as `12.34`.
- `Minimum Score` and `Maximum Score` define the inclusive accepted range.
- `Server Only` requires the exact `room_server` permission for submissions. When disabled, an
  authenticated client may submit only its own score.
- `Allow Guests` controls whether accounts with `IsGuest == true` may submit. It does not hide existing
  guest entries.
- `Starts At` and `Ends At` are optional ISO 8601 UTC timestamps such as
  `2026-06-01T00:00:00Z`. Empty values remove the corresponding boundary.

Activity is computed from the time fields; there is no separate `Enabled` flag. Before `Starts At` the
season is upcoming, inside the interval it is active, and at or after `Ends At` it is ended. Upcoming and
ended seasons remain readable but reject submissions.

## Client API

```csharp
Mst.Client.Leaderboards.GetLeaderboards(callback);
Mst.Client.Leaderboards.GetEntries("survivalTime", callback, offset: 0, limit: 20);
Mst.Client.Leaderboards.GetEntriesAroundMe("survivalTime", callback, limit: 11);
Mst.Client.Leaderboards.GetMyEntry("survivalTime", callback);
Mst.Client.Leaderboards.SubmitScore("survivalTime", score, callback);
```

Client submission succeeds only when the selected definition has `Server Only` disabled. The master
ignores `AccountId` and `PlayerName` from a client packet and uses the authenticated account identity.

`GetEntries`, `GetEntriesAroundMe` and `GetMyEntry` cache each response for 60 real-time seconds inside
`LeaderboardsModuleClient`. The cache key includes the request kind, leaderboard key, season, page,
connection and authenticated account, so different leaderboards and pages never share a response.
Identical requests made while the first one is still in flight are combined into one network request.
Both successful and failed responses are cached; therefore callbacks can complete synchronously from
the cache. A connection-status change clears the cache, and a successful client score submission clears
cached reads for the affected leaderboard.

## Trusted Server API

```csharp
Mst.Server.Leaderboards.SubmitScore(
    accountId,
    "survivalTime",
    score,
    callback,
    playerName: playerName,
    playerAvatar: playerAvatar);
```

The trusted API accepts a target account, an optional current public player name and an optional public
HTTPS avatar URL. A missing name falls back to the MST account username. An empty avatar clears the
stored avatar. Unsupported URI schemes, malformed URLs and values longer than 512 characters are stored
as empty. Numeric permission level or account-admin state does not replace the exact `room_server`
capability.

Direct client submissions cannot set or clear an avatar. The master ignores the avatar value from a
client packet just as it ignores client-supplied account ID and player name.

## Ordering And Rank

All participants are stored. Page size controls only how many are returned. Entries are ordered by
score and then by `AccountId` using ordinal comparison, which gives stable ranks when scores are equal.
Rank is one-based and calculated as the number of entries ordered ahead of the player plus one.

`SubmitScore` returns the canonical persisted entry and `ScoreChanged`. With `Keep Best`, a worse replay
still succeeds but returns `ScoreChanged == false` and the previously stored score. Current player name
and avatar metadata are refreshed independently from score acceptance. This lets callers mirror the
confirmed canonical value instead of the rejected attempt while keeping presentation current.

## Persistence Contract

The provider table or collection is named `leaderboard_entries`. Its unique identity is
`LeaderboardKey + SeasonId + AccountId`. `SubmitScoreAsync` must compare and write atomically; the module
does not perform a vulnerable read-then-update sequence. Timestamps are stored in UTC. SQL installations
created before the avatar field was added must recreate the non-production `leaderboard_entries` table
before testing because the factory creates missing tables but does not migrate existing schemas.

Page entries, total count and current-player rank are separate database queries. Under active concurrent
submissions they are eventually consistent rather than one transactional snapshot. Deep offset paging is
also intentionally retained for the first version; cursor paging can be added later without changing the
stored score contract.

## Validation Checklist

- Submit from a trusted room to a `Server Only` leaderboard.
- Confirm a normal client is rejected by the same leaderboard.
- Disable `Server Only` and confirm the client can update only its own account.
- Test guest-enabled and guest-disabled definitions.
- Submit a better and worse value with `Keep Best` enabled.
- Confirm that a worse score preserves the score but refreshes the trusted player name and avatar.
- Confirm that empty and invalid avatar URLs display the configured UI fallback.
- Verify equal scores are ordered consistently by account ID.
- Verify upcoming and ended seasons remain readable but reject writes.
- Restart the master and confirm entries are restored from the selected database bridge.
