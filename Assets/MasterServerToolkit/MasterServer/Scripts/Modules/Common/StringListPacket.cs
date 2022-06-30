using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class StringListPacket : BaseListPacket<string>
    {
        protected override string ReadItem(EndianBinaryReader reader)
        {
            return reader.ReadString();
        }

        protected override void WriteItem(string item, EndianBinaryWriter writer)
        {
            writer.Write(item);
        }
    }
}
