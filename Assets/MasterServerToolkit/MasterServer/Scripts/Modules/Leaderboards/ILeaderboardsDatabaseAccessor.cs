using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Persistence contract owned by the MST leaderboards module.
    /// </summary>
    public interface ILeaderboardsDatabaseAccessor : IDatabaseAccessor
    {
        /// <summary>
        /// Atomically creates or updates one account entry according to the leaderboard policy.
        /// </summary>
        Task<LeaderboardScoreUpdateResult> SubmitScoreAsync(
            LeaderboardScoreSubmission submission,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets one account entry or <c>null</c> when it has not submitted a score.
        /// </summary>
        Task<LeaderboardEntry> GetEntryAsync(
            string leaderboardKey,
            string seasonId,
            string accountId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a sorted page of entries.
        /// </summary>
        Task<IReadOnlyList<LeaderboardEntry>> GetEntriesAsync(
            string leaderboardKey,
            string seasonId,
            LeaderboardSortOrder sortOrder,
            int offset,
            int limit,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts all entries in one leaderboard season.
        /// </summary>
        Task<long> CountEntriesAsync(
            string leaderboardKey,
            string seasonId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Counts entries ranked ahead of the supplied score. Account ID is used as a stable tie breaker.
        /// </summary>
        Task<long> CountBetterEntriesAsync(
            string leaderboardKey,
            string seasonId,
            LeaderboardSortOrder sortOrder,
            long score,
            string accountId,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Validated score submission passed to a persistence provider.
    /// </summary>
    public sealed class LeaderboardScoreSubmission
    {
        public string LeaderboardKey { get; set; }
        public string SeasonId { get; set; }
        public string AccountId { get; set; }
        public string PlayerName { get; set; }

        /// <summary>
        /// Gets or sets the trusted public avatar update. <c>null</c> preserves the stored value;
        /// an empty string clears it.
        /// </summary>
        public string PlayerAvatar { get; set; }
        public long Score { get; set; }
        public LeaderboardSortOrder SortOrder { get; set; }
        public bool KeepBest { get; set; }
        public System.DateTime SubmittedAtUtc { get; set; }
    }

    /// <summary>
    /// Result of one atomic score submission.
    /// </summary>
    public sealed class LeaderboardScoreUpdateResult
    {
        public LeaderboardEntry Entry { get; set; }
        public bool ScoreChanged { get; set; }
    }
}
