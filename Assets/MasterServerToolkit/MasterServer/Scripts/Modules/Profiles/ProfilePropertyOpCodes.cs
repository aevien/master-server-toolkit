using MasterServerToolkit.Extensions;

namespace MasterServerToolkit.MasterServer
{
    public partial struct ProfilePropertyOpCodes
    {
        public static ushort achievements = nameof(achievements).ToUint16Hash();
    }
}