using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class AchievemetProgressData : SerializablePacket
    {
        public string id;
        public int currentValue;
        public int maxValue;
        public long endTime;

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            id = reader.ReadString();
            currentValue = reader.ReadInt32();
            maxValue = reader.ReadInt32();
            endTime = reader.ReadInt64();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(id);
            writer.Write(currentValue);
            writer.Write(maxValue);
            writer.Write(endTime);
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField("id", id);
            json.AddField("currentValue", currentValue);
            json.AddField("maxValue", maxValue);
            json.AddField("endTime", endTime);
            return json;
        }

        public override void FromJson(MstJson json)
        {
            id = json["id"].StringValue;
            currentValue = json["currentValue"].IntValue;
            maxValue = json["maxValue"].IntValue;
            endTime = json["endTime"].LongValue;
        }
    }
}