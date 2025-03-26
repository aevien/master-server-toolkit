using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.GameService
{
    public class PurchasesInfo : SerializablePacket
    {
        public GameServiceId serviceId;
        public MstJson data;

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            serviceId = (GameServiceId)reader.ReadByte();
            data = reader.ReadJson();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write((byte)serviceId);
            writer.Write(data);
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField(nameof(serviceId), serviceId.ToString());
            json.AddField(nameof(data), data);

            return json;
        }
    }
}