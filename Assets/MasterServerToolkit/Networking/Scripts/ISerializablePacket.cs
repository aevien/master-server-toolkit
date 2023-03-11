
namespace MasterServerToolkit.Networking
{
    public interface ISerializablePacket
    {
        void ToBinaryWriter(EndianBinaryWriter writer);
        void FromBinaryReader(EndianBinaryReader reader);
        byte[] ToBytes();
        string ToBase64String();
    }
}