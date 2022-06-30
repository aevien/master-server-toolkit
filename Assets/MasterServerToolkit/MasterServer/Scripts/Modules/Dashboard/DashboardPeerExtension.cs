using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class DashboardPeerExtension : IPeerExtension
    {
        public string SourceId { get; private set; }
        public IPeer Peer { get; private set; }

        public DashboardPeerExtension(string sourecId, IPeer peer)
        {
            SourceId = sourecId ?? throw new ArgumentNullException(nameof(sourecId));
            Peer = peer ?? throw new ArgumentNullException(nameof(peer));
        }
    }
}
