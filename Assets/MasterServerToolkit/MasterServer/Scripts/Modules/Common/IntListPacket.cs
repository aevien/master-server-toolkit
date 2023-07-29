using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class IntListPacket : BaseListPacket<int>
    {
        protected override int ReadItem(EndianBinaryReader reader)
        {
            return reader.ReadInt32();
        }

        protected override void WriteItem(int item, EndianBinaryWriter writer)
        {
            writer.Write(item);
        }
    }
}
