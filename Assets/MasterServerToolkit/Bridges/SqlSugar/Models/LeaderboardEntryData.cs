using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.LeaderboardEntries)]
    [SugarIndex("ix_leaderboard_entries_score_asc",
        nameof(LeaderboardKey), OrderByType.Asc,
        nameof(SeasonId), OrderByType.Asc,
        nameof(Score), OrderByType.Asc,
        nameof(AccountId), OrderByType.Asc, false)]
    [SugarIndex("ix_leaderboard_entries_score_desc",
        nameof(LeaderboardKey), OrderByType.Asc,
        nameof(SeasonId), OrderByType.Asc,
        nameof(Score), OrderByType.Desc,
        nameof(AccountId), OrderByType.Asc, false)]
    public sealed class LeaderboardEntryData
    {
        public const int MaxLeaderboardKeyLength = LeaderboardDefinition.MaxKeyLength;
        public const int MaxSeasonIdLength = LeaderboardDefinition.MaxSeasonIdLength;
        public const int MaxAccountIdLength = 38;

        [SugarColumn(ColumnName = "leaderboard_key", Length = MaxLeaderboardKeyLength,
            IsPrimaryKey = true, IsNullable = false)]
        public string LeaderboardKey { get; set; }

        [SugarColumn(ColumnName = "season_id", Length = MaxSeasonIdLength,
            IsPrimaryKey = true, IsNullable = false)]
        public string SeasonId { get; set; }

        [SugarColumn(ColumnName = "account_id", Length = MaxAccountIdLength,
            IsPrimaryKey = true, IsNullable = false)]
        public string AccountId { get; set; }

        [SugarColumn(ColumnName = "player_name", Length = LeaderboardEntry.MaxPlayerNameLength,
            IsNullable = false)]
        public string PlayerName { get; set; }

        [SugarColumn(ColumnName = "player_avatar", Length = LeaderboardEntry.MaxPlayerAvatarLength,
            IsNullable = false)]
        public string PlayerAvatar { get; set; }

        [SugarColumn(ColumnName = "score", ColumnDataType = "bigint", IsNullable = false)]
        public long Score { get; set; }

        [SugarColumn(ColumnName = "created_at", IsNullable = false)]
        public DateTime CreatedAtUtc { get; set; }

        [SugarColumn(ColumnName = "updated_at", IsNullable = false)]
        public DateTime UpdatedAtUtc { get; set; }

        public LeaderboardEntry ToEntry()
        {
            return new LeaderboardEntry
            {
                LeaderboardKey = LeaderboardKey ?? string.Empty,
                SeasonId = SeasonId ?? LeaderboardDefinition.AllTimeSeasonId,
                AccountId = AccountId ?? string.Empty,
                PlayerName = PlayerName ?? string.Empty,
                PlayerAvatar = PlayerAvatar ?? string.Empty,
                Score = Score,
                CreatedAtUtc = AsUtc(CreatedAtUtc),
                UpdatedAtUtc = AsUtc(UpdatedAtUtc)
            };
        }

        private static DateTime AsUtc(DateTime value)
        {
            if (value == default)
                return value;

            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
