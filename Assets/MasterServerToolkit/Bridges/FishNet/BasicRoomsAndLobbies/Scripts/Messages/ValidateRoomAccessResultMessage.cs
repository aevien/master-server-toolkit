#if FISHNET
using FishNet.Broadcast;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.Bridges.FishNetworking
{
    public struct ValidateRoomAccessResultMessage : IBroadcast
    {
        public string Error;
        public ResponseStatus Status;
    }

}
#endif