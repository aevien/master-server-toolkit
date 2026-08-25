using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public sealed class LeaderboardDefinitionPacket : SerializablePacket
    {
        public string Key { get; set; } = string.Empty;
        public Dictionary<string, string> Title { get; set; } = new Dictionary<string, string>();
        public string SeasonId { get; set; } = LeaderboardDefinition.AllTimeSeasonId;
        public LeaderboardSortOrder SortOrder { get; set; }
        public bool KeepBest { get; set; }
        public int DecimalPlaces { get; set; }
        public long MinimumScore { get; set; }
        public long MaximumScore { get; set; }
        public bool ServerOnly { get; set; }
        public bool AllowGuests { get; set; }
        public DateTime? StartsAtUtc { get; set; }
        public DateTime? EndsAtUtc { get; set; }
        public LeaderboardAvailability Availability { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Key ?? string.Empty);
            writer.Write(Title ?? new Dictionary<string, string>());
            writer.Write(SeasonId ?? LeaderboardDefinition.AllTimeSeasonId);
            writer.Write((byte)SortOrder);
            writer.Write(KeepBest);
            writer.Write(DecimalPlaces);
            writer.Write(MinimumScore);
            writer.Write(MaximumScore);
            writer.Write(ServerOnly);
            writer.Write(AllowGuests);
            WriteOptionalDate(writer, StartsAtUtc);
            WriteOptionalDate(writer, EndsAtUtc);
            writer.Write((byte)Availability);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Key = reader.ReadString();
            Title = reader.ReadDictionary();
            SeasonId = reader.ReadString();
            SortOrder = (LeaderboardSortOrder)reader.ReadByte();
            KeepBest = reader.ReadBoolean();
            DecimalPlaces = reader.ReadInt32();
            MinimumScore = reader.ReadInt64();
            MaximumScore = reader.ReadInt64();
            ServerOnly = reader.ReadBoolean();
            AllowGuests = reader.ReadBoolean();
            StartsAtUtc = ReadOptionalDate(reader);
            EndsAtUtc = ReadOptionalDate(reader);
            Availability = (LeaderboardAvailability)reader.ReadByte();
        }

        public static LeaderboardDefinitionPacket FromDefinition(
            LeaderboardDefinition definition, DateTime utcNow)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            definition.TryGetPeriod(out DateTime? startsAtUtc, out DateTime? endsAtUtc, out _);

            return new LeaderboardDefinitionPacket
            {
                Key = definition.Key,
                Title = definition.GetTitles(),
                SeasonId = definition.SeasonId,
                SortOrder = definition.SortOrder,
                KeepBest = definition.KeepBest,
                DecimalPlaces = definition.DecimalPlaces,
                MinimumScore = definition.MinimumScore,
                MaximumScore = definition.MaximumScore,
                ServerOnly = definition.ServerOnly,
                AllowGuests = definition.AllowGuests,
                StartsAtUtc = startsAtUtc,
                EndsAtUtc = endsAtUtc,
                Availability = definition.GetAvailability(utcNow)
            };
        }

        private static void WriteOptionalDate(EndianBinaryWriter writer, DateTime? value)
        {
            writer.Write(value.HasValue);

            if (value.HasValue)
                writer.Write(value.Value);
        }

        private static DateTime? ReadOptionalDate(EndianBinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadDateTime() : null;
        }
    }

    public sealed class LeaderboardDefinitionsPacket : BaseListPacket<LeaderboardDefinitionPacket>
    {
        protected override void WriteItem(LeaderboardDefinitionPacket item, EndianBinaryWriter writer)
        {
            (item ?? new LeaderboardDefinitionPacket()).ToBinaryWriter(writer);
        }

        protected override LeaderboardDefinitionPacket ReadItem(EndianBinaryReader reader)
        {
            var item = new LeaderboardDefinitionPacket();
            item.FromBinaryReader(reader);
            return item;
        }
    }

    public sealed class LeaderboardEntryListPacket : BaseListPacket<LeaderboardEntry>
    {
        protected override void WriteItem(LeaderboardEntry item, EndianBinaryWriter writer)
        {
            (item ?? new LeaderboardEntry()).ToBinaryWriter(writer);
        }

        protected override LeaderboardEntry ReadItem(EndianBinaryReader reader)
        {
            var item = new LeaderboardEntry();
            item.FromBinaryReader(reader);
            return item;
        }
    }

    public sealed class LeaderboardEntriesRequestPacket : SerializablePacket
    {
        public string Key { get; set; } = string.Empty;
        public string SeasonId { get; set; } = string.Empty;
        public int Offset { get; set; }
        public int Limit { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Key ?? string.Empty);
            writer.Write(SeasonId ?? string.Empty);
            writer.Write(Offset);
            writer.Write(Limit);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Key = reader.ReadString();
            SeasonId = reader.ReadString();
            Offset = reader.ReadInt32();
            Limit = reader.ReadInt32();
        }
    }

    public sealed class LeaderboardAroundPlayerRequestPacket : SerializablePacket
    {
        public string Key { get; set; } = string.Empty;
        public string SeasonId { get; set; } = string.Empty;
        public int Limit { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Key ?? string.Empty);
            writer.Write(SeasonId ?? string.Empty);
            writer.Write(Limit);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Key = reader.ReadString();
            SeasonId = reader.ReadString();
            Limit = reader.ReadInt32();
        }
    }

    public sealed class LeaderboardEntryRequestPacket : SerializablePacket
    {
        public string Key { get; set; } = string.Empty;
        public string SeasonId { get; set; } = string.Empty;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Key ?? string.Empty);
            writer.Write(SeasonId ?? string.Empty);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Key = reader.ReadString();
            SeasonId = reader.ReadString();
        }
    }

    public sealed class LeaderboardSubmitScorePacket : SerializablePacket
    {
        public string Key { get; set; } = string.Empty;
        public string SeasonId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string PlayerAvatar { get; set; } = string.Empty;
        public long Score { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Key ?? string.Empty);
            writer.Write(SeasonId ?? string.Empty);
            writer.Write(AccountId ?? string.Empty);
            writer.Write(PlayerName ?? string.Empty);
            writer.Write(PlayerAvatar ?? string.Empty);
            writer.Write(Score);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Key = reader.ReadString();
            SeasonId = reader.ReadString();
            AccountId = reader.ReadString();
            PlayerName = reader.ReadString();
            PlayerAvatar = reader.ReadString();
            Score = reader.ReadInt64();
        }
    }

    public sealed class LeaderboardEntriesPacket : SerializablePacket
    {
        public LeaderboardDefinitionPacket Definition { get; set; } = new LeaderboardDefinitionPacket();
        public List<LeaderboardEntry> Entries { get; set; } = new List<LeaderboardEntry>();
        public long TotalEntries { get; set; }
        public long CurrentPlayerRank { get; set; }
        public int Offset { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            (Definition ?? new LeaderboardDefinitionPacket()).ToBinaryWriter(writer);
            new LeaderboardEntryListPacket { Items = Entries ?? new List<LeaderboardEntry>() }
                .ToBinaryWriter(writer);
            writer.Write(TotalEntries);
            writer.Write(CurrentPlayerRank);
            writer.Write(Offset);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Definition = new LeaderboardDefinitionPacket();
            Definition.FromBinaryReader(reader);

            var entries = new LeaderboardEntryListPacket();
            entries.FromBinaryReader(reader);
            Entries = entries.Items;
            TotalEntries = reader.ReadInt64();
            CurrentPlayerRank = reader.ReadInt64();
            Offset = reader.ReadInt32();
        }
    }

    public sealed class LeaderboardSubmitResultPacket : SerializablePacket
    {
        public LeaderboardEntry Entry { get; set; } = new LeaderboardEntry();
        public bool ScoreChanged { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            (Entry ?? new LeaderboardEntry()).ToBinaryWriter(writer);
            writer.Write(ScoreChanged);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Entry = new LeaderboardEntry();
            Entry.FromBinaryReader(reader);
            ScoreChanged = reader.ReadBoolean();
        }
    }
}
