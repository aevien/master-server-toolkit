#if MIRROR
using Mirror;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public struct ValidateRoomAccessRequestMessage : NetworkMessage
    {
        public string token;
        public string version;
    }
}
#endif