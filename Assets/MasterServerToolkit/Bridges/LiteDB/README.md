# LiteDB Bridge

This bridge provides embedded local database accessors for MST.

## Main Types

- `LiteDatabaseAccessorFactory` opens/configures LiteDB access.
- `AccountsLiteDbAccessorFactory`, `ChatLiteDbAccessorFactory`, `LeaderboardsLiteDbAccessorFactory`, and `ProfilesLiteDbAccessorFactory`
  register accessors for their owning modules.
- `AccountsDatabaseAccessor` implements auth/account persistence.
- `ProfilesDatabaseAccessor` implements profile persistence.
- `ChatDatabaseAccessor` implements optional chat message persistence.
- `LeaderboardsDatabaseAccessor` implements leaderboard score persistence and ranking queries.
- `Scripts/Models` contains provider-specific data models.

## Rules

- Keep LiteDB-specific types inside this bridge.
- Dispose database connections/accessors cleanly.
- Enable `LiteDatabase.UtcDate` for every database connection so persisted dates are read as UTC.
- Use this bridge for local/dev or embedded deployments, not as a hard dependency of MST core.
- Keep model mapping aligned with `IAccountsDatabaseAccessor`, `IProfilesDatabaseAccessor`,
  `IChatDatabaseAccessor`, and `ILeaderboardsDatabaseAccessor`.

## Authentication Data

The accounts database keeps authentication support data in separate server-owned collections:

- `reset_codes` stores one active password-reset code per normalized email;
- `email_confirmation_codes` stores one active email-confirmation code per normalized email;
- `auth_token_revisions` stores the current authentication-token revision per account.

Saving a verification code replaces the previous record for that email and resets both its UTC
expiration time and remaining attempt count. Validation is atomic: a valid code is consumed, an
expired code is removed, and every invalid code decrements the remaining attempts. Spending the
last attempt removes the record and returns `AttemptsExceeded`. Missing records and malformed input
return `Invalid`. Older code records that do not contain expiration metadata are treated as expired
and removed on their next validation attempt.

Authentication-token revisions are not stored on the account document. A missing revision record
means revision `0`; incrementing the revision is transactional and returns the newly persisted value.
This lets the authentication layer invalidate previously issued tokens without changing ordinary
token rotation behavior.

## Database Files

By default, each LiteDB module owns a separate database file and accessor lifetime:

- accounts use `accounts.db`;
- chat history uses `chat.db`;
- leaderboards use `leaderboards.db`;
- profiles use `profiles.db`.

`ChatModule` creates chat persistence through its own optional `ChatLiteDbAccessorFactory`. Do not
register chat persistence through `AccountsLiteDbAccessorFactory` or reuse the accounts database name.
Existing `chat_messages` data inside an older `accounts.db` file is not migrated automatically.

`LeaderboardsModule` creates leaderboard persistence through `LeaderboardsLiteDbAccessorFactory`.
The factory is idempotent and registers one accessor for its lifetime. Scores are stored in the
`leaderboard_entries` collection. The document ID uniquely identifies leaderboard key, season, and
account. Score comparison and update execute under one accessor-owned semaphore and one
LiteDB transaction. Page order and rank use the same deterministic rule: score first according to
the configured sort direction, then account ID in ordinal ascending order. Player name and public
HTTPS avatar are presentation snapshots. They are refreshed even when `Keep Best` rejects a worse
score; an empty trusted avatar clears the stored value.

LiteDB ranking queries materialize all entries for the requested leaderboard season before applying
the account-ID tie breaker. This is suitable for local, development, and smaller embedded servers.
Use a production database bridge for large public leaderboards where deep pages and frequent rank
queries must scale to large player populations.

## Inspector Configuration

Each module-specific factory exposes `Database Name`. It is the LiteDB file name used by that
accessor. Leave it empty only while adding a new factory in the Editor: `OnValidate` derives a default
name from the factory type. The leaderboards factory explicitly uses `leaderboards` when the value is
empty. Keep accounts, chat, leaderboards, and profiles on their documented separate names for
independent ownership and disposal.
