using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ChatChannel
    {
        private Dictionary<string, ChatUserPeerExtension> _users;

        /// <summary>
        /// Name of the channel
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Users connected to chis channel
        /// </summary>
        public IEnumerable<ChatUserPeerExtension> Users { get { return _users.Values; } }

        public ChatChannel(string name)
        {
            Name = name;
            _users = new Dictionary<string, ChatUserPeerExtension>();
        }

        /// <summary>
        /// Returns true, if user successfully joined a channel
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool AddUser(ChatUserPeerExtension user)
        {
            if (!IsUserAllowed(user))
            {
                return false;
            }

            // Add disconnect listener
            user.Peer.OnPeerDisconnectedEvent += OnUserDisconnect;

            // Add user
            _users.Add(user.Username, user);

            // Add channel to users collection
            user.CurrentChannels.Add(this);

            OnJoined(user);
            return true;
        }

        protected virtual void OnJoined(ChatUserPeerExtension newUser)
        {
            var data = new List<string>() { Name, newUser.Username };
            var msg = Mst.Create.Message((short)MstMessageCodes.UserJoinedChannel, data.ToBytes());

            foreach (var user in _users.Values)
            {
                if (user != newUser)
                {
                    user.Peer.SendMessage(msg, DeliveryMethod.Reliable);
                }
            }
        }

        protected virtual void OnLeft(ChatUserPeerExtension removedUser)
        {
            var data = new List<string>() { Name, removedUser.Username };
            var msg = Mst.Create.Message((short)MstMessageCodes.UserLeftChannel, data.ToBytes());

            foreach (var user in _users.Values)
            {
                if (user != removedUser)
                {
                    user.Peer.SendMessage(msg, DeliveryMethod.Reliable);
                }
            }
        }

        protected virtual bool IsUserAllowed(ChatUserPeerExtension user)
        {
            // Can't join if already here
            return !_users.ContainsKey(user.Username);
        }

        /// <summary>
        /// Invoked, when user, who is connected to this channel, leaves
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnUserDisconnect(IPeer peer)
        {
            var extension = peer.GetExtension<ChatUserPeerExtension>();

            if (extension == null)
            {
                return;
            }

            RemoveUser(extension);
        }

        public void RemoveUser(ChatUserPeerExtension user)
        {
            // Remove disconnect listener
            user.Peer.OnPeerDisconnectedEvent -= OnUserDisconnect;

            // Remove channel from users collection
            user.CurrentChannels.Remove(this);

            // Remove user
            _users.Remove(user.Username);

            if (user.DefaultChannel == this)
            {
                user.DefaultChannel = null;
            }

            OnLeft(user);
        }

        /// <summary>
        /// Handle messages
        /// </summary>
        public virtual void BroadcastMessage(ChatMessagePacket packet)
        {
            // Override name to be in a "standard" format (uppercase letters and etc.)
            packet.Receiver = Name;

            var msg = Mst.Create.Message((short)MstMessageCodes.ChatMessage, packet);

            foreach (var user in _users.Values)
            {
                user.Peer.SendMessage(msg, DeliveryMethod.Reliable);
            }
        }
    }
}