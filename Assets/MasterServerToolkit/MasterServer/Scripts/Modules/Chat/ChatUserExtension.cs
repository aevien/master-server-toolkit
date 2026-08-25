using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ChatUserPeerExtension : IPeerExtension
    {
        private readonly object syncRoot = new object();

        /// <summary>
        /// List of the channels this user belongs to
        /// </summary>
        public HashSet<ChatChannel> CurrentChannels { get; private set; }

        internal object SyncRoot => syncRoot;

        /// <summary>
        /// User default channel
        /// </summary>
        public ChatChannel DefaultChannel
        {
            get
            {
                lock (syncRoot)
                {
                    return defaultChannel;
                }
            }
            set
            {
                lock (syncRoot)
                {
                    defaultChannel = value;
                }
            }
        }

        private ChatChannel defaultChannel;

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

        public List<ChatChannel> GetChannelsSnapshot()
        {
            lock (syncRoot)
            {
                return new List<ChatChannel>(CurrentChannels);
            }
        }

        public bool ContainsChannel(ChatChannel channel)
        {
            if (channel == null)
                return false;

            lock (syncRoot)
            {
                return CurrentChannels.Contains(channel);
            }
        }

        public int ChannelsCount
        {
            get
            {
                lock (syncRoot)
                {
                    return CurrentChannels.Count;
                }
            }
        }

        public ChatChannel FirstChannelOrDefault()
        {
            lock (syncRoot)
            {
                foreach (var channel in CurrentChannels)
                {
                    return channel;
                }
            }

            return null;
        }

        internal bool AddChannel(ChatChannel channel)
        {
            if (channel == null)
                return false;

            lock (syncRoot)
            {
                return CurrentChannels.Add(channel);
            }
        }

        internal bool RemoveChannel(ChatChannel channel)
        {
            if (channel == null)
                return false;

            lock (syncRoot)
            {
                return CurrentChannels.Remove(channel);
            }
        }
    }
}
