#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public sealed class LeaderboardEntryData
    {
        [BsonId]
        public string Id { get; set; }

        public string LeaderboardKey { get; set; }
        public string SeasonId { get; set; }
        public string AccountId { get; set; }
        public string PlayerName { get; set; }
        public string PlayerAvatar { get; set; }
        public long Score { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public LeaderboardEntry ToEntry()
        {
            var entry = new LeaderboardEntry
            {
                LeaderboardKey = LeaderboardKey ?? string.Empty,
                SeasonId = SeasonId ?? LeaderboardDefinition.AllTimeSeasonId,
                AccountId = AccountId ?? string.Empty,
                PlayerName = PlayerName ?? string.Empty,
                PlayerAvatar = PlayerAvatar ?? string.Empty,
                Score = Score,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = UpdatedAtUtc
            };

            entry.Normalize();
            return entry;
        }

        public static LeaderboardEntryData FromSubmission(
            LeaderboardScoreSubmission submission,
            DateTime submittedAtUtc)
        {
            return new LeaderboardEntryData
            {
                Id = CreateId(submission.LeaderboardKey, submission.SeasonId, submission.AccountId),
                LeaderboardKey = submission.LeaderboardKey,
                SeasonId = submission.SeasonId,
                AccountId = submission.AccountId,
                PlayerName = submission.PlayerName,
                PlayerAvatar = submission.PlayerAvatar ?? string.Empty,
                Score = submission.Score,
                CreatedAtUtc = submittedAtUtc,
                UpdatedAtUtc = submittedAtUtc
            };
        }

        public static string CreateId(string leaderboardKey, string seasonId, string accountId)
        {
            leaderboardKey ??= string.Empty;
            seasonId ??= string.Empty;
            accountId ??= string.Empty;

            return $"{leaderboardKey.Length}:{leaderboardKey}" +
                   $"{seasonId.Length}:{seasonId}" +
                   $"{accountId.Length}:{accountId}";
        }
    }
}

#endif
