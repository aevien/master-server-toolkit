#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public sealed class LeaderboardsDatabaseAccessor : ILeaderboardsDatabaseAccessor
    {
        private const string CollectionName = "leaderboard_entries";

        private readonly IMongoCollection<LeaderboardEntryMongoDB> entriesCollection;

        public MstProperties CustomProperties { get; } = new MstProperties();
        public Logger Logger { get; set; }

        public LeaderboardsDatabaseAccessor(string connectionString, string databaseName)
            : this(new MongoClient(connectionString), databaseName) { }

        public LeaderboardsDatabaseAccessor(MongoClient client, string databaseName)
        {
            if (client == null)
                throw new ArgumentNullException(nameof(client));

            if (string.IsNullOrWhiteSpace(databaseName))
                throw new ArgumentException("MongoDB database name is required", nameof(databaseName));

            entriesCollection = client
                .GetDatabase(databaseName.Trim())
                .GetCollection<LeaderboardEntryMongoDB>(CollectionName);

            CreateIndexes();
        }

        public Task<LeaderboardScoreUpdateResult> SubmitScoreAsync(
            LeaderboardScoreSubmission submission,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LeaderboardScoreSubmission normalizedSubmission = NormalizeSubmission(submission);

            return normalizedSubmission.KeepBest
                ? SubmitBestScoreAsync(normalizedSubmission, cancellationToken)
                : SubmitLatestScoreAsync(normalizedSubmission, cancellationToken);
        }

        public async Task<LeaderboardEntry> GetEntryAsync(
            string leaderboardKey,
            string seasonId,
            string accountId,
            CancellationToken cancellationToken = default)
        {
            FilterDefinition<LeaderboardEntryMongoDB> filter = CreateIdentityFilter(
                NormalizeLeaderboardKey(leaderboardKey),
                NormalizeSeasonId(seasonId),
                NormalizeRequired(accountId, nameof(accountId)));

            LeaderboardEntryMongoDB entry = await entriesCollection
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return entry?.ToEntry();
        }

        public async Task<IReadOnlyList<LeaderboardEntry>> GetEntriesAsync(
            string leaderboardKey,
            string seasonId,
            LeaderboardSortOrder sortOrder,
            int offset,
            int limit,
            CancellationToken cancellationToken = default)
        {
            ValidateSortOrder(sortOrder);

            if (limit <= 0)
                return Array.Empty<LeaderboardEntry>();

            string normalizedKey = NormalizeLeaderboardKey(leaderboardKey);
            string normalizedSeasonId = NormalizeSeasonId(seasonId);
            FilterDefinition<LeaderboardEntryMongoDB> filter = CreateSeasonFilter(
                normalizedKey, normalizedSeasonId);

            List<LeaderboardEntryMongoDB> documents = await entriesCollection
                .Find(filter)
                .Sort(CreateRankSort(sortOrder))
                .Skip(Math.Max(0, offset))
                .Limit(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var entries = new List<LeaderboardEntry>(documents.Count);

            foreach (LeaderboardEntryMongoDB document in documents)
                entries.Add(document.ToEntry());

            return entries;
        }

        public Task<long> CountEntriesAsync(
            string leaderboardKey,
            string seasonId,
            CancellationToken cancellationToken = default)
        {
            FilterDefinition<LeaderboardEntryMongoDB> filter = CreateSeasonFilter(
                NormalizeLeaderboardKey(leaderboardKey),
                NormalizeSeasonId(seasonId));

            return entriesCollection.CountDocumentsAsync(
                filter,
                cancellationToken: cancellationToken);
        }

        public Task<long> CountBetterEntriesAsync(
            string leaderboardKey,
            string seasonId,
            LeaderboardSortOrder sortOrder,
            long score,
            string accountId,
            CancellationToken cancellationToken = default)
        {
            ValidateSortOrder(sortOrder);

            string normalizedAccountId = NormalizeRequired(accountId, nameof(accountId));
            FilterDefinitionBuilder<LeaderboardEntryMongoDB> filters =
                Builders<LeaderboardEntryMongoDB>.Filter;
            FilterDefinition<LeaderboardEntryMongoDB> betterScore =
                sortOrder == LeaderboardSortOrder.Ascending
                    ? filters.Lt(entry => entry.Score, score)
                    : filters.Gt(entry => entry.Score, score);
            FilterDefinition<LeaderboardEntryMongoDB> sameScoreEarlierAccount = filters.And(
                filters.Eq(entry => entry.Score, score),
                filters.Lt(entry => entry.AccountId, normalizedAccountId));
            FilterDefinition<LeaderboardEntryMongoDB> filter = filters.And(
                CreateSeasonFilter(
                    NormalizeLeaderboardKey(leaderboardKey),
                    NormalizeSeasonId(seasonId)),
                filters.Or(betterScore, sameScoreEarlierAccount));

            return entriesCollection.CountDocumentsAsync(
                filter,
                cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            CustomProperties.Clear();
        }

        private async Task<LeaderboardScoreUpdateResult> SubmitBestScoreAsync(
            LeaderboardScoreSubmission submission,
            CancellationToken cancellationToken)
        {
            LeaderboardEntryMongoDB updated = await TryUpdateBestScoreAsync(
                    submission, cancellationToken)
                .ConfigureAwait(false);

            if (updated != null)
                return CreateUpdateResult(updated, true);

            var inserted = LeaderboardEntryMongoDB.FromSubmission(
                submission,
                Mst.Helper.CreateGuidString(),
                submission.SubmittedAtUtc);

            try
            {
                await entriesCollection.InsertOneAsync(
                    inserted,
                    cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                return CreateUpdateResult(inserted, true);
            }
            catch (Exception exception) when (IsDuplicateKey(exception))
            {
                updated = await TryUpdateBestScoreAsync(
                    submission, cancellationToken).ConfigureAwait(false);

                if (updated != null)
                    return CreateUpdateResult(updated, true);

                LeaderboardEntryMongoDB metadataUpdated = await TryUpdatePlayerMetadataAsync(
                        submission, cancellationToken)
                    .ConfigureAwait(false);

                if (metadataUpdated != null)
                    return CreateUpdateResult(metadataUpdated, false);

                LeaderboardEntryMongoDB existing = await entriesCollection
                    .Find(CreateIdentityFilter(submission))
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (existing == null)
                    throw new InvalidOperationException(
                        "Leaderboard entry disappeared during a concurrent score submission",
                        exception);

                return CreateUpdateResult(existing, false);
            }
        }

        private async Task<LeaderboardScoreUpdateResult> SubmitLatestScoreAsync(
            LeaderboardScoreSubmission submission,
            CancellationToken cancellationToken)
        {
            string insertedId = Mst.Helper.CreateGuidString();

            try
            {
                LeaderboardEntryMongoDB previous = await ReplaceLatestScoreAsync(
                    submission, insertedId, true, cancellationToken).ConfigureAwait(false);

                return CreateLatestScoreResult(submission, insertedId, previous);
            }
            catch (Exception exception) when (IsDuplicateKey(exception))
            {
                LeaderboardEntryMongoDB previous = await ReplaceLatestScoreAsync(
                    submission, insertedId, false, cancellationToken).ConfigureAwait(false);

                if (previous == null)
                    throw new InvalidOperationException(
                        "Leaderboard entry disappeared during a concurrent score submission",
                        exception);

                return CreateLatestScoreResult(submission, insertedId, previous);
            }
        }

        private Task<LeaderboardEntryMongoDB> TryUpdateBestScoreAsync(
            LeaderboardScoreSubmission submission,
            CancellationToken cancellationToken)
        {
            FilterDefinitionBuilder<LeaderboardEntryMongoDB> filters =
                Builders<LeaderboardEntryMongoDB>.Filter;
            FilterDefinition<LeaderboardEntryMongoDB> improvesScore =
                submission.SortOrder == LeaderboardSortOrder.Ascending
                    ? filters.Gt(entry => entry.Score, submission.Score)
                    : filters.Lt(entry => entry.Score, submission.Score);
            FilterDefinition<LeaderboardEntryMongoDB> filter = filters.And(
                CreateIdentityFilter(submission),
                improvesScore);
            UpdateDefinitionBuilder<LeaderboardEntryMongoDB> updates =
                Builders<LeaderboardEntryMongoDB>.Update;
            var updateParts = new List<UpdateDefinition<LeaderboardEntryMongoDB>>
            {
                updates.Set(entry => entry.PlayerName, submission.PlayerName),
                updates.Set(entry => entry.Score, submission.Score),
                updates.Set(entry => entry.UpdatedAtUtc, submission.SubmittedAtUtc)
            };

            if (submission.PlayerAvatar != null)
                updateParts.Add(updates.Set(entry => entry.PlayerAvatar, submission.PlayerAvatar));

            UpdateDefinition<LeaderboardEntryMongoDB> update = updates.Combine(updateParts);
            var options = new FindOneAndUpdateOptions<LeaderboardEntryMongoDB>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            };

            return entriesCollection.FindOneAndUpdateAsync(
                filter,
                update,
                options,
                cancellationToken);
        }

        private Task<LeaderboardEntryMongoDB> TryUpdatePlayerMetadataAsync(
            LeaderboardScoreSubmission submission,
            CancellationToken cancellationToken)
        {
            FilterDefinitionBuilder<LeaderboardEntryMongoDB> filters =
                Builders<LeaderboardEntryMongoDB>.Filter;
            FilterDefinition<LeaderboardEntryMongoDB> metadataChanged =
                filters.Ne(entry => entry.PlayerName, submission.PlayerName);

            if (submission.PlayerAvatar != null)
            {
                metadataChanged = filters.Or(
                    metadataChanged,
                    filters.Ne(entry => entry.PlayerAvatar, submission.PlayerAvatar));
            }

            FilterDefinition<LeaderboardEntryMongoDB> filter = filters.And(
                CreateIdentityFilter(submission),
                metadataChanged);
            UpdateDefinitionBuilder<LeaderboardEntryMongoDB> updates =
                Builders<LeaderboardEntryMongoDB>.Update;
            var updateParts = new List<UpdateDefinition<LeaderboardEntryMongoDB>>
            {
                updates.Set(entry => entry.PlayerName, submission.PlayerName),
                updates.Set(entry => entry.UpdatedAtUtc, submission.SubmittedAtUtc)
            };

            if (submission.PlayerAvatar != null)
                updateParts.Add(updates.Set(entry => entry.PlayerAvatar, submission.PlayerAvatar));

            UpdateDefinition<LeaderboardEntryMongoDB> update = updates.Combine(updateParts);
            var options = new FindOneAndUpdateOptions<LeaderboardEntryMongoDB>
            {
                IsUpsert = false,
                ReturnDocument = ReturnDocument.After
            };

            return entriesCollection.FindOneAndUpdateAsync(
                filter,
                update,
                options,
                cancellationToken);
        }

        private Task<LeaderboardEntryMongoDB> ReplaceLatestScoreAsync(
            LeaderboardScoreSubmission submission,
            string insertedId,
            bool allowInsert,
            CancellationToken cancellationToken)
        {
            UpdateDefinitionBuilder<LeaderboardEntryMongoDB> updates =
                Builders<LeaderboardEntryMongoDB>.Update;
            var updateParts = new List<UpdateDefinition<LeaderboardEntryMongoDB>>
            {
                updates.Set(entry => entry.PlayerName, submission.PlayerName),
                updates.Set(entry => entry.Score, submission.Score),
                updates.Set(entry => entry.UpdatedAtUtc, submission.SubmittedAtUtc),
                updates.SetOnInsert(entry => entry.Id, insertedId),
                updates.SetOnInsert(entry => entry.LeaderboardKey, submission.LeaderboardKey),
                updates.SetOnInsert(entry => entry.SeasonId, submission.SeasonId),
                updates.SetOnInsert(entry => entry.AccountId, submission.AccountId),
                updates.SetOnInsert(entry => entry.CreatedAtUtc, submission.SubmittedAtUtc)
            };

            updateParts.Add(submission.PlayerAvatar == null
                ? updates.SetOnInsert(entry => entry.PlayerAvatar, string.Empty)
                : updates.Set(entry => entry.PlayerAvatar, submission.PlayerAvatar));

            UpdateDefinition<LeaderboardEntryMongoDB> update = updates.Combine(updateParts);
            var options = new FindOneAndUpdateOptions<LeaderboardEntryMongoDB>
            {
                IsUpsert = allowInsert,
                ReturnDocument = ReturnDocument.Before
            };

            return entriesCollection.FindOneAndUpdateAsync(
                CreateIdentityFilter(submission),
                update,
                options,
                cancellationToken);
        }

        private static LeaderboardScoreUpdateResult CreateLatestScoreResult(
            LeaderboardScoreSubmission submission,
            string insertedId,
            LeaderboardEntryMongoDB previous)
        {
            LeaderboardEntryMongoDB current = LeaderboardEntryMongoDB.FromSubmission(
                submission,
                previous?.Id ?? insertedId,
                previous?.CreatedAtUtc ?? submission.SubmittedAtUtc);
            current.PlayerAvatar = submission.PlayerAvatar ?? previous?.PlayerAvatar ?? string.Empty;

            return new LeaderboardScoreUpdateResult
            {
                Entry = current.ToEntry(),
                ScoreChanged = previous == null || previous.Score != submission.Score
            };
        }

        private static LeaderboardScoreUpdateResult CreateUpdateResult(
            LeaderboardEntryMongoDB entry,
            bool scoreChanged)
        {
            return new LeaderboardScoreUpdateResult
            {
                Entry = entry.ToEntry(),
                ScoreChanged = scoreChanged
            };
        }

        private static LeaderboardScoreSubmission NormalizeSubmission(
            LeaderboardScoreSubmission submission)
        {
            if (submission == null)
                throw new ArgumentNullException(nameof(submission));

            ValidateSortOrder(submission.SortOrder);
            string playerName = submission.PlayerName?.Trim() ?? string.Empty;

            if (playerName.Length > LeaderboardEntry.MaxPlayerNameLength)
                playerName = playerName.Substring(0, LeaderboardEntry.MaxPlayerNameLength);

            return new LeaderboardScoreSubmission
            {
                LeaderboardKey = NormalizeLeaderboardKey(submission.LeaderboardKey),
                SeasonId = NormalizeSeasonId(submission.SeasonId),
                AccountId = NormalizeRequired(
                    submission.AccountId, nameof(submission.AccountId)),
                PlayerName = playerName,
                PlayerAvatar = submission.PlayerAvatar == null
                    ? null
                    : LeaderboardEntry.NormalizePlayerAvatar(submission.PlayerAvatar),
                Score = submission.Score,
                SortOrder = submission.SortOrder,
                KeepBest = submission.KeepBest,
                SubmittedAtUtc = NormalizeUtc(submission.SubmittedAtUtc)
            };
        }

        private void CreateIndexes()
        {
            entriesCollection.Indexes.CreateOne(
                new CreateIndexModel<LeaderboardEntryMongoDB>(
                    Builders<LeaderboardEntryMongoDB>.IndexKeys
                        .Ascending(entry => entry.LeaderboardKey)
                        .Ascending(entry => entry.SeasonId)
                        .Ascending(entry => entry.AccountId),
                    new CreateIndexOptions
                    {
                        Name = "leaderboard_identity_unique",
                        Unique = true
                    }));

            entriesCollection.Indexes.CreateOne(
                new CreateIndexModel<LeaderboardEntryMongoDB>(
                    Builders<LeaderboardEntryMongoDB>.IndexKeys
                        .Ascending(entry => entry.LeaderboardKey)
                        .Ascending(entry => entry.SeasonId)
                        .Ascending(entry => entry.Score)
                        .Ascending(entry => entry.AccountId),
                    new CreateIndexOptions { Name = "leaderboard_rank_ascending" }));

            entriesCollection.Indexes.CreateOne(
                new CreateIndexModel<LeaderboardEntryMongoDB>(
                    Builders<LeaderboardEntryMongoDB>.IndexKeys
                        .Ascending(entry => entry.LeaderboardKey)
                        .Ascending(entry => entry.SeasonId)
                        .Descending(entry => entry.Score)
                        .Ascending(entry => entry.AccountId),
                    new CreateIndexOptions { Name = "leaderboard_rank_descending" }));
        }

        private static FilterDefinition<LeaderboardEntryMongoDB> CreateIdentityFilter(
            LeaderboardScoreSubmission submission)
        {
            return CreateIdentityFilter(
                submission.LeaderboardKey,
                submission.SeasonId,
                submission.AccountId);
        }

        private static FilterDefinition<LeaderboardEntryMongoDB> CreateIdentityFilter(
            string leaderboardKey,
            string seasonId,
            string accountId)
        {
            FilterDefinitionBuilder<LeaderboardEntryMongoDB> filters =
                Builders<LeaderboardEntryMongoDB>.Filter;

            return filters.And(
                filters.Eq(entry => entry.LeaderboardKey, leaderboardKey),
                filters.Eq(entry => entry.SeasonId, seasonId),
                filters.Eq(entry => entry.AccountId, accountId));
        }

        private static FilterDefinition<LeaderboardEntryMongoDB> CreateSeasonFilter(
            string leaderboardKey,
            string seasonId)
        {
            FilterDefinitionBuilder<LeaderboardEntryMongoDB> filters =
                Builders<LeaderboardEntryMongoDB>.Filter;

            return filters.And(
                filters.Eq(entry => entry.LeaderboardKey, leaderboardKey),
                filters.Eq(entry => entry.SeasonId, seasonId));
        }

        private static SortDefinition<LeaderboardEntryMongoDB> CreateRankSort(
            LeaderboardSortOrder sortOrder)
        {
            SortDefinitionBuilder<LeaderboardEntryMongoDB> sort =
                Builders<LeaderboardEntryMongoDB>.Sort;

            return sortOrder == LeaderboardSortOrder.Ascending
                ? sort.Ascending(entry => entry.Score).Ascending(entry => entry.AccountId)
                : sort.Descending(entry => entry.Score).Ascending(entry => entry.AccountId);
        }

        private static bool IsDuplicateKey(Exception exception)
        {
            return exception is MongoDuplicateKeyException ||
                   exception is MongoWriteException writeException &&
                   writeException.WriteError?.Category == ServerErrorCategory.DuplicateKey;
        }

        private static void ValidateSortOrder(LeaderboardSortOrder sortOrder)
        {
            if (!Enum.IsDefined(typeof(LeaderboardSortOrder), sortOrder))
                throw new ArgumentOutOfRangeException(nameof(sortOrder));
        }

        private static string NormalizeRequired(string value, string parameterName)
        {
            string normalized = value?.Trim() ?? string.Empty;

            if (normalized.Length == 0)
                throw new ArgumentException("Value is required", parameterName);

            return normalized;
        }

        private static string NormalizeSeasonId(string seasonId)
        {
            string normalized = string.IsNullOrWhiteSpace(seasonId)
                ? LeaderboardDefinition.AllTimeSeasonId
                : seasonId.Trim();

            if (normalized.Length > LeaderboardDefinition.MaxSeasonIdLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(seasonId),
                    seasonId,
                    $"Season identifier cannot exceed {LeaderboardDefinition.MaxSeasonIdLength} characters");
            }

            return normalized;
        }

        private static string NormalizeLeaderboardKey(string leaderboardKey)
        {
            string normalized = NormalizeRequired(leaderboardKey, nameof(leaderboardKey));

            if (normalized.Length > LeaderboardDefinition.MaxKeyLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaderboardKey),
                    leaderboardKey,
                    $"Leaderboard key cannot exceed {LeaderboardDefinition.MaxKeyLength} characters");
            }

            return normalized;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default)
                return DateTime.UtcNow;

            if (value.Kind == DateTimeKind.Utc)
                return value;

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}

#endif
