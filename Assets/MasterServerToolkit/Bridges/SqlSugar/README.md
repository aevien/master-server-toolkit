# SqlSugar Bridge

This bridge provides SQL database accessors through SqlSugar.

## Main Types

- `SqlSugarDatabaseAccessorFactory` configures the SQL provider.
- `AccountsDatabaseAccessorFactory`, `ProfilesDatabaseAccessorFactory`,
  `AnalyticsDatabaseAccessorFactory`, `GroupsDatabaseAccessorFactory`,
  `ChatDatabaseAccessorFactory`, and `LeaderboardsDatabaseAccessorFactory` register accessors for
  their owning modules.
- `AccountsDatabaseAccessor`, `ProfilesDatabaseAccessor`, `AnalyticsDatabaseAccessor`,
  `GroupsDatabaseAccessor`, `ChatDatabaseAccessor`, and `LeaderboardsDatabaseAccessor` implement
  module persistence.
- `TablesMapping` and `Models` define provider-side table mapping.

## Factory Ownership

Assign the matching factory to each module: accounts to `AuthModule`, profiles to `ProfilesModule`,
analytics to `AnalyticsModule`, groups to `GroupModule`, chat to `ChatModule`, and leaderboards to
`LeaderboardsModule`. Each factory creates and owns exactly one module accessor. Repeated calls to
`LeaderboardsDatabaseAccessorFactory.CreateAccessors()` reuse and, when necessary, re-register the
same accessor instead of opening another owner.

Factories may use the same connection settings and physical SQL database. This separation changes
runtime ownership only; existing tables and records do not require migration.

## Rules

- Keep SQL/SqlSugar types inside this bridge.
- Check required tables with `IsAnyTable(tableName, false)` during accessor startup and verify the
  table exists after `InitTables`; SqlSugar's cached table list can outlive externally deleted tables
  when Unity Domain Reload is disabled.
- Do not add migration scripts unless explicitly requested.
- If table/API mapping changes, update the current bridge code directly and document the new expectation.
- Do not log credentials or full connection strings.

## Authentication Persistence

Email-confirmation and password-reset codes are stored one record per normalized email. Saving a
new code replaces the previous code together with its UTC expiration time and remaining-attempt
counter. Validation is atomic: success consumes the code, an expired code is removed, an invalid
code decrements the counter, and the last invalid attempt removes the record. Concurrent validation
cannot consume the same code twice.

Existing `email_confirmation_codes` and `password_reset_codes` tables are upgraded during accessor
startup. Missing nullable `expires_at` and `attempts_left` columns are added explicitly instead of
relying on `InitTables` to alter an existing table. Legacy rows are backfilled as expired with no
remaining attempts, so codes created before this lifecycle contract cannot be accepted. New writes
always persist both values; all expiration comparisons use UTC.
Expiration values are truncated to whole UTC seconds before storage so the lifecycle behaves
consistently on providers whose SQL `datetime` type does not preserve sub-second precision.

Authentication-token revisions are stored in the separate server-owned `auth_token_revisions`
table. A missing row means revision `0`. Increment uses an optimistic compare-and-swap loop, so
parallel revocations cannot lose an increment. The revision is intentionally not stored in the
account row and normal token rotation does not change it.

## Leaderboard Persistence

`LeaderboardsDatabaseAccessor` stores one row per leaderboard, season, and account in
`leaderboard_entries`. The composite primary key is `(leaderboard_key, season_id, account_id)`.
`score` is a SQL `BIGINT`; player names are snapshots used for display and are limited to the MST
leaderboard packet limit of 64 characters. Public avatar snapshots are stored in `player_avatar`,
accept only HTTPS URLs and are limited to 512 characters. Creation and update timestamps are written
in UTC. Existing non-production tables created before `player_avatar` must be recreated because this
factory does not migrate an already existing schema.

Both ascending and descending ranking indexes start with leaderboard and season, then order by
score and finally by account ID. Page queries and rank counting use the same account-ID tie breaker,
so equal scores have stable positions.

Score submission uses an optimistic compare-and-swap loop. A concurrent first submission is
resolved by the composite key. Existing rows are updated only while the score, player-name and avatar
snapshot still match the version that was read. With `Keep Best` enabled, a worse or equal score
cannot replace a better score even when requests arrive concurrently, while changed display metadata
is still refreshed. With `Keep Best` disabled,
the last update that successfully completes the compare-and-swap loop becomes current.

The accessor supports leaderboard and season identifiers up to 64 characters and account IDs up to
38 characters. These limits keep the composite key and ranking indexes portable across the SQL
providers supported by this bridge.

## Inspector Configuration

`SqlSugarDatabaseAccessorFactory` defines fallback SQL settings. Runtime MST arguments override all
four values:

- `Connection String` is provider-specific and is overridden by `-mstDatabaseConnectionString`.
- `Auto Close Connection` asks SqlSugar to close connections after operations and is overridden by
  `-mstDatabaseAutoCloseConnection`.
- `Language` controls SqlSugar messages and diagnostics and is overridden by
  `-mstDatabaseLanguageType`.
- `Data Provider` selects the `DbType` used for query generation and is overridden by
  `-mstDatabaseProvider`.

The connection-string syntax and deployed driver must match `Data Provider`. These fields are excluded
from unsupported WebGL/iOS player builds, while remaining available in the Unity Editor.
