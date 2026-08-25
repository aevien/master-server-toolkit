#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using MasterServerToolkit.MasterServer;
using MongoDB.Bson.Serialization.Attributes;
using System;

namespace MasterServerToolkit.Bridges.MongoDB
{
    internal sealed class LeaderboardEntryMongoDB
    {
        [BsonId]
        public string Id { get; set; }

        public string LeaderboardKey { get; set; }
        public string SeasonId { get; set; }
        public string AccountId { get; set; }
        public string PlayerName { get; set; }
        public string PlayerAvatar { get; set; }
        public long Score { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime CreatedAtUtc { get; set; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
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

        public static LeaderboardEntryMongoDB FromSubmission(
            LeaderboardScoreSubmission submission,
            string id,
            DateTime createdAtUtc)
        {
            if (submission == null)
                throw new ArgumentNullException(nameof(submission));

            return new LeaderboardEntryMongoDB
            {
                Id = id,
                LeaderboardKey = submission.LeaderboardKey,
                SeasonId = submission.SeasonId,
                AccountId = submission.AccountId,
                PlayerName = submission.PlayerName,
                PlayerAvatar = submission.PlayerAvatar ?? string.Empty,
                Score = submission.Score,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = submission.SubmittedAtUtc
            };
        }
    }
}

#endif
