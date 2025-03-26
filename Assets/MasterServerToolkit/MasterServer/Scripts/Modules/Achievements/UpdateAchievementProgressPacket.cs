using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class UpdateAchievementProgressPacket : SerializablePacket
    {
        public string key;
        public string userId;
        public int progress;

        public UpdateAchievementProgressPacket()
        {
            userId = string.Empty;
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            key = reader.ReadString();
            userId = reader.ReadString();
            progress = reader.ReadInt32();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(key);
            writer.Write(userId);
            writer.Write(progress);
        }
    }
}