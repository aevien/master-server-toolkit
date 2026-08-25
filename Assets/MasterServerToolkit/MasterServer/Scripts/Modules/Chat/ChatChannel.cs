using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ChatChannel
    {
        private readonly object syncRoot;
        private readonly Dictionary<string, ChatUserPeerExtension> channelUsers = new Dictionary<string, ChatUserPeerExtension>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ChatChannelPermission> userPermissions = new Dictionary<string, ChatChannelPermission>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ChatChannelPermission> invitedUsers = new Dictionary<string, ChatChannelPermission>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> bannedUsers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Name of the channel
        /// </summary>
        public string Name { get; private set; }

        public ChatChannelOptions Options { get; private set; }

        public string Id
        {
            get
            {
                lock (syncRoot)
                {
                    return Options.Id;
                }
            }
        }

        public ChatChannelType Type
        {
            get
            {
                lock (syncRoot)
                {
                    return Options.Type;
                }
            }
        }

        public bool IsClosed
        {
            get
            {
                lock (syncRoot)
                {
                    return Options.RequiresMembership();
                }
            }
        }

        public bool IsArchived
        {
            get
            {
                lock (syncRoot)
                {
                    return Options.IsArchived;
                }
            }
            set
            {
                lock (syncRoot)
                {
                    Options.IsArchived = value;
                }
            }
        }

        public string OwnerUsername
        {
            get
            {
                lock (syncRoot)
                {
                    return Options.OwnerUsername;
                }
            }
        }

        public ChatChannelPermission OwnerPermissions
        {
            get
            {
                lock (syncRoot)
                {
                    return Options.OwnerPermissions;
                }
            }
        }

        public List<string> GetInvitedUsersSnapshot()
        {
            lock (syncRoot)
            {
                return new List<string>(invitedUsers.Keys);
            }
        }

        public List<string> GetBannedUsersSnapshot()
        {
            lock (syncRoot)
            {
                return new List<string>(bannedUsers);
            }
        }

        public ChatChannel(string name) : this(name, new ChatChannelOptions(), null)
        {
        }

        public ChatChannel(string name, ChatChannelOptions options) : this(name, options, null)
        {
        }

        internal ChatChannel(string name, ChatChannelOptions options, object syncRoot)
        {
            this.syncRoot = syncRoot ?? new object();
            Name = name;
            Options = options ?? new ChatChannelOptions();

            if (string.IsNullOrWhiteSpace(Options.Id))
                Options.Id = Name;

            if (!string.IsNullOrWhiteSpace(Options.OwnerUsername))
                SetPermissions(Options.OwnerUsername, Options.OwnerPermissions);
        }

        /// <summary>
        /// Returns true, if user successfully joined a channel
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        public bool AddUser(ChatUserPeerExtension user)
        {
            string username = NormalizeUsername(user?.Username);

            if (string.IsNullOrWhiteSpace(username))
                return false;

            lock (syncRoot)
            {
                if (!IsUserAllowedLocked(username))
                {
                    return false;
                }

                if (user.Peer.GetExtension<ChatUserPeerExtension>() != user)
                    return false;

                // Add disconnect listener
                user.Peer.OnConnectionCloseEvent += OnUserDisconnect;

                // Add user
                channelUsers.Add(username, user);

                if (!userPermissions.ContainsKey(username))
                {
                    if (invitedUsers.TryGetValue(username, out ChatChannelPermission invitePermissions))
                        userPermissions[username] = invitePermissions;
                    else
                        userPermissions[username] = Options.DefaultMemberPermissions;
                }

                invitedUsers.Remove(username);

                // Add channel to users collection
                user.AddChannel(this);
            }

            // Notify about new user
            NotifyOnJoined(user);

            return true;
        }

        /// <summary>
        /// Notify all users about new user who joined this channel
        /// </summary>
        /// <param name="newUser"></param>
        protected virtual void NotifyOnJoined(ChatUserPeerExtension newUser)
        {
            var data = new List<string>() { Name, newUser.Username };
            var msg = MessageHelper.Create(MstOpCodes.UserJoinedChannel, data.ToBytes());

            foreach (var user in GetUsersSnapshot())
            {
                if (user.Username != newUser.Username)
                {
                    user.Peer.SendMessage(msg, DeliveryMethod.ReliableFragmented);
                }
            }
        }

        /// <summary>
        /// Notify all users about user who left this channel
        /// </summary>
        /// <param name="removedUser"></param>
        protected virtual void NotifyOnLeft(ChatUserPeerExtension removedUser)
        {
            var data = new List<string>() { Name, removedUser.Username };
            var msg = MessageHelper.Create(MstOpCodes.UserLeftChannel, data.ToBytes());

            foreach (var user in GetUsersSnapshot())
            {
                if (user.Username != removedUser.Username)
                {
                    user.Peer.SendMessage(msg, DeliveryMethod.ReliableFragmented);
                }
            }
        }

        /// <summary>
        /// Check if user is allowed to be added to channel
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        protected virtual bool IsUserAllowed(ChatUserPeerExtension user)
        {
            string username = NormalizeUsername(user?.Username);

            lock (syncRoot)
            {
                return IsUserAllowedLocked(username);
            }
        }

        public virtual bool InviteUser(string username)
        {
            ChatChannelPermission permissions;

            lock (syncRoot)
            {
                permissions = Options.InvitedMemberPermissions;
            }

            return InviteUser(username, permissions);
        }

        public virtual bool InviteUser(string username, ChatChannelPermission permissions)
        {
            username = NormalizeUsername(username);

            lock (syncRoot)
            {
                if (string.IsNullOrWhiteSpace(username) || Options.IsArchived || IsBannedLocked(username))
                    return false;

                invitedUsers[username] = permissions;
                return true;
            }
        }

        public virtual bool RevokeInvite(string username)
        {
            username = NormalizeUsername(username);

            lock (syncRoot)
            {
                return !string.IsNullOrWhiteSpace(username) && invitedUsers.Remove(username);
            }
        }

        public virtual bool BanUser(string username)
        {
            username = NormalizeUsername(username);

            if (string.IsNullOrWhiteSpace(username))
                return false;

            ChatUserPeerExtension user = null;

            lock (syncRoot)
            {
                invitedUsers.Remove(username);
                userPermissions.Remove(username);
                bannedUsers.Add(username);

                channelUsers.TryGetValue(username, out user);
            }

            if (user != null)
                RemoveUser(user);

            return true;
        }

        public virtual bool RemoveMember(string username)
        {
            username = NormalizeUsername(username);

            if (string.IsNullOrWhiteSpace(username))
                return false;

            ChatUserPeerExtension user = null;

            lock (syncRoot)
            {
                invitedUsers.Remove(username);
                userPermissions.Remove(username);

                channelUsers.TryGetValue(username, out user);
            }

            if (user != null)
                RemoveUser(user);

            return true;
        }

        public virtual bool UnbanUser(string username)
        {
            username = NormalizeUsername(username);

            lock (syncRoot)
            {
                return !string.IsNullOrWhiteSpace(username) && bannedUsers.Remove(username);
            }
        }

        public virtual bool IsBanned(string username)
        {
            username = NormalizeUsername(username);

            lock (syncRoot)
            {
                return IsBannedLocked(username);
            }
        }

        public virtual bool IsInvited(string username)
        {
            username = NormalizeUsername(username);

            lock (syncRoot)
            {
                return !string.IsNullOrWhiteSpace(username) && invitedUsers.ContainsKey(username);
            }
        }

        public virtual void SetPermissions(string username, ChatChannelPermission permissions)
        {
            username = NormalizeUsername(username);

            lock (syncRoot)
            {
                if (!string.IsNullOrWhiteSpace(username))
                    userPermissions[username] = permissions;
            }
        }

        public virtual ChatChannelPermission GetPermissions(string username)
        {
            username = NormalizeUsername(username);

            if (string.IsNullOrWhiteSpace(username))
                return ChatChannelPermission.None;

            lock (syncRoot)
            {
                return GetPermissionsLocked(username);
            }
        }

        public virtual bool HasPermission(ChatUserPeerExtension user, ChatChannelPermission permission)
        {
            return user != null && HasPermission(user.Username, permission);
        }

        public virtual bool HasPermission(string username, ChatChannelPermission permission)
        {
            username = NormalizeUsername(username);

            lock (syncRoot)
            {
                if (Options.IsArchived || IsBannedLocked(username))
                    return false;

                ChatChannelPermission permissions = GetPermissionsLocked(username);
                return (permissions & permission) == permission;
            }
        }

        public virtual bool IsOwner(ChatUserPeerExtension user)
        {
            return user != null && IsOwner(user.Username);
        }

        public virtual bool IsOwner(string username)
        {
            username = NormalizeUsername(username);

            lock (syncRoot)
            {
                string ownerUsername = NormalizeUsername(Options.OwnerUsername);
                return !string.IsNullOrWhiteSpace(ownerUsername)
                       && string.Equals(ownerUsername, username, StringComparison.OrdinalIgnoreCase);
            }
        }

        protected virtual string NormalizeUsername(string username)
        {
            return (username ?? string.Empty).Trim();
        }

        /// <summary>
        /// Invoked, when user, who is connected to this channel, leaves
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnUserDisconnect(IPeer peer)
        {
            var extension = peer.GetExtension<ChatUserPeerExtension>();

            if (extension != null)
                RemoveUser(extension);
        }

        /// <summary>
        /// Removes user from channel
        /// </summary>
        /// <param name="user"></param>
        public void RemoveUser(ChatUserPeerExtension user)
        {
            if (user == null)
                return;

            string username = NormalizeUsername(user.Username);

            bool removed;

            lock (syncRoot)
            {
                // Remove disconnect listener
                user.Peer.OnConnectionCloseEvent -= OnUserDisconnect;

                // Remove channel from users collection
                user.RemoveChannel(this);

                // Remove user
                removed = channelUsers.TryGetValue(username, out ChatUserPeerExtension currentUser)
                          && currentUser == user
                          && channelUsers.Remove(username);

                if (user.DefaultChannel == this)
                    user.DefaultChannel = null;
            }

            if (removed)
                NotifyOnLeft(user);
        }

        /// <summary>
        /// Handle messages
        /// </summary>
        public virtual void BroadcastMessage(ChatMessagePacket packet)
        {
            // Override name to be in a "standard" format (uppercase letters and etc.)
            packet.Receiver = Name;

            var msg = MessageHelper.Create(MstOpCodes.ChatMessage, packet);

            foreach (var user in GetUsersSnapshot())
            {
                user.Peer.SendMessage(msg, DeliveryMethod.ReliableFragmentedSequenced);
            }
        }

        public virtual List<ChatUserPeerExtension> GetUsersSnapshot()
        {
            lock (syncRoot)
            {
                return new List<ChatUserPeerExtension>(channelUsers.Values);
            }
        }

        public virtual List<string> GetUsernamesSnapshot()
        {
            lock (syncRoot)
            {
                return new List<string>(channelUsers.Keys);
            }
        }

        public virtual int UsersCount
        {
            get
            {
                lock (syncRoot)
                {
                    return channelUsers.Count;
                }
            }
        }

        public virtual bool ContainsUser(ChatUserPeerExtension user)
        {
            string username = NormalizeUsername(user?.Username);

            lock (syncRoot)
            {
                return !string.IsNullOrWhiteSpace(username)
                       && channelUsers.TryGetValue(username, out ChatUserPeerExtension currentUser)
                       && currentUser == user;
            }
        }

        private bool IsUserAllowedLocked(string username)
        {
            if (string.IsNullOrWhiteSpace(username) || Options.IsArchived || IsBannedLocked(username) || channelUsers.ContainsKey(username))
                return false;

            return HasPermissionLocked(username, ChatChannelPermission.Join);
        }

        private bool IsBannedLocked(string username)
        {
            return !string.IsNullOrWhiteSpace(username) && bannedUsers.Contains(username);
        }

        private ChatChannelPermission GetPermissionsLocked(string username)
        {
            if (IsOwnerLocked(username))
                return Options.OwnerPermissions;

            if (userPermissions.TryGetValue(username, out ChatChannelPermission permissions))
                return permissions;

            if (invitedUsers.TryGetValue(username, out ChatChannelPermission invitePermissions))
                return invitePermissions;

            return Options.RequiresMembership() ? ChatChannelPermission.None : Options.DefaultMemberPermissions;
        }

        private bool HasPermissionLocked(string username, ChatChannelPermission permission)
        {
            if (Options.IsArchived || IsBannedLocked(username))
                return false;

            ChatChannelPermission permissions = GetPermissionsLocked(username);
            return (permissions & permission) == permission;
        }

        private bool IsOwnerLocked(string username)
        {
            string ownerUsername = NormalizeUsername(Options.OwnerUsername);
            return !string.IsNullOrWhiteSpace(ownerUsername)
                   && string.Equals(ownerUsername, username, StringComparison.OrdinalIgnoreCase);
        }
    }
}
