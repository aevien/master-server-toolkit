using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// One persisted and transferable leaderboard entry.
    /// </summary>
    public sealed class LeaderboardEntry : SerializablePacket
    {
        public const int MaxPlayerNameLength = 64;
        public const int MaxPlayerAvatarLength = 512;

        public string LeaderboardKey { get; set; } = string.Empty;
        public string SeasonId { get; set; } = LeaderboardDefinition.AllTimeSeasonId;
        public string AccountId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the public HTTPS avatar URL stored as display-only leaderboard metadata.
        /// </summary>
        public string PlayerAvatar { get; set; } = string.Empty;
        public long Score { get; set; }
        public long Rank { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            Normalize();
            writer.Write(LeaderboardKey);
            writer.Write(SeasonId);
            writer.Write(AccountId);
            writer.Write(PlayerName);
            writer.Write(PlayerAvatar);
            writer.Write(Score);
            writer.Write(Rank);
            writer.Write(CreatedAtUtc);
            writer.Write(UpdatedAtUtc);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            LeaderboardKey = reader.ReadString();
            SeasonId = reader.ReadString();
            AccountId = reader.ReadString();
            PlayerName = reader.ReadString();
            PlayerAvatar = reader.ReadString();
            Score = reader.ReadInt64();
            Rank = reader.ReadInt64();
            CreatedAtUtc = reader.ReadDateTime();
            UpdatedAtUtc = reader.ReadDateTime();
            Normalize();
        }

        public override MstJson ToJson()
        {
            MstJson json = base.ToJson();
            json.AddField("leaderboardKey", LeaderboardKey);
            json.AddField("seasonId", SeasonId);
            json.AddField("accountId", AccountId);
            json.AddField("playerName", PlayerName);
            json.AddField("playerAvatar", PlayerAvatar);
            json.AddField("score", Score);
            json.AddField("rank", Rank);
            json.AddField("createdAt", CreatedAtUtc);
            json.AddField("updatedAt", UpdatedAtUtc);
            return json;
        }

        /// <summary>
        /// Creates a detached copy safe to return from a database accessor.
        /// </summary>
        public LeaderboardEntry Clone()
        {
            return new LeaderboardEntry
            {
                LeaderboardKey = LeaderboardKey,
                SeasonId = SeasonId,
                AccountId = AccountId,
                PlayerName = PlayerName,
                PlayerAvatar = PlayerAvatar,
                Score = Score,
                Rank = Rank,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = UpdatedAtUtc
            };
        }

        /// <summary>
        /// Normalizes nullable strings and timestamps at the persistence boundary.
        /// </summary>
        public void Normalize()
        {
            LeaderboardKey = LeaderboardKey?.Trim() ?? string.Empty;
            SeasonId = string.IsNullOrWhiteSpace(SeasonId)
                ? LeaderboardDefinition.AllTimeSeasonId
                : SeasonId.Trim();
            AccountId = AccountId?.Trim() ?? string.Empty;
            PlayerName = PlayerName?.Trim() ?? string.Empty;
            PlayerAvatar = NormalizePlayerAvatar(PlayerAvatar);

            if (PlayerName.Length > MaxPlayerNameLength)
                PlayerName = PlayerName.Substring(0, MaxPlayerNameLength);

            CreatedAtUtc = NormalizeUtc(CreatedAtUtc);
            UpdatedAtUtc = NormalizeUtc(UpdatedAtUtc);
        }

        /// <summary>
        /// Returns a public HTTPS avatar URL that is safe to transfer to other clients.
        /// Unsupported, malformed and overlong values are normalized to an empty string.
        /// </summary>
        public static string NormalizePlayerAvatar(string value)
        {
            string normalized = value?.Trim() ?? string.Empty;

            if (normalized.Length == 0 || normalized.Length > MaxPlayerAvatarLength)
                return string.Empty;

            if (!Uri.TryCreate(normalized, UriKind.Absolute, out Uri uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                return string.Empty;
            }

            return normalized;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default)
                return DateTime.UtcNow;

            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }
    }
}
