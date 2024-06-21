using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class UpdateAchievementProgressPacket : SerializablePacket
    {
        public string id;
        public string username;
        public int value;

        public UpdateAchievementProgressPacket()
        {
            username = string.Empty;
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            id = reader.ReadString();
            username = reader.ReadString();
            value = reader.ReadInt32();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(id);
            writer.Write(username);
            writer.Write(value);
        }
    }
}