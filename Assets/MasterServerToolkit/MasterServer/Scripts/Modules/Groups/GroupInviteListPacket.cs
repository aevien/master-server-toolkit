using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class GroupInviteListPacket : BaseListPacket<GroupInvitePacket>
    {
        protected override GroupInvitePacket ReadItem(EndianBinaryReader reader)
        {
            var item = new GroupInvitePacket();
            item.FromBinaryReader(reader);
            return item;
        }

        protected override void WriteItem(GroupInvitePacket item, EndianBinaryWriter writer)
        {
            item.ToBinaryWriter(writer);
        }
    }
}
