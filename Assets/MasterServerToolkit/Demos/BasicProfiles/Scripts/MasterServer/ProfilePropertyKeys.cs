using MasterServerToolkit.Extensions;

namespace MasterServerToolkit.Examples.BasicProfile
{
    public struct ProfilePropertyKeys
    {
        public static ushort displayName = nameof(displayName).ToUint16Hash();
        public static ushort avatarUrl = nameof(avatarUrl).ToUint16Hash();
        public static ushort bronze = nameof(bronze).ToUint16Hash();
        public static ushort silver = nameof(silver).ToUint16Hash();
        public static ushort gold = nameof(gold).ToUint16Hash();
        public static ushort items = nameof(items).ToUint16Hash();
    }
}