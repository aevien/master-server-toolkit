# MongoDB Bridge

This bridge provides MongoDB accessors and document models for MST.

## Main Types

- `MongoDbClientFactory` creates MongoDB clients.
- `AccountsDatabaseAccessorFactory`, `ChatDatabaseAccessorFactory`,
  `LeaderboardsDatabaseAccessorFactory`, and `ProfilesDatabaseAccessorFactory` register accessors
  for their owning modules.
- `AccountsDatabaseAccessor`, `ProfilesDatabaseAccessor`, `ProfilesDocumentDatabaseAccessor`,
  `ChatDatabaseAccessor`, and `LeaderboardsDatabaseAccessor` implement module persistence.
- `ChatDatabaseAccessorFactory` registers optional chat message persistence through the
  `chat_messages` collection.
- `LeaderboardsDatabaseAccessorFactory` registers cross-platform leaderboard persistence through
  the `leaderboard_entries` collection.
- `Scripts/Models` contains MongoDB-specific account, profile, chat, and leaderboard documents.

## Authentication Persistence

`AccountsDatabaseAccessor` keeps authentication data in separate collections:

- `accounts` stores account records;
- `account_blocks` stores account blocks;
- `account_service_bindings` stores external-service bindings;
- `resetCodes` stores password-reset codes;
- `emailConfirmationCodes` stores email-confirmation codes;
- `auth_token_revisions` stores server-owned token revisions by account ID.

Password-reset and email-confirmation codes are one-time records. Saving a code replaces the previous
record for the normalized email and resets its UTC expiration time and remaining-attempt count. Code
validation is performed with conditional atomic MongoDB operations:

- a valid active code is deleted and returns `Success`;
- an expired code is deleted and returns `Expired`;
- an invalid code decrements `AttemptsLeft`;
- the last invalid attempt deletes the record and returns `AttemptsExceeded`;
- missing records and invalid input return `Invalid`.

All expiration timestamps are stored as UTC. Existing code documents without expiration or attempt
fields are treated as expired and removed on their next validation.

Authentication-token revisions are not stored in account documents and are never exposed through the
account model. A missing revision is `0`. Revocation uses MongoDB `$inc` with an upsert and returns the
new revision atomically, so concurrent revocations cannot lose increments.

## Rules

- Keep MongoDB driver types out of MST core modules.
- Do not log connection strings or credentials.
- Dispose or reuse client/session resources according to driver expectations.
- Keep query behavior compatible with auth/profile module contracts.

## Leaderboard Persistence

`LeaderboardsDatabaseAccessor` stores one entry for every leaderboard, season, and account in the
`leaderboard_entries` collection. A unique compound index on `LeaderboardKey`, `SeasonId`, and
`AccountId` prevents duplicate participant rows. Separate ascending and descending rank indexes use
`AccountId` ascending as the stable tie breaker. Rank counting uses the same ordering rule as page
queries, so equal scores receive deterministic consecutive positions.

Scores are stored as signed 64-bit integers. `CreatedAtUtc` and `UpdatedAtUtc` are explicitly stored
as UTC dates. The accessor applies score policies atomically per MongoDB document:

- with `KeepBest` enabled, a conditional `FindOneAndUpdate` replaces only a worse score;
- a missing entry is inserted, and a duplicate-key race retries the conditional update;
- a rejected `KeepBest` score can still update changed player name and public HTTPS avatar metadata
  atomically; its score remains unchanged, and `UpdatedAtUtc` changes only when stored metadata changes;
- with `KeepBest` disabled, an atomic upsert stores the latest completed submission, retrying as an
  update when two processes create the same participant entry concurrently.

MongoDB operations do not provide a transaction-wide snapshot across page, count, and player-rank
queries. Rankings can therefore shift while active submissions are arriving. Each individual entry
update remains atomic.

## Factory Ownership

Assign `AccountsDatabaseAccessorFactory` to `AuthModule`, `ChatDatabaseAccessorFactory` to
`ChatModule`, `LeaderboardsDatabaseAccessorFactory` to `LeaderboardsModule`, and
`ProfilesDatabaseAccessorFactory` to `ProfilesModule`. The accounts factory does not register chat or
leaderboard persistence.

All module factories may reference the same `MongoDbClientFactory`. They still use one physical
MongoDB database and separate collections, so splitting factory ownership does not require a data
migration.

## Profile Persistence

`ProfilesDatabaseAccessor` stores a complete binary profile payload. `ProfilesDocumentDatabaseAccessor`
stores the profile as a MongoDB document. Both accessors support single and batch saves through the same
contract:

- profile inputs are validated and serialized into detached save snapshots before MongoDB is called;
- an empty batch is a successful no-op;
- saves use `UpdateOne` upserts keyed by `UserId`;
- only the payload and `UserId` on insert are updated, so MongoDB keeps the existing immutable `_id`;
- driver and serialization failures are propagated to `ProfilesModule`, which keeps the pending save for
  its normal retry cycle.

A batch uses MongoDB ordered bulk writes but is not wrapped in a transaction. A connection failure can
therefore persist a prefix of the batch. Retrying is safe because each operation is an idempotent `$set`
upsert for the same `UserId`.

## Inspector Configuration

Add one `MongoDbClientFactory` to the server object and assign it to the module-specific accessor
factories:

- `Default Connection String` is the fallback MongoDB driver URI. The
  `-mstDatabaseConfiguration` argument overrides it at startup.
- `Database Name` selects the physical MongoDB database shared by the referenced accessors. It is
  required; an empty value is reset to `masterServerToolkit`.

`ProfilesDatabaseAccessorFactory.Mongo Db Client Factory` is required. `Save Data As Bytes` selects
the profile representation:

- enabled: one complete binary profile payload is stored per user;
- disabled: profile values are stored as a MongoDB document.

Changing the representation does not convert existing records. Keep this setting stable for a live
database unless data conversion is handled separately.

The `Mongo Db Client Factory` reference on accounts, chat, and leaderboards factories is required for
the same reason as on profiles: it supplies the shared client and selected database. The module
factories own their accessors, but they do not create independent MongoDB clients. Repeated calls to
the leaderboards factory are idempotent: the already registered accessor is reused instead of being
replaced and disposed.
