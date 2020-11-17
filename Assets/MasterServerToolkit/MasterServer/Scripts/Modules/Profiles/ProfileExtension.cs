using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ProfilePeerExtension : IPeerExtension
    {
        /// <summary>
        /// Username
        /// </summary>
        public string UserId { get; private set; }

        /// <summary>
        /// Profile data
        /// </summary>
        public ObservableServerProfile Profile { get; private set; }

        /// <summary>
        /// Peer asigned to this extension
        /// </summary>
        public IPeer Peer { get; private set; }

        public ProfilePeerExtension(ObservableServerProfile profile, IPeer peer)
        {
            UserId = profile.UserId;
            Profile = profile;
            Peer = peer;
        }
    }
}