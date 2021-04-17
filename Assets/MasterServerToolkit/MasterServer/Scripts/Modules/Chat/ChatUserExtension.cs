using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ChatUserPeerExtension : IPeerExtension
    {
        /// <summary>
        /// List of the channels this user belongs to
        /// </summary>
        public HashSet<ChatChannel> CurrentChannels { get; private set; }

        /// <summary>
        /// User default channel
        /// </summary>
        public ChatChannel DefaultChannel { get; set; }

        /// <summary>
        /// Username in channels
        /// </summary>
        public string Username { get; private set; }

        public IPeer Peer { get; private set; }

        public ChatUserPeerExtension(IPeer peer, string username)
        {
            Peer = peer;
            Username = username;
            CurrentChannels = new HashSet<ChatChannel>();
        }
    }
}