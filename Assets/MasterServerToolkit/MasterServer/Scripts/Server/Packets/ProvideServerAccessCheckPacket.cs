using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ProvideServerAccessCheckPacket : SerializablePacket
    {
        public string DeviceId { get; set; } = string.Empty;
        public string ApplicationKey { get; set; } = string.Empty;
        public MstProperties CustomOptions { get; set; } = new MstProperties();

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(DeviceId);
            writer.Write(ApplicationKey);
            writer.Write(CustomOptions.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            DeviceId = reader.ReadString();
            ApplicationKey = reader.ReadString();
            CustomOptions = new MstProperties(reader.ReadDictionary());
        }
    }
}