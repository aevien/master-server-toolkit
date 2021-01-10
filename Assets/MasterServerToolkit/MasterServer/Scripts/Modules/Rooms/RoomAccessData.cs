using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class RoomAccessData
    {
        public RoomAccessPacket Access { get; set; }
        public IPeer Peer { get; set; }
        public DateTime Timeout { get; set; }
    }
}