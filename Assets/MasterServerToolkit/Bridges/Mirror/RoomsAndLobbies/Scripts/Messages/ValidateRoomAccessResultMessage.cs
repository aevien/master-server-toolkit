#if MIRROR
using MasterServerToolkit.Networking;
using Mirror;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public struct ValidateRoomAccessResultMessage : NetworkMessage
    {
        public byte[] error;
        public ResponseStatus status;
    }
}
#endif
