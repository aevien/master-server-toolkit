#if FISHNET
using FishNet.Broadcast;

namespace MasterServerToolkit.Bridges.FishNetworking
{
    public struct ValidateRoomAccessRequestMessage : IBroadcast
    {
        public string Token;
    }

}
#endif