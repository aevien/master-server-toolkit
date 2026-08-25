using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ChatClient : MstBaseClient
    {
        public delegate void ChatChannelsCallback(List<ChatChannelInfo> channels, string error);
        public delegate void ChatUsersCallback(List<string> users, string error);
        public delegate void ChatChannelInfoCallback(ChatChannelInfoPacket channel, string error);

        public delegate void ChatUserHandler(string channel, string user);
        public delegate void ChatMessageHandler(ChatMessagePacket message);

        /// <summary>
        /// Invoked, when user leaves a channel
        /// </summary>
        public event ChatUserHandler OnUserLeftChannelEvent;

        /// <summary>
        /// Invoked, when user joins a channel
        /// </summary>
        public event ChatUserHandler OnUserJoinedChannelEvent;

        /// <summary>
        /// Invoked, when a new message is received
        /// </summary>
        public event ChatMessageHandler OnMessageReceivedEvent;

        public ChatClient(IClientSocket connection) : base(connection)
        {
            RegisterErrors(
                MstErrorCodes.CHAT_CANNOT_BAN_SELF,
                MstErrorCodes.CHAT_CHANNEL_ALREADY_EXISTS,
                MstErrorCodes.CHAT_CHANNEL_AND_USERNAME_REQUIRED,
                MstErrorCodes.CHAT_CHANNEL_BAN_FAILED,
                MstErrorCodes.CHAT_CHANNEL_CREATE_FAILED,
                MstErrorCodes.CHAT_CHANNEL_ENSURE_FAILED,
                MstErrorCodes.CHAT_CHANNEL_INVITE_FAILED,
                MstErrorCodes.CHAT_CHANNEL_JOIN_FAILED,
                MstErrorCodes.CHAT_CHANNEL_MEMBERSHIP_REQUIRED,
                MstErrorCodes.CHAT_CHANNEL_NOT_FOUND,
                MstErrorCodes.CHAT_CHANNEL_PERMISSION_REQUIRED,
                MstErrorCodes.CHAT_CHANNEL_REQUIRED,
                MstErrorCodes.CHAT_CHANNEL_TYPE_FORBIDDEN,
                MstErrorCodes.CHAT_CHANNEL_TYPE_INVALID,
                MstErrorCodes.CHAT_CHANNEL_USER_ADD_FAILED,
                MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED,
                MstErrorCodes.CHAT_CHANNEL_USER_REMOVE_FAILED,
                MstErrorCodes.CHAT_DEFAULT_CHANNEL_NOT_SET,
                MstErrorCodes.CHAT_DEFAULT_PERMISSIONS_INVALID,
                MstErrorCodes.CHAT_HISTORY_LIMIT_INVALID,
                MstErrorCodes.CHAT_HISTORY_VISIBILITY_INVALID,
                MstErrorCodes.CHAT_INVITED_PERMISSIONS_INVALID,
                MstErrorCodes.CHAT_MESSAGE_TYPE_INVALID,
                MstErrorCodes.CHAT_OWNER_PERMISSION_REQUIRED,
                MstErrorCodes.CHAT_OWNER_TARGET_PROTECTED,
                MstErrorCodes.CHAT_PERMISSION_GRANT_EXCEEDS_OWN,
                MstErrorCodes.CHAT_RECEIVER_REQUIRED,
                MstErrorCodes.CHAT_RECIPIENTS_OFFLINE,
                MstErrorCodes.CHAT_RECIPIENTS_REQUIRED,
                MstErrorCodes.CHAT_SENDER_REQUIRED,
                MstErrorCodes.CHAT_SERVER_API_FORBIDDEN,
                MstErrorCodes.CHAT_SERVER_MESSAGE_PACKET_REQUIRED,
                MstErrorCodes.CHAT_TARGET_USERNAME_REQUIRED,
                MstErrorCodes.CHAT_USER_ADD_FAILED,
                MstErrorCodes.CHAT_USER_ALREADY_IDENTIFIED,
                MstErrorCodes.CHAT_USER_NOT_IDENTIFIED,
                MstErrorCodes.CHAT_USER_NOT_ONLINE,
                MstErrorCodes.CHAT_USERNAME_ALREADY_EXISTS,
                MstErrorCodes.CHAT_USERNAME_PICKING_DISABLED,
                MstErrorCodes.CHAT_USERNAME_REQUIRED,
                MstErrorCodes.CHAT_USERNAME_WHITESPACE_INVALID,
                MstErrorCodes.CHAT_USERS_MESSAGE_FORBIDDEN);

            RegisterMessageHandler(MstOpCodes.UserJoinedChannel, OnUserJoinedChannelHandler);
            RegisterMessageHandler(MstOpCodes.UserLeftChannel, OnUserLeftChannelHandler);
            RegisterMessageHandler(MstOpCodes.ChatMessage, OnChatMessageHandler);
        }

        /// <summary>
        /// Sends a request to set chat username
        /// </summary>
        /// <param name="username"></param>
        /// <param name="callback"></param>
        public void PickUsername(string username, SuccessCallback callback)
        {
            PickUsername(username, callback, Connection);
        }

        /// <summary>
        /// Sends a request to set chat username
        /// </summary>
        public void PickUsername(string username, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.PickUsername, username, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to join a specified channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="callback"></param>
        public void JoinChannel(string channel, SuccessCallback callback)
        {
            JoinChannel(channel, callback, Connection);
        }

        /// <summary>
        /// Sends a request to join a specified channel
        /// </summary>
        public void JoinChannel(string channel, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.JoinChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sends a request to leave a specified channel
        /// </summary>
        public void LeaveChannel(string channel, SuccessCallback callback)
        {
            LeaveChannel(channel, callback, Connection);
        }

        /// <summary>
        /// Sends a request to leave a specified channel
        /// </summary>
        public void LeaveChannel(string channel, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.LeaveChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        /// <summary>
        /// Sets a default channel to the specified channel.
        /// Messages, that have no channel, will be sent to default channel
        /// </summary>
        public void SetDefaultChannel(string channel, SuccessCallback callback)
        {
            SetDefaultChannel(channel, callback, Connection);
        }

        /// <summary>
        /// Sets a default channel to the specified channel.
        /// Messages, that have no channel, will be sent to default channel
        /// </summary>
        public void SetDefaultChannel(string channel, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.SetDefaultChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        /// <summary>
        /// Retrieves a list of channels, which user has joined
        /// </summary>
        /// <param name="callback"></param>
        public void GetMyChannels(ChatChannelsCallback callback)
        {
            GetMyChannels(callback, Connection);
        }

        /// <summary>
        /// Retrieves a list of channels, which user has joined
        /// </summary>
        public void GetMyChannels(ChatChannelsCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(new List<ChatChannelInfo>(), Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.GetCurrentChannels, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(new List<ChatChannelInfo>(), Mst.Errors.Parse(status, response));
                    return;
                }

                var data = response.AsPacket<ChatChannelsListPacket>();
                callback?.Invoke(data.Channels, null);
            });
        }

        /// <summary>
        /// Retrieves a list users in a channel
        /// </summary>
        public void GetUsersInChannel(string channel, ChatUsersCallback callback)
        {
            GetUsersInChannel(channel, callback, Connection);
        }

        /// <summary>
        /// Retrieves a list of users in a channel
        /// </summary>
        public void GetUsersInChannel(string channel, ChatUsersCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(new List<string>(), Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.GetUsersInChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(new List<string>(), Mst.Errors.Parse(status, response));
                    return;
                }

                var list = new List<string>().FromBytes(response.AsBytes());

                callback?.Invoke(list, null);
            });
        }

        /// <summary>
        /// Sends a message to default channel
        /// </summary>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void SendToDefaultChannel(string message, SuccessCallback callback)
        {
            SendChannelMessage(string.Empty, message, callback);
        }

        /// <summary>
        /// Sends a message to specified channel
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void SendChannelMessage(string channel, string message, SuccessCallback callback)
        {
            SendMessage(new ChatMessagePacket()
            {
                Receiver = channel,
                Message = message,
                MessageType = ChatMessageType.Channel
            }, callback, Connection);
        }

        /// <summary>
        /// Sends a private message to specified user
        /// </summary>
        /// <param name="receiver"></param>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void SendPrivateMessage(string receiver, string message, SuccessCallback callback)
        {
            SendMessage(new ChatMessagePacket()
            {
                Receiver = receiver,
                Message = message,
                MessageType = ChatMessageType.Private
            }, callback, Connection);
        }

        /// <summary>
        /// Creates a public chat channel and joins it.
        /// </summary>
        public void CreateChannel(string channel, SuccessCallback callback)
        {
            CreateChannel(channel, ChatChannelType.Public, false, callback);
        }

        /// <summary>
        /// Creates a public chat channel and joins it.
        /// </summary>
        public void CreateChannel(string channel, SuccessCallback callback, IClientSocket connection)
        {
            CreateChannel(channel, ChatChannelType.Public, false, callback, connection);
        }

        /// <summary>
        /// Creates a chat channel and joins it.
        /// </summary>
        public void CreateChannel(string channel, ChatChannelType type, bool isClosed, SuccessCallback callback)
        {
            CreateChannel(channel, type, isClosed, callback, Connection);
        }

        /// <summary>
        /// Creates a chat channel and joins it.
        /// </summary>
        public void CreateChannel(string channel, ChatChannelType type, bool isClosed, SuccessCallback callback, IClientSocket connection)
        {
            CreateChannel(new ChatChannelOptionsPacket
            {
                ChannelName = channel,
                Type = type,
                IsClosed = isClosed
            }, callback, connection);
        }

        /// <summary>
        /// Creates a chat channel and joins it.
        /// </summary>
        public void CreateChannel(ChatChannelOptionsPacket options, SuccessCallback callback)
        {
            CreateChannel(options, callback, Connection);
        }

        /// <summary>
        /// Creates a chat channel and joins it.
        /// </summary>
        public void CreateChannel(ChatChannelOptionsPacket options, SuccessCallback callback, IClientSocket connection)
        {
            SendSuccessRequest(MstOpCodes.CreateChatChannel, options ?? new ChatChannelOptionsPacket(), callback, connection);
        }

        public void CreateChatChannel(ChatChannelOptionsPacket options, SuccessCallback callback)
        {
            CreateChannel(options, callback);
        }

        public void CreateChatChannel(ChatChannelOptionsPacket options, SuccessCallback callback, IClientSocket connection)
        {
            CreateChannel(options, callback, connection);
        }

        /// <summary>
        /// Gets extended chat channel information.
        /// </summary>
        public void GetChannelInfo(string channel, ChatChannelInfoCallback callback)
        {
            GetChannelInfo(channel, callback, Connection);
        }

        /// <summary>
        /// Gets extended chat channel information.
        /// </summary>
        public void GetChannelInfo(string channel, ChatChannelInfoCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.GetChatChannelInfo, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(response.AsPacket<ChatChannelInfoPacket>(), null);
            });
        }

        public void GetChatChannelInfo(string channel, ChatChannelInfoCallback callback)
        {
            GetChannelInfo(channel, callback);
        }

        public void GetChatChannelInfo(string channel, ChatChannelInfoCallback callback, IClientSocket connection)
        {
            GetChannelInfo(channel, callback, connection);
        }

        /// <summary>
        /// Gets chat channels that invited current user.
        /// </summary>
        public void GetInvites(ChatChannelsCallback callback)
        {
            GetInvites(callback, Connection);
        }

        /// <summary>
        /// Gets chat channels that invited current user.
        /// </summary>
        public void GetInvites(ChatChannelsCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(new List<ChatChannelInfo>(), Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.GetChatInvites, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(new List<ChatChannelInfo>(), Mst.Errors.Parse(status, response));
                    return;
                }

                var data = response.AsPacket<ChatChannelsListPacket>();
                callback?.Invoke(data.Channels, null);
            });
        }

        public void GetChatInvites(ChatChannelsCallback callback)
        {
            GetInvites(callback);
        }

        public void GetChatInvites(ChatChannelsCallback callback, IClientSocket connection)
        {
            GetInvites(callback, connection);
        }

        /// <summary>
        /// Invites a user to a chat channel.
        /// </summary>
        public void InviteToChannel(string channel, string username, SuccessCallback callback)
        {
            InviteToChannel(channel, username, ChatChannelPermission.DefaultMember, callback);
        }

        /// <summary>
        /// Invites a user to a chat channel with explicit permissions.
        /// </summary>
        public void InviteToChannel(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback)
        {
            InviteToChannel(channel, username, permissions, callback, Connection);
        }

        /// <summary>
        /// Invites a user to a chat channel with explicit permissions.
        /// </summary>
        public void InviteToChannel(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelUserRequest(MstOpCodes.InviteToChatChannel, channel, username, permissions, callback, connection);
        }

        public void InviteToChatChannel(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback)
        {
            InviteToChannel(channel, username, permissions, callback);
        }

        public void InviteToChatChannel(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback, IClientSocket connection)
        {
            InviteToChannel(channel, username, permissions, callback, connection);
        }

        /// <summary>
        /// Revokes a pending chat channel invite.
        /// </summary>
        public void RevokeInvite(string channel, string username, SuccessCallback callback)
        {
            RevokeInvite(channel, username, callback, Connection);
        }

        /// <summary>
        /// Revokes a pending chat channel invite.
        /// </summary>
        public void RevokeInvite(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelUserRequest(MstOpCodes.RevokeChatInvite, channel, username, ChatChannelPermission.None, callback, connection);
        }

        public void RevokeChatInvite(string channel, string username, SuccessCallback callback)
        {
            RevokeInvite(channel, username, callback);
        }

        public void RevokeChatInvite(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            RevokeInvite(channel, username, callback, connection);
        }

        /// <summary>
        /// Accepts a chat channel invite.
        /// </summary>
        public void AcceptInvite(string channel, SuccessCallback callback)
        {
            AcceptInvite(channel, callback, Connection);
        }

        /// <summary>
        /// Accepts a chat channel invite.
        /// </summary>
        public void AcceptInvite(string channel, SuccessCallback callback, IClientSocket connection)
        {
            SendSuccessRequest(MstOpCodes.AcceptChatInvite, channel, callback, connection);
        }

        public void AcceptChatInvite(string channel, SuccessCallback callback)
        {
            AcceptInvite(channel, callback);
        }

        public void AcceptChatInvite(string channel, SuccessCallback callback, IClientSocket connection)
        {
            AcceptInvite(channel, callback, connection);
        }

        /// <summary>
        /// Declines a chat channel invite.
        /// </summary>
        public void DeclineInvite(string channel, SuccessCallback callback)
        {
            DeclineInvite(channel, callback, Connection);
        }

        /// <summary>
        /// Declines a chat channel invite.
        /// </summary>
        public void DeclineInvite(string channel, SuccessCallback callback, IClientSocket connection)
        {
            SendSuccessRequest(MstOpCodes.DeclineChatInvite, channel, callback, connection);
        }

        public void DeclineChatInvite(string channel, SuccessCallback callback)
        {
            DeclineInvite(channel, callback);
        }

        public void DeclineChatInvite(string channel, SuccessCallback callback, IClientSocket connection)
        {
            DeclineInvite(channel, callback, connection);
        }

        /// <summary>
        /// Kicks a user from a chat channel.
        /// </summary>
        public void KickFromChannel(string channel, string username, SuccessCallback callback)
        {
            KickFromChannel(channel, username, callback, Connection);
        }

        /// <summary>
        /// Kicks a user from a chat channel.
        /// </summary>
        public void KickFromChannel(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelUserRequest(MstOpCodes.KickChatUser, channel, username, ChatChannelPermission.None, callback, connection);
        }

        public void KickChatUser(string channel, string username, SuccessCallback callback)
        {
            KickFromChannel(channel, username, callback);
        }

        public void KickChatUser(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            KickFromChannel(channel, username, callback, connection);
        }

        /// <summary>
        /// Bans a user from a chat channel.
        /// </summary>
        public void BanFromChannel(string channel, string username, SuccessCallback callback)
        {
            BanFromChannel(channel, username, callback, Connection);
        }

        /// <summary>
        /// Bans a user from a chat channel.
        /// </summary>
        public void BanFromChannel(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelUserRequest(MstOpCodes.BanChatUser, channel, username, ChatChannelPermission.None, callback, connection);
        }

        public void BanChatUser(string channel, string username, SuccessCallback callback)
        {
            BanFromChannel(channel, username, callback);
        }

        public void BanChatUser(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            BanFromChannel(channel, username, callback, connection);
        }

        /// <summary>
        /// Removes a user ban from a chat channel.
        /// </summary>
        public void UnbanFromChannel(string channel, string username, SuccessCallback callback)
        {
            UnbanFromChannel(channel, username, callback, Connection);
        }

        /// <summary>
        /// Removes a user ban from a chat channel.
        /// </summary>
        public void UnbanFromChannel(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelUserRequest(MstOpCodes.UnbanChatUser, channel, username, ChatChannelPermission.None, callback, connection);
        }

        public void UnbanChatUser(string channel, string username, SuccessCallback callback)
        {
            UnbanFromChannel(channel, username, callback);
        }

        public void UnbanChatUser(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            UnbanFromChannel(channel, username, callback, connection);
        }

        /// <summary>
        /// Sets user permissions in a chat channel.
        /// </summary>
        public void SetChannelPermissions(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback)
        {
            SetChannelPermissions(channel, username, permissions, callback, Connection);
        }

        /// <summary>
        /// Sets user permissions in a chat channel.
        /// </summary>
        public void SetChannelPermissions(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelUserRequest(MstOpCodes.SetChatUserPermissions, channel, username, permissions, callback, connection);
        }

        public void SetChatUserPermissions(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback)
        {
            SetChannelPermissions(channel, username, permissions, callback);
        }

        public void SetChatUserPermissions(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback, IClientSocket connection)
        {
            SetChannelPermissions(channel, username, permissions, callback, connection);
        }

        /// <summary>
        /// Sends a generic message packet to server
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="callback"></param>
        public void SendMessage(ChatMessagePacket packet, SuccessCallback callback)
        {
            SendMessage(packet, callback, Connection);
        }

        /// <summary>
        /// Sends a generic message packet to server
        /// </summary>
        public void SendMessage(ChatMessagePacket packet, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(MstOpCodes.ChatMessage, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        protected virtual void SendChannelUserRequest(ushort opCode, string channel, string username, ChatChannelPermission permissions, SuccessCallback callback, IClientSocket connection)
        {
            SendSuccessRequest(opCode, new ChatChannelUserPacket
            {
                ChannelName = channel,
                Username = username,
                Permissions = permissions
            }, callback, connection);
        }

        protected virtual void SendSuccessRequest(ushort opCode, string payload, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(opCode, payload, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        protected virtual void SendSuccessRequest(ushort opCode, ISerializablePacket payload, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            connection.SendMessage(opCode, payload, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        #region Message handlers

        private void OnChatMessageHandler(IIncomingMessage message)
        {
            var packet = message.AsPacket<ChatMessagePacket>();
            OnMessageReceivedEvent?.Invoke(packet);
        }

        private void OnUserLeftChannelHandler(IIncomingMessage message)
        {
            var data = new List<string>().FromBytes(message.AsBytes());
            OnUserLeftChannelEvent?.Invoke(data[0], data[1]);
        }

        private void OnUserJoinedChannelHandler(IIncomingMessage message)
        {
            var data = new List<string>().FromBytes(message.AsBytes());
            OnUserJoinedChannelEvent?.Invoke(data[0], data[1]);
        }

        #endregion
    }
}
