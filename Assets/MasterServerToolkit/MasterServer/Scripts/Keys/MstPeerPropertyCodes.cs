using MasterServerToolkit.Extensions;

namespace MasterServerToolkit.MasterServer
{
    public struct MstPeerPropertyCodes
    {
        public static uint Start = nameof(Start).ToUint32Hash();

        public static uint RegisteredRooms = nameof(RegisteredRooms).ToUint32Hash();

        public static uint RegisteredSpawners = nameof(RegisteredSpawners).ToUint32Hash();
        public static uint ClientSpawnRequest = nameof(ClientSpawnRequest).ToUint32Hash();
    }
}