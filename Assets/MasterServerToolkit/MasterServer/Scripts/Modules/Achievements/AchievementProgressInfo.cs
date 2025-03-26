using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class AchievementProgressInfo : SerializablePacket
    {
        public string key;
        public int progress;
        public int required;
        public DateTime unlockDate;

        public bool IsUnlocked => progress >= required;

        public AchievementProgressInfo() { }
        public AchievementProgressInfo(AchievementData data)
        {
            key = data.Key;
            progress = 0;
            required = data.RequiredProgress;
            unlockDate = DateTime.MaxValue;
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            key = reader.ReadString();
            progress = reader.ReadInt32();
            required = reader.ReadInt32();
            unlockDate = reader.ReadDateTime();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(key);
            writer.Write(progress);
            writer.Write(required);
            writer.Write(unlockDate);
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField("key", key);
            json.AddField("progress", progress);
            json.AddField("required", required);
            json.AddField("unlock_time", unlockDate);
            return json;
        }

        public override void FromJson(MstJson json)
        {
            key = json["key"].StringValue;
            progress = json["progress"].IntValue;
            required = json["required"].IntValue;
            unlockDate = DateTime.Parse(json["unlock_time"].StringValue);
        }
    }
}