using MasterServerToolkit.Extensions;

namespace MasterServerToolkit.MasterServer
{
    public partial struct ProfilePropertyOpCodes
    {
        public static ushort displayName = nameof(displayName).ToUint16Hash();
        public static ushort avatarUrl = nameof(avatarUrl).ToUint16Hash();
        public static ushort bronze = nameof(bronze).ToUint16Hash();
        public static ushort silver = nameof(silver).ToUint16Hash();
        public static ushort gold = nameof(gold).ToUint16Hash();
        public static ushort spice = nameof(spice).ToUint16Hash();
        public static ushort wood = nameof(wood).ToUint16Hash();
        public static ushort items = nameof(items).ToUint16Hash();
    }
}