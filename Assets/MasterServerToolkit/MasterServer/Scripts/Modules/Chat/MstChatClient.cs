using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class MstChatClient : MstBaseClient
    {
        public delegate void ChatChannelsCallback(List<ChatChannelInfo> channels, string error);
        public delegate void ChatUsersCallback(List<string> users, string error);

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

        public MstChatClient(IClientSocket connection) : base(connection)
        {
            SetHandler((short)MstMessageCodes.UserJoinedChannel, OnUserJoinedChannelHandler);
            SetHandler((short)MstMessageCodes.UserLeftChannel, OnUserLeftChannelHandler);
            SetHandler((short)MstMessageCodes.ChatMessage, OnChatMessageHandler);
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
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage((short)MstMessageCodes.PickUsername, username, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
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
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage((short)MstMessageCodes.JoinChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
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
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage((short)MstMessageCodes.LeaveChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
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
            if (!connection.IsConnected)
            {
                callback.Invoke(false, "Not connected");
                return;
            }

            connection.SendMessage((short)MstMessageCodes.SetDefaultChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
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
            if (!connection.IsConnected)
            {
                callback.Invoke(new List<ChatChannelInfo>(), "Not connected");
                return;
            }

            connection.SendMessage((short)MstMessageCodes.GetCurrentChannels, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(new List<ChatChannelInfo>(), response.AsString("Unknown error"));
                    return;
                }

                var data = response.Deserialize(new ChatChannelsListPacket());
                callback.Invoke(data.Channels, null);
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
            if (!connection.IsConnected)
            {
                callback.Invoke(new List<string>(), "Not connected");
                return;
            }

            connection.SendMessage((short)MstMessageCodes.GetUsersInChannel, channel, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(new List<string>(), response.AsString("Unknown error"));
                    return;
                }

                var list = new List<string>().FromBytes(response.AsBytes());

                callback.Invoke(list, null);
            });
        }

        /// <summary>
        /// Sends a message to default channel
        /// </summary>
        /// <param name="message"></param>
        /// <param name="callback"></param>
        public void SendToDefaultChannel(string message, SuccessCallback callback)
        {
            SendMessage(new ChatMessagePacket()
            {
                Receiver = "",
                Message = message,
                MessageType = ChatMessageType.ChannelMessage
            }, callback, Connection);
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
                MessageType = ChatMessageType.ChannelMessage
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
                MessageType = ChatMessageType.PrivateMessage
            }, callback, Connection);
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
            connection.SendMessage((short)MstMessageCodes.ChatMessage, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback.Invoke(false, response.AsString("Unknown error"));
                    return;
                }

                callback.Invoke(true, null);
            });
        }

        #region Message handlers

        private void OnChatMessageHandler(IIncomingMessage message)
        {
            var packet = message.Deserialize(new ChatMessagePacket());

            if (OnMessageReceivedEvent != null)
            {
                OnMessageReceivedEvent.Invoke(packet);
            }
        }

        private void OnUserLeftChannelHandler(IIncomingMessage message)
        {
            var data = new List<string>().FromBytes(message.AsBytes());
            if (OnUserLeftChannelEvent != null)
            {
                OnUserLeftChannelEvent.Invoke(data[0], data[1]);
            }
        }

        private void OnUserJoinedChannelHandler(IIncomingMessage message)
        {
            var data = new List<string>().FromBytes(message.AsBytes());
            OnUserJoinedChannelEvent?.Invoke(data[0], data[1]);
        }

        #endregion
    }
}