using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ChatServer : MstBaseClient
    {
        public ChatServer(IClientSocket connection) : base(connection) { }

        public void EnsureChannel(ChatChannelOptionsPacket options, SuccessCallback callback = null)
        {
            EnsureChannel(options, callback, Connection);
        }

        public void EnsureChannel(ChatChannelOptionsPacket options, SuccessCallback callback, IClientSocket connection)
        {
            SendSuccessRequest(MstOpCodes.ServerEnsureChatChannel, options ?? new ChatChannelOptionsPacket(), callback, connection);
        }

        public void AddUserToChannel(string channel, string username, SuccessCallback callback = null)
        {
            AddUserToChannel(channel, username, ChatChannelPermission.DefaultMember, callback, Connection);
        }

        public void AddUserToChannel(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback = null)
        {
            AddUserToChannel(channel, username, permissions, callback, Connection);
        }

        public void AddUserToChannel(string channel, string username, ChatChannelPermission permissions, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelUserRequest(MstOpCodes.ServerAddChatUserToChannel, channel, username, permissions, callback, connection);
        }

        public void RemoveUserFromChannel(string channel, string username, SuccessCallback callback = null)
        {
            RemoveUserFromChannel(channel, username, callback, Connection);
        }

        public void RemoveUserFromChannel(string channel, string username, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelUserRequest(MstOpCodes.ServerRemoveChatUserFromChannel, channel, username, ChatChannelPermission.None, callback, connection);
        }

        public void SendMessageToUsers(string senderUsername, IEnumerable<string> recipients, string message, SuccessCallback callback = null)
        {
            SendMessageToUsers(senderUsername, recipients, message, callback, Connection);
        }

        public void SendMessageToUsers(string senderUsername, IEnumerable<string> recipients, string message, SuccessCallback callback, IClientSocket connection)
        {
            SendMessageToUsers(senderUsername, string.Empty, string.Empty, recipients, message, callback, connection);
        }

        public void SendMessageToUsers(string senderUsername, string receiver, IEnumerable<string> recipients, string message, SuccessCallback callback = null)
        {
            SendMessageToUsers(senderUsername, receiver, recipients, message, callback, Connection);
        }

        public void SendMessageToUsers(string senderUsername, string receiver, string senderDisplayName,
            IEnumerable<string> recipients, string message, SuccessCallback callback = null)
        {
            SendMessageToUsers(senderUsername, receiver, senderDisplayName, recipients, message, callback, Connection);
        }

        public void SendMessageToUsers(string senderUsername, string receiver, string senderDisplayName,
            string senderAvatar, string receiverAvatar, IEnumerable<string> recipients, string message,
            SuccessCallback callback = null)
        {
            SendMessageToUsers(senderUsername, receiver, senderDisplayName, senderAvatar, receiverAvatar,
                recipients, message, callback, Connection);
        }

        public void SendMessageToUsers(string senderUsername, string receiver, IEnumerable<string> recipients, string message, SuccessCallback callback, IClientSocket connection)
        {
            SendMessageToUsers(senderUsername, receiver, string.Empty, recipients, message, callback, connection);
        }

        public void SendMessageToUsers(string senderUsername, string receiver, string senderDisplayName,
            IEnumerable<string> recipients, string message, SuccessCallback callback, IClientSocket connection)
        {
            SendMessageToUsers(senderUsername, receiver, senderDisplayName, string.Empty, string.Empty,
                recipients, message, callback, connection);
        }

        public void SendMessageToUsers(string senderUsername, string receiver, string senderDisplayName,
            string senderAvatar, string receiverAvatar, IEnumerable<string> recipients, string message,
            SuccessCallback callback, IClientSocket connection)
        {
            SendServerMessage(new ServerChatMessagePacket
            {
                MessageType = ChatMessageType.Users,
                Receiver = receiver,
                Sender = senderUsername,
                SenderDisplayName = senderDisplayName,
                SenderAvatar = senderAvatar,
                ReceiverAvatar = receiverAvatar,
                Message = message
            }, recipients, callback, connection);
        }

        public void SendPrivateMessage(string senderUsername, string receiver, string message, SuccessCallback callback = null)
        {
            SendPrivateMessage(senderUsername, receiver, message, callback, Connection);
        }

        public void SendPrivateMessage(string senderUsername, string senderDisplayName, string receiver,
            string receiverDisplayName, string message, SuccessCallback callback = null)
        {
            SendPrivateMessage(senderUsername, senderDisplayName, receiver, receiverDisplayName, message, callback, Connection);
        }

        public void SendPrivateMessage(string senderUsername, string senderDisplayName, string senderAvatar,
            string receiver, string receiverDisplayName, string receiverAvatar, string message,
            SuccessCallback callback = null)
        {
            SendPrivateMessage(senderUsername, senderDisplayName, senderAvatar, receiver, receiverDisplayName,
                receiverAvatar, message, callback, Connection);
        }

        public void SendPrivateMessage(string senderUsername, string receiver, string message, SuccessCallback callback, IClientSocket connection)
        {
            SendPrivateMessage(senderUsername, string.Empty, receiver, string.Empty, message, callback, connection);
        }

        public void SendPrivateMessage(string senderUsername, string senderDisplayName, string receiver,
            string receiverDisplayName, string message, SuccessCallback callback, IClientSocket connection)
        {
            SendPrivateMessage(senderUsername, senderDisplayName, string.Empty, receiver, receiverDisplayName,
                string.Empty, message, callback, connection);
        }

        public void SendPrivateMessage(string senderUsername, string senderDisplayName, string senderAvatar,
            string receiver, string receiverDisplayName, string receiverAvatar, string message,
            SuccessCallback callback, IClientSocket connection)
        {
            SendServerMessage(new ServerChatMessagePacket
            {
                MessageType = ChatMessageType.Private,
                Receiver = receiver,
                Sender = senderUsername,
                SenderDisplayName = senderDisplayName,
                ReceiverDisplayName = receiverDisplayName,
                SenderAvatar = senderAvatar,
                ReceiverAvatar = receiverAvatar,
                Message = message
            }, null, callback, connection);
        }

        public void SendChannelMessage(string senderUsername, string channel, string message, SuccessCallback callback = null)
        {
            SendChannelMessage(senderUsername, channel, message, callback, Connection);
        }

        public void SendChannelMessage(string senderUsername, string senderDisplayName, string channel,
            string message, SuccessCallback callback = null)
        {
            SendChannelMessage(senderUsername, senderDisplayName, channel, message, callback, Connection);
        }

        public void SendChannelMessage(string senderUsername, string senderDisplayName, string senderAvatar,
            string channel, string message, SuccessCallback callback = null)
        {
            SendChannelMessage(senderUsername, senderDisplayName, senderAvatar, channel, message, callback, Connection);
        }

        public void SendChannelMessage(string senderUsername, string channel, string message, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelMessage(senderUsername, string.Empty, channel, message, callback, connection);
        }

        public void SendChannelMessage(string senderUsername, string senderDisplayName, string channel,
            string message, SuccessCallback callback, IClientSocket connection)
        {
            SendChannelMessage(senderUsername, senderDisplayName, string.Empty, channel, message, callback, connection);
        }

        public void SendChannelMessage(string senderUsername, string senderDisplayName, string senderAvatar,
            string channel, string message, SuccessCallback callback, IClientSocket connection)
        {
            SendServerMessage(new ServerChatMessagePacket
            {
                MessageType = ChatMessageType.Channel,
                Receiver = channel,
                Sender = senderUsername,
                SenderDisplayName = senderDisplayName,
                SenderAvatar = senderAvatar,
                Message = message
            }, null, callback, connection);
        }

        private void SendChannelUserRequest(ushort opCode, string channel, string username, ChatChannelPermission permissions, SuccessCallback callback, IClientSocket connection)
        {
            SendSuccessRequest(opCode, new ChatChannelUserPacket
            {
                ChannelName = channel,
                Username = username,
                Permissions = permissions
            }, callback, connection);
        }

        private void SendServerMessage(ServerChatMessagePacket packet, IEnumerable<string> recipients, SuccessCallback callback, IClientSocket connection)
        {
            if (recipients != null)
                packet.Recipients.AddRange(recipients);

            SendSuccessRequest(MstOpCodes.ServerSendChatMessageToUsers, packet, callback, connection);
        }

        private void SendSuccessRequest(ushort opCode, ISerializablePacket payload, SuccessCallback callback, IClientSocket connection)
        {
            if (connection == null || !connection.IsConnected)
            {
                string error = Mst.Errors.Parse(ResponseStatus.NotConnected);
                Logs.Error("Chat server request failed. Status=NotConnected");
                callback?.Invoke(false, error);
                return;
            }

            connection.SendMessage(opCode, payload, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    string error = Mst.Errors.Parse(status, response);
                    Logs.Error($"Chat server request failed. Status={status}");
                    callback?.Invoke(false, error);
                    return;
                }

                callback?.Invoke(true, string.Empty);
            });
        }
    }
}
