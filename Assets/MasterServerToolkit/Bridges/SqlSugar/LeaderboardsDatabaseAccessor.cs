using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    /// <summary>
    /// Persists authoritative MST leaderboard entries in a SQL database through SqlSugar.
    /// </summary>
    public sealed class LeaderboardsDatabaseAccessor : ILeaderboardsDatabaseAccessor
    {
        private readonly ConnectionConfig configuration;

        public MstProperties CustomProperties { get; } = new MstProperties();
        public Logger Logger { get; set; }

        public LeaderboardsDatabaseAccessor(ConnectionConfig configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            using SqlSugarClient db = new SqlSugarClient(configuration);
            string tableName = db.EntityMaintenance.GetTableName(typeof(LeaderboardEntryData));

            if (!db.DbMaintenance.IsAnyTable(tableName, false))
                db.CodeFirst.InitTables(typeof(LeaderboardEntryData));

            if (!db.DbMaintenance.IsAnyTable(tableName, false))
                throw new InvalidOperationException($"Required database table '{tableName}' was not created");
        }

        public void Dispose() { }

        public async Task<LeaderboardScoreUpdateResult> SubmitScoreAsync(
            LeaderboardScoreSubmission submission,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LeaderboardEntryData incoming = CreateData(submission);

            using SqlSugarClient db = new SqlSugarClient(configuration);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                LeaderboardEntryData stored = await FindEntryDataAsync(
                    db, incoming.LeaderboardKey, incoming.SeasonId, incoming.AccountId,
                    cancellationToken).ConfigureAwait(false);

                if (stored == null)
                {
                    if (!await TryInsertAsync(db, incoming, cancellationToken).ConfigureAwait(false))
                        continue;

                    return new LeaderboardScoreUpdateResult
                    {
                        Entry = incoming.ToEntry(),
                        ScoreChanged = true
                    };
                }

                bool replaceScore = !submission.KeepBest ||
                    IsBetter(incoming.Score, stored.Score, submission.SortOrder);
                bool scoreChanged = replaceScore && incoming.Score != stored.Score;
                bool playerNameChanged = !string.Equals(
                    incoming.PlayerName,
                    stored.PlayerName,
                    StringComparison.Ordinal);
                bool playerAvatarChanged = submission.PlayerAvatar != null && !string.Equals(
                    incoming.PlayerAvatar,
                    stored.PlayerAvatar,
                    StringComparison.Ordinal);

                if (!replaceScore && !playerNameChanged && !playerAvatarChanged)
                {
                    return new LeaderboardScoreUpdateResult
                    {
                        Entry = stored.ToEntry(),
                        ScoreChanged = false
                    };
                }

                long resultingScore = replaceScore ? incoming.Score : stored.Score;
                string resultingPlayerAvatar = playerAvatarChanged
                    ? incoming.PlayerAvatar
                    : stored.PlayerAvatar;
                int updatedRows = await db.Updateable<LeaderboardEntryData>()
                    .SetColumns(entry => entry.Score == resultingScore)
                    .SetColumns(entry => entry.PlayerName == incoming.PlayerName)
                    .SetColumns(entry => entry.PlayerAvatar == resultingPlayerAvatar)
                    .SetColumns(entry => entry.UpdatedAtUtc == incoming.UpdatedAtUtc)
                    .Where(entry =>
                        entry.LeaderboardKey == incoming.LeaderboardKey &&
                        entry.SeasonId == incoming.SeasonId &&
                        entry.AccountId == incoming.AccountId &&
                        entry.Score == stored.Score &&
                        entry.PlayerName == stored.PlayerName &&
                        entry.PlayerAvatar == stored.PlayerAvatar)
                    .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);

                if (updatedRows == 0)
                    continue;

                LeaderboardEntryData persisted = await FindEntryDataAsync(
                    db, incoming.LeaderboardKey, incoming.SeasonId, incoming.AccountId,
                    cancellationToken).ConfigureAwait(false);

                if (persisted == null)
                    throw new InvalidOperationException("Updated leaderboard entry could not be reloaded");

                return new LeaderboardScoreUpdateResult
                {
                    Entry = persisted.ToEntry(),
                    ScoreChanged = scoreChanged
                };
            }
        }

        public async Task<LeaderboardEntry> GetEntryAsync(
            string leaderboardKey,
            string seasonId,
            string accountId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            leaderboardKey = NormalizeOptional(leaderboardKey);
            seasonId = NormalizeSeasonId(seasonId);
            accountId = NormalizeOptional(accountId);

            if (leaderboardKey.Length == 0 || accountId.Length == 0)
                return null;

            using SqlSugarClient db = new SqlSugarClient(configuration);
            LeaderboardEntryData entry = await FindEntryDataAsync(
                db, leaderboardKey, seasonId, accountId, cancellationToken).ConfigureAwait(false);

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
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSortOrder(sortOrder);
            leaderboardKey = NormalizeOptional(leaderboardKey);
            seasonId = NormalizeSeasonId(seasonId);

            if (leaderboardKey.Length == 0 || limit <= 0)
                return Array.Empty<LeaderboardEntry>();

            offset = Math.Max(0, offset);

            using SqlSugarClient db = new SqlSugarClient(configuration);
            ISugarQueryable<LeaderboardEntryData> query = db.Queryable<LeaderboardEntryData>()
                .Where(entry =>
                    entry.LeaderboardKey == leaderboardKey &&
                    entry.SeasonId == seasonId);

            query = sortOrder == LeaderboardSortOrder.Descending
                ? query.OrderBy(entry => entry.Score, OrderByType.Desc)
                : query.OrderBy(entry => entry.Score, OrderByType.Asc);

            List<LeaderboardEntryData> data = await query
                .OrderBy(entry => entry.AccountId, OrderByType.Asc)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken).ConfigureAwait(false);

            var entries = new List<LeaderboardEntry>(data.Count);

            foreach (LeaderboardEntryData item in data)
                entries.Add(item.ToEntry());

            return entries;
        }

        public async Task<long> CountEntriesAsync(
            string leaderboardKey,
            string seasonId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            leaderboardKey = NormalizeOptional(leaderboardKey);
            seasonId = NormalizeSeasonId(seasonId);

            if (leaderboardKey.Length == 0)
                return 0L;

            using SqlSugarClient db = new SqlSugarClient(configuration);
            int count = await db.Queryable<LeaderboardEntryData>()
                .CountAsync(entry =>
                    entry.LeaderboardKey == leaderboardKey &&
                    entry.SeasonId == seasonId,
                    cancellationToken).ConfigureAwait(false);

            return count;
        }

        public async Task<long> CountBetterEntriesAsync(
            string leaderboardKey,
            string seasonId,
            LeaderboardSortOrder sortOrder,
            long score,
            string accountId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateSortOrder(sortOrder);
            leaderboardKey = NormalizeOptional(leaderboardKey);
            seasonId = NormalizeSeasonId(seasonId);
            accountId = NormalizeOptional(accountId);

            if (leaderboardKey.Length == 0 || accountId.Length == 0)
                return 0L;

            using SqlSugarClient db = new SqlSugarClient(configuration);
            ISugarQueryable<LeaderboardEntryData> query = db.Queryable<LeaderboardEntryData>()
                .Where(entry =>
                    entry.LeaderboardKey == leaderboardKey &&
                    entry.SeasonId == seasonId);

            int count = sortOrder == LeaderboardSortOrder.Descending
                ? await query.CountAsync(entry =>
                    entry.Score > score ||
                    entry.Score == score && SqlFunc.CompareTo(entry.AccountId, accountId) < 0,
                    cancellationToken).ConfigureAwait(false)
                : await query.CountAsync(entry =>
                    entry.Score < score ||
                    entry.Score == score && SqlFunc.CompareTo(entry.AccountId, accountId) < 0,
                    cancellationToken).ConfigureAwait(false);

            return count;
        }

        private static async Task<LeaderboardEntryData> FindEntryDataAsync(
            SqlSugarClient db,
            string leaderboardKey,
            string seasonId,
            string accountId,
            CancellationToken cancellationToken)
        {
            return await db.Queryable<LeaderboardEntryData>()
                .FirstAsync(entry =>
                    entry.LeaderboardKey == leaderboardKey &&
                    entry.SeasonId == seasonId &&
                    entry.AccountId == accountId,
                    cancellationToken).ConfigureAwait(false);
        }

        private static async Task<bool> TryInsertAsync(
            SqlSugarClient db,
            LeaderboardEntryData entry,
            CancellationToken cancellationToken)
        {
            try
            {
                await db.Insertable(entry)
                    .ExecuteCommandAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool competingInsertSucceeded = await db.Queryable<LeaderboardEntryData>()
                    .AnyAsync(item =>
                        item.LeaderboardKey == entry.LeaderboardKey &&
                        item.SeasonId == entry.SeasonId &&
                        item.AccountId == entry.AccountId,
                        cancellationToken).ConfigureAwait(false);

                if (!competingInsertSucceeded)
                    throw;

                return false;
            }
        }

        private static LeaderboardEntryData CreateData(LeaderboardScoreSubmission submission)
        {
            if (submission == null)
                throw new ArgumentNullException(nameof(submission));

            ValidateSortOrder(submission.SortOrder);

            string leaderboardKey = NormalizeRequired(
                submission.LeaderboardKey, nameof(submission.LeaderboardKey),
                LeaderboardEntryData.MaxLeaderboardKeyLength);
            string seasonId = NormalizeSeasonId(submission.SeasonId);
            string accountId = NormalizeRequired(
                submission.AccountId, nameof(submission.AccountId),
                LeaderboardEntryData.MaxAccountIdLength);

            if (seasonId.Length > LeaderboardEntryData.MaxSeasonIdLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(submission.SeasonId), submission.SeasonId,
                    $"Season identifier cannot exceed {LeaderboardEntryData.MaxSeasonIdLength} characters");
            }

            string playerName = NormalizeOptional(submission.PlayerName);

            if (playerName.Length > LeaderboardEntry.MaxPlayerNameLength)
                playerName = playerName.Substring(0, LeaderboardEntry.MaxPlayerNameLength);

            DateTime submittedAtUtc = NormalizeUtc(submission.SubmittedAtUtc);

            return new LeaderboardEntryData
            {
                LeaderboardKey = leaderboardKey,
                SeasonId = seasonId,
                AccountId = accountId,
                PlayerName = playerName,
                PlayerAvatar = submission.PlayerAvatar == null
                    ? string.Empty
                    : LeaderboardEntry.NormalizePlayerAvatar(submission.PlayerAvatar),
                Score = submission.Score,
                CreatedAtUtc = submittedAtUtc,
                UpdatedAtUtc = submittedAtUtc
            };
        }

        private static bool IsBetter(long candidate, long stored, LeaderboardSortOrder sortOrder)
        {
            return sortOrder == LeaderboardSortOrder.Descending
                ? candidate > stored
                : candidate < stored;
        }

        private static void ValidateSortOrder(LeaderboardSortOrder sortOrder)
        {
            if (!Enum.IsDefined(typeof(LeaderboardSortOrder), sortOrder))
                throw new ArgumentOutOfRangeException(nameof(sortOrder), sortOrder, "Unsupported leaderboard sort order");
        }

        private static string NormalizeRequired(string value, string parameterName, int maxLength)
        {
            string normalized = NormalizeOptional(value);

            if (normalized.Length == 0)
                throw new ArgumentException("Value is required", parameterName);

            if (normalized.Length > maxLength)
            {
                throw new ArgumentOutOfRangeException(
                    parameterName, value, $"Value cannot exceed {maxLength} characters");
            }

            return normalized;
        }

        private static string NormalizeSeasonId(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? LeaderboardDefinition.AllTimeSeasonId
                : value.Trim();
        }

        private static string NormalizeOptional(string value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default)
                return DateTime.UtcNow;

            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }
    }
}
