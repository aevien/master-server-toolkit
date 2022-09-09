using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class LobbyUserPeerExtension : IPeerExtension
    {
        public IPeer Peer { get; private set; }

        /// <summary>
        /// Lobby, to which current peer belongs
        /// </summary>
        public ILobby CurrentLobby { get; set; }

        public LobbyUserPeerExtension(IPeer peer)
        {
            Peer = peer;
        }
    }
}