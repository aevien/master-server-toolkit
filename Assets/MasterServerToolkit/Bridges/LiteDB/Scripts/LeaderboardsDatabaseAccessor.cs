#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logger = MasterServerToolkit.Logging.Logger;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public sealed class LeaderboardsDatabaseAccessor : ILeaderboardsDatabaseAccessor
    {
        private const string CollectionName = "leaderboard_entries";

        private readonly LiteDatabase database;
        private readonly ILiteCollection<LeaderboardEntryData> entriesCollection;
        private readonly SemaphoreSlim dbSemaphore = new SemaphoreSlim(1, 1);

        private bool isDisposed;

        public MstProperties CustomProperties { get; } = new MstProperties();
        public Logger Logger { get; set; }

        public LeaderboardsDatabaseAccessor(string databaseName = "leaderboards")
        {
            databaseName = string.IsNullOrWhiteSpace(databaseName)
                ? "leaderboards"
                : databaseName.Trim();

            database = new LiteDatabase($"{databaseName}.db");
            database.UtcDate = true;

            entriesCollection = database.GetCollection<LeaderboardEntryData>(CollectionName);
            entriesCollection.EnsureIndex(entry => entry.LeaderboardKey);
            entriesCollection.EnsureIndex(entry => entry.SeasonId);
            entriesCollection.EnsureIndex(entry => entry.AccountId);
            entriesCollection.EnsureIndex(entry => entry.Score);
        }

        public async Task<LeaderboardScoreUpdateResult> SubmitScoreAsync(
            LeaderboardScoreSubmission submission,
            CancellationToken cancellationToken = default)
        {
            ValidateSubmission(submission);
            ThrowIfDisposed();

            DateTime submittedAtUtc = ToUtc(submission.SubmittedAtUtc);

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();

                return ExecuteInTransaction(() => SubmitScore(submission, submittedAtUtc));
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<LeaderboardEntry> GetEntryAsync(
            string leaderboardKey,
            string seasonId,
            string accountId,
            CancellationToken cancellationToken = default)
        {
            leaderboardKey = NormalizeLeaderboardKey(leaderboardKey);
            seasonId = NormalizeSeasonId(seasonId);
            accountId = RequireValue(accountId, nameof(accountId));
            ThrowIfDisposed();

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();

                string id = LeaderboardEntryData.CreateId(leaderboardKey, seasonId, accountId);
                return entriesCollection.FindById(id)?.ToEntry();
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<IReadOnlyList<LeaderboardEntry>> GetEntriesAsync(
            string leaderboardKey,
            string seasonId,
            LeaderboardSortOrder sortOrder,
            int offset,
            int limit,
            CancellationToken cancellationToken = default)
        {
            leaderboardKey = NormalizeLeaderboardKey(leaderboardKey);
            seasonId = NormalizeSeasonId(seasonId);
            ValidateSortOrder(sortOrder);
            ThrowIfDisposed();

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset cannot be negative");

            if (limit <= 0)
                return Array.Empty<LeaderboardEntry>();

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();

                IEnumerable<LeaderboardEntryData> matchingEntries = entriesCollection.Find(entry =>
                    entry.LeaderboardKey == leaderboardKey &&
                    entry.SeasonId == seasonId);

                IOrderedEnumerable<LeaderboardEntryData> orderedEntries = sortOrder == LeaderboardSortOrder.Descending
                    ? matchingEntries
                        .OrderByDescending(entry => entry.Score)
                        .ThenBy(entry => entry.AccountId, StringComparer.Ordinal)
                    : matchingEntries
                        .OrderBy(entry => entry.Score)
                        .ThenBy(entry => entry.AccountId, StringComparer.Ordinal);

                List<LeaderboardEntry> result = orderedEntries
                    .Skip(offset)
                    .Take(limit)
                    .Select(entry => entry.ToEntry())
                    .ToList();

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<long> CountEntriesAsync(
            string leaderboardKey,
            string seasonId,
            CancellationToken cancellationToken = default)
        {
            leaderboardKey = NormalizeLeaderboardKey(leaderboardKey);
            seasonId = NormalizeSeasonId(seasonId);
            ThrowIfDisposed();

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();

                return entriesCollection.LongCount(entry =>
                    entry.LeaderboardKey == leaderboardKey &&
                    entry.SeasonId == seasonId);
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<long> CountBetterEntriesAsync(
            string leaderboardKey,
            string seasonId,
            LeaderboardSortOrder sortOrder,
            long score,
            string accountId,
            CancellationToken cancellationToken = default)
        {
            leaderboardKey = NormalizeLeaderboardKey(leaderboardKey);
            seasonId = NormalizeSeasonId(seasonId);
            accountId = RequireValue(accountId, nameof(accountId));
            ValidateSortOrder(sortOrder);
            ThrowIfDisposed();

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                ThrowIfDisposed();
                cancellationToken.ThrowIfCancellationRequested();

                IEnumerable<LeaderboardEntryData> matchingEntries = entriesCollection.Find(entry =>
                    entry.LeaderboardKey == leaderboardKey &&
                    entry.SeasonId == seasonId);

                long result = matchingEntries.LongCount(entry =>
                    IsRankedAhead(entry, sortOrder, score, accountId));

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public void Dispose()
        {
            dbSemaphore.Wait();

            try
            {
                if (isDisposed)
                    return;

                isDisposed = true;
                CustomProperties.Clear();
                database.Dispose();
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        private LeaderboardScoreUpdateResult SubmitScore(
            LeaderboardScoreSubmission submission,
            DateTime submittedAtUtc)
        {
            string id = LeaderboardEntryData.CreateId(
                submission.LeaderboardKey,
                submission.SeasonId,
                submission.AccountId);

            LeaderboardEntryData entry = entriesCollection.FindById(id);

            if (entry == null)
            {
                entry = LeaderboardEntryData.FromSubmission(submission, submittedAtUtc);
                entriesCollection.Upsert(entry);

                return new LeaderboardScoreUpdateResult
                {
                    Entry = entry.ToEntry(),
                    ScoreChanged = true
                };
            }

            bool acceptsScore = !submission.KeepBest ||
                                IsBetterScore(submission.Score, entry.Score, submission.SortOrder);
            bool scoreChanged = acceptsScore && entry.Score != submission.Score;
            bool playerNameChanged = !string.Equals(
                entry.PlayerName,
                submission.PlayerName,
                StringComparison.Ordinal);
            bool playerAvatarChanged = submission.PlayerAvatar != null && !string.Equals(
                entry.PlayerAvatar,
                submission.PlayerAvatar,
                StringComparison.Ordinal);

            if (acceptsScore)
                entry.Score = submission.Score;

            if (playerNameChanged)
                entry.PlayerName = submission.PlayerName;

            if (playerAvatarChanged)
                entry.PlayerAvatar = submission.PlayerAvatar;

            if (acceptsScore || playerNameChanged || playerAvatarChanged)
            {
                entry.UpdatedAtUtc = MaxUtc(entry.UpdatedAtUtc, submittedAtUtc);
                entriesCollection.Upsert(entry);
            }

            return new LeaderboardScoreUpdateResult
            {
                Entry = entry.ToEntry(),
                ScoreChanged = scoreChanged
            };
        }

        private T ExecuteInTransaction<T>(Func<T> action)
        {
            if (!database.BeginTrans())
                throw new InvalidOperationException("Failed to begin LiteDB leaderboard transaction");

            try
            {
                T result = action();

                if (!database.Commit())
                    throw new InvalidOperationException("Failed to commit LiteDB leaderboard transaction");

                return result;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        }

        private static bool IsBetterScore(
            long submittedScore,
            long storedScore,
            LeaderboardSortOrder sortOrder)
        {
            return sortOrder == LeaderboardSortOrder.Descending
                ? submittedScore > storedScore
                : submittedScore < storedScore;
        }

        private static bool IsRankedAhead(
            LeaderboardEntryData entry,
            LeaderboardSortOrder sortOrder,
            long score,
            string accountId)
        {
            if (entry.Score != score)
            {
                return sortOrder == LeaderboardSortOrder.Descending
                    ? entry.Score > score
                    : entry.Score < score;
            }

            return string.CompareOrdinal(entry.AccountId, accountId) < 0;
        }

        private static void ValidateSubmission(LeaderboardScoreSubmission submission)
        {
            if (submission == null)
                throw new ArgumentNullException(nameof(submission));

            submission.LeaderboardKey = NormalizeLeaderboardKey(submission.LeaderboardKey);
            submission.SeasonId = NormalizeSeasonId(submission.SeasonId);
            submission.AccountId = RequireValue(submission.AccountId, nameof(submission.AccountId));
            submission.PlayerName = submission.PlayerName?.Trim() ?? string.Empty;

            if (submission.PlayerName.Length > LeaderboardEntry.MaxPlayerNameLength)
            {
                submission.PlayerName = submission.PlayerName.Substring(
                    0,
                    LeaderboardEntry.MaxPlayerNameLength);
            }

            if (submission.PlayerAvatar != null)
                submission.PlayerAvatar = LeaderboardEntry.NormalizePlayerAvatar(submission.PlayerAvatar);

            ValidateSortOrder(submission.SortOrder);
        }

        private static void ValidateSortOrder(LeaderboardSortOrder sortOrder)
        {
            if (!Enum.IsDefined(typeof(LeaderboardSortOrder), sortOrder))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sortOrder),
                    sortOrder,
                    "Unsupported leaderboard sort order");
            }
        }

        private static string RequireValue(string value, string parameterName)
        {
            value = value?.Trim() ?? string.Empty;

            if (value.Length == 0)
                throw new ArgumentException("Value cannot be empty", parameterName);

            return value;
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
            string normalized = RequireValue(leaderboardKey, nameof(leaderboardKey));

            if (normalized.Length > LeaderboardDefinition.MaxKeyLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(leaderboardKey),
                    leaderboardKey,
                    $"Leaderboard key cannot exceed {LeaderboardDefinition.MaxKeyLength} characters");
            }

            return normalized;
        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value == default)
                return DateTime.UtcNow;

            if (value.Kind == DateTimeKind.Utc)
                return value;

            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static DateTime MaxUtc(DateTime current, DateTime candidate)
        {
            current = ToUtc(current);
            candidate = ToUtc(candidate);
            return current >= candidate ? current : candidate;
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(LeaderboardsDatabaseAccessor));
        }
    }
}

#endif
