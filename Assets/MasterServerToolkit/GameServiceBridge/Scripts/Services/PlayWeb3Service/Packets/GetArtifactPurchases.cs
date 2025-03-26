using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.GameService
{
    public class GetArtifactPurchases : SerializablePacket
    {
        public ushort skip = 0;
        public ushort limit = 0;
        public string wallet = string.Empty;

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            skip = reader.ReadUInt16();
            limit = reader.ReadUInt16();
            wallet = reader.ReadString();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(skip);
            writer.Write(limit);
            writer.Write(wallet);
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField(nameof(skip), skip);
            json.AddField(nameof(limit), limit);
            json.AddField(nameof(wallet), wallet);

            return json;
        }
    }
}