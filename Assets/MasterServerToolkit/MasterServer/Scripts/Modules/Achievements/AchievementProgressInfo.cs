using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Globalization;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementProgressInfo : SerializablePacket
    {
        public string key;
        public int progress;
        public int required;
        public long unlockedAt;
        public bool rewardApplied;

        public bool IsUnlocked => progress >= required && unlockedAt > 0;

        /// <summary>
        /// Indicates that the unlock result hook completed successfully.
        /// </summary>
        public bool IsRewardApplied => IsUnlocked && rewardApplied;

        public AchievementProgressInfo() { }
        public AchievementProgressInfo(AchievementData data)
        {
            key = data.key;
            progress = 0;
            required = data.requiredProgress;
            unlockedAt = 0;
            rewardApplied = false;
        }

        public AchievementProgressInfo(AchievementProgressInfo source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            key = source.key;
            progress = source.progress;
            required = source.required;
            unlockedAt = source.unlockedAt;
            rewardApplied = source.rewardApplied;
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            key = reader.ReadString();
            progress = reader.ReadInt32();
            required = reader.ReadInt32();
            unlockedAt = reader.ReadInt64();
            rewardApplied = reader.ReadBoolean();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(key);
            writer.Write(progress);
            writer.Write(required);
            writer.Write(unlockedAt);
            writer.Write(rewardApplied);
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField("key", key);
            json.AddField("progress", progress);
            json.AddField("required", required);
            json.AddField("unlock_time", unlockedAt);
            json.AddField("reward_applied", rewardApplied);
            return json;
        }

        public override void FromJson(MstJson json)
        {
            key = json["key"].StringValue;
            progress = json["progress"].IntValue;
            required = json["required"].IntValue;

            MstJson unlockTime = json["unlock_time"];

            if (unlockTime.IsNumber)
            {
                unlockedAt = unlockTime.LongValue;
                rewardApplied = IsUnlocked &&
                    (!json.HasField("reward_applied") || json["reward_applied"].BoolValue);
                return;
            }

            ReadLegacyUnlockState(unlockTime.StringValue);
        }

        private void ReadLegacyUnlockState(string value)
        {
            DateTime legacyDate;
            bool parsed = DateTime.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out legacyDate);

            if (!parsed)
            {
                parsed = DateTime.TryParseExact(
                    value,
                    new[] { "dd.MM.yyyy H:mm:ss", "dd.MM.yyyy HH:mm:ss" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out legacyDate);
            }

            if (!parsed)
            {
                parsed = DateTime.TryParse(
                    value,
                    CultureInfo.CurrentCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out legacyDate);
            }

            if (!parsed)
            {
                parsed = DateTime.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out legacyDate);
            }

            if (!parsed)
                throw new FormatException($"Invalid legacy achievement unlock time: {value}");

            if (progress < required)
            {
                unlockedAt = 0;
                rewardApplied = false;
                return;
            }

            if (legacyDate.Year >= 9999)
            {
                unlockedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                rewardApplied = false;
                return;
            }

            if (legacyDate.Kind == DateTimeKind.Unspecified)
                legacyDate = DateTime.SpecifyKind(legacyDate, DateTimeKind.Utc);
            else
                legacyDate = legacyDate.ToUniversalTime();

            unlockedAt = new DateTimeOffset(legacyDate).ToUnixTimeMilliseconds();
            rewardApplied = true;
        }
    }
}
