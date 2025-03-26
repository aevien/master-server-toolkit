using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class AnalyticsDataInfoPacket : SerializablePacket, IAnalyticsInfoData
    {
        public string Id { get; set; }
        public string UserId { get; set; }
        public string Key { get; set; }
        public string Category { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Data { get; set; }
        public bool IsSessionEvent { get; set; } = false;

        public AnalyticsDataInfoPacket()
        {
            Id = Guid.NewGuid().ToString();
            Timestamp = DateTime.UtcNow;
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            UserId = reader.ReadString();
            Key = reader.ReadString();
            Category = reader.ReadString();
            Timestamp = reader.ReadDateTime();
            Data = reader.ReadDictionary();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(UserId);
            writer.Write(Key);
            writer.Write(Category);
            writer.Write(Timestamp);
            writer.Write(Data);
        }

        public override void FromJson(MstJson json)
        {
            base.FromJson(json);
            Id = json["id"].StringValue;
            UserId = json["user_id"].StringValue;
            Key = json["key"].StringValue;
            Category = json["category"].StringValue;
            Timestamp = DateTime.Parse(json["timestamp"].StringValue);
            Data = json["data"].ToDictionary();
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField("id", Id);
            json.AddField("user_id", UserId);
            json.AddField("key", Key);
            json.AddField("category", Category);
            json.AddField("timestamp", Timestamp.ToString());
            json.AddField("data", MstJson.Create(Data));
            return base.ToJson();
        }
    }
}