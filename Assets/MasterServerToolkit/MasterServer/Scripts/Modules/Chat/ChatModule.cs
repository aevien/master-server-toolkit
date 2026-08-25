using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Chat module gives your availability to send chat messages to clients, create chat channels etc.
    /// </summary>
    public class ChatModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("Persistence")]
        [SerializeField, Tooltip("Optional factory that creates and registers IChatDatabaseAccessor. Leave None to deliver chat without persisting accepted raw messages.")]
        protected DatabaseAccessorFactory databaseAccessorFactory;

        /// <summary>
        /// If true, chat module will subscribe to auth module, and automatically setup chat users when they log in
        /// </summary>
        [Header("General Settings")]
        [SerializeField, Tooltip("Subscribes to AuthModule login/logout events and automatically creates and removes chat users for authenticated sessions. Disable only when project code manages chat identities explicitly.")]
        protected bool useAuthModule = true;

        [SerializeField, Tooltip("Checks channel names and outgoing message copies through CensorModule. Raw accepted messages remain unchanged for optional persistence; requires CensorModule when enabled.")]
        protected bool useCensorModule = true;

        /// <summary>
        /// If true, the first channel that user joins will be set as hist local channel
        /// </summary>
        [SerializeField, Tooltip("Sets the first joined channel as the user's local/default channel when no earlier channel is available.")]
        protected bool setFirstChannelAsLocal = true;

        /// <summary>
        /// If true, when user leaves all of the channels except for one, that one channel will be set to be his local channel
        /// </summary>
        [SerializeField, Tooltip("Sets the only remaining channel as the user's local/default channel after leaving other channels.")]
        protected bool setLastChannelAsLocal = true;

        /// <summary>
        /// If true, users will be allowed to choose usernames
        /// </summary>
        [SerializeField, Tooltip("Allows clients to request a custom chat username. Disable when chat identity must always be assigned by authentication or server code.")]
        protected bool allowUsernamePicking = true;

        /// <summary>
        /// Min number of character a channel name must consist of
        /// </summary>
        [SerializeField, Tooltip("Minimum character count accepted for client-created channel names. Keep this value less than or equal to Max Channel Name Length.")]
        protected int minChannelNameLength = 5;

        /// <summary>
        /// Max number of character a channel name must consist of
        /// </summary>
        [SerializeField, Tooltip("Maximum character count accepted for client-created channel names. Keep this value greater than or equal to Min Channel Name Length.")]
        protected int maxChannelNameLength = 25;

        #endregion

        /// <summary>
        /// Censor module for bad words checking :)
        /// </summary>
        protected CensorModule censorModule;

        /// <summary>
        /// Auth module
        /// </summary>
        protected AuthModule authModule;

        /// <summary>
        /// Optional chat history persistence.
        /// </summary>
        protected IChatDatabaseAccessor databaseAccessor;

        /// <summary>
        /// Gets or sets the optional factory used to register chat persistence.
        /// </summary>
        public DatabaseAccessorFactory DatabaseAccessorFactory
        {
            get => databaseAccessorFactory;
            set => databaseAccessorFactory = value;
        }

        /// <summary>
        /// Synchronizes chat users, channels and channel membership across async server handlers.
        /// </summary>
        protected readonly object chatStateLock = new object();

        /// <summary>
        /// Users connected to chats
        /// </summary>
        public Dictionary<string, ChatUserPeerExtension> ChatUsers { get; protected set; } = new Dictionary<string, ChatUserPeerExtension>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// List of available chat channels
        /// </summary>
        public Dictionary<string, ChatChannel> ChatChannels { get; protected set; } = new Dictionary<string, ChatChannel>(StringComparer.OrdinalIgnoreCase);

        protected override void Awake()
        {
            base.Awake();

            // Optional AuthModule dependency if "useAuthModule" is true
            AddOptionalDependency<AuthModule>();

            // Optional CensorModule for to forbade bad words in chats
            AddOptionalDependency<CensorModule>();
        }

        public override void Initialize(IServer server)
        {
            // Set handlers
            server.RegisterMessageHandler(MstOpCodes.PickUsername, OnPickUsernameRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.JoinChannel, OnJoinChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.LeaveChannel, OnLeaveChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.GetCurrentChannels, OnGetCurrentChannelsRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ChatMessage, OnChatMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.GetUsersInChannel, OnGetUsersInChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.SetDefaultChannel, OnSetDefaultChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.CreateChatChannel, OnCreateChatChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.GetChatChannelInfo, OnGetChatChannelInfoRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.GetChatInvites, OnGetChatInvitesRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.InviteToChatChannel, OnInviteToChatChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.RevokeChatInvite, OnRevokeChatInviteRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.AcceptChatInvite, OnAcceptChatInviteRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.DeclineChatInvite, OnDeclineChatInviteRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.KickChatUser, OnKickChatUserRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.BanChatUser, OnBanChatUserRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.UnbanChatUser, OnUnbanChatUserRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.SetChatUserPermissions, OnSetChatUserPermissionsRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerEnsureChatChannel, OnServerEnsureChatChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerAddChatUserToChannel, OnServerAddChatUserToChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerRemoveChatUserFromChannel, OnServerRemoveChatUserFromChannelRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ServerSendChatMessageToUsers, OnServerSendChatMessageToUsersRequestHandler);

            // Setup authModule dependencies
            authModule = server.GetModule<AuthModule>();

            // Setup censorModule
            censorModule = server.GetModule<CensorModule>();

            // Setup optional chat database accessor
            if (databaseAccessorFactory != null)
                databaseAccessorFactory.CreateAccessors();

            databaseAccessor = Mst.Server.DbAccessors.GetAccessor<IChatDatabaseAccessor>();

            if (useAuthModule)
            {
                if (authModule)
                {
                    authModule.OnUserLoggedInEvent += OnUserLoggedInEventHandler;
                    authModule.OnUserLoggedOutEvent += OnUserLoggedOutEventHandler;
                }
                else
                {
                    logger.Error($"{GetType().Name} was set to use {nameof(AuthModule)}, but {nameof(AuthModule)} was not found");
                }
            }

            if (useCensorModule && censorModule == null)
            {
                logger.Error($"{GetType().Name} was set to use {nameof(CensorModule)}, but {nameof(CensorModule)} was not found");
            }
        }

        protected virtual void OnDestroy()
        {
            if (authModule)
            {
                authModule.OnUserLoggedInEvent -= OnUserLoggedInEventHandler;
                authModule.OnUserLoggedOutEvent -= OnUserLoggedOutEventHandler;
            }
        }

        /// <summary>
        /// Add new user to chat
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        protected virtual bool AddChatUser(ChatUserPeerExtension user)
        {
            if (user == null)
            {
                return false;
            }

            string username = NormalizeUsername(user.Username);

            if (string.IsNullOrWhiteSpace(username))
            {
                logger.Error("Trying to add a chat user with an empty username");
                return false;
            }

            lock (chatStateLock)
            {
                if (ChatUsers.ContainsKey(username))
                {
                    logger.Error($"Trying to add user {username} to chat, but one is already connected");
                    return false;
                }

                // Add the new user
                ChatUsers[username] = user;

                // Start listening user disconnection
                user.Peer.OnConnectionCloseEvent += OnClientDisconnected;
            }

            logger.Debug($"User {username} has been successfully added to chat");
            return true;
        }

        /// <summary>
        /// Remove user from chat
        /// </summary>
        /// <param name="user"></param>
        protected virtual void RemoveChatUser(ChatUserPeerExtension user)
        {
            if (user == null)
            {
                return;
            }

            string username = NormalizeUsername(user.Username);

            List<ChatChannel> channels;
            bool removedFromRegistry = false;
            bool extensionCleared = false;

            lock (chatStateLock)
            {
                // Remove from chat users list only if this peer extension is still the registered one.
                if (ChatUsers.TryGetValue(username, out ChatUserPeerExtension currentUser) && currentUser == user)
                {
                    ChatUsers.Remove(username);
                    removedFromRegistry = true;
                }

                channels = user.GetChannelsSnapshot();

                // Stop listening this removed user disconnection
                user.Peer.OnConnectionCloseEvent -= OnClientDisconnected;

                // Clear stale chat identity so signed-out peers cannot keep using chat handlers.
                if (user.Peer.GetExtension<ChatUserPeerExtension>() == user)
                {
                    user.Peer.ClearExtension<ChatUserPeerExtension>();
                    extensionCleared = true;
                }
            }

            // Remove from channels
            foreach (var chatChannel in channels)
            {
                chatChannel.RemoveUser(user);
            }

            logger.Debug($"Chat cleanup user={username} registryRemoved={removedFromRegistry} extensionCleared={extensionCleared} channelsRemoved={channels.Count}");
            logger.Debug($"User {username} has been successfully removed from chat");
        }

        /// <summary>
        /// Creates chat user as <see cref="ChatUserPeerExtension"/>
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="userId"></param>
        /// <param name="username"></param>
        /// <returns></returns>
        protected virtual ChatUserPeerExtension CreateChatUser(IPeer peer, string username)
        {
            logger.Debug($"Created new chat user {username}");
            return new ChatUserPeerExtension(peer, username);
        }

        /// <summary>
        /// Retrieves an existing channel or creates a new one
        /// </summary>
        /// <param name="channelName"></param>
        /// <returns></returns>
        public virtual ChatChannel GetOrCreateChannel(string channelName)
        {
            return GetOrCreateChannel(channelName, !useCensorModule);
        }

        public virtual ChatChannel GetOrCreateChannel(string channelName, ChatChannelOptions options)
        {
            return GetOrCreateChannel(channelName, !useCensorModule, options);
        }

        /// <summary>
        /// Retrieves an existing channel or creates a new one.
        /// If <see cref="ignoreForbidden"/> value is set to false,
        /// before creating a channel, a check will be executed to make sure that
        /// no forbidden words are used in the name
        /// </summary>
        /// <param name="channelName"></param>
        /// <param name="ignoreForbidden"></param>
        /// <returns></returns>
        protected virtual ChatChannel GetOrCreateChannel(string channelName, bool ignoreForbidden)
        {
            return GetOrCreateChannel(channelName, ignoreForbidden, null);
        }

        protected virtual ChatChannel GetOrCreateChannel(string channelName, bool ignoreForbidden, ChatChannelOptions options)
        {
            channelName = NormalizeChannelName(channelName);

            logger.Debug($"Trying to get channel {channelName}");

            if (string.IsNullOrWhiteSpace(channelName))
            {
                return null;
            }

            lock (chatStateLock)
            {
                if (!ChatChannels.TryGetValue(channelName, out ChatChannel channel))
                {
                    // Check if our new channel name is incorrect
                    if (channelName.Length < minChannelNameLength || channelName.Length > maxChannelNameLength)
                    {
                        return null;
                    }

                    // There's no such channel, but we might be able to create one
                    if (!ignoreForbidden && censorModule != null && censorModule.ContainsBadWords(channelName))
                    {
                        // Channel contains a forbidden word
                        return null;
                    }

                    // Create new channel
                    channel = CreateChannel(channelName, options);

                    // Add this channel to list
                    ChatChannels.Add(channelName, channel);
                }
                else
                {
                    logger.Debug($"Channel with name {channelName} is already created");
                }

                return channel;
            }
        }

        protected virtual ChatChannel CreateChannel(string channelName)
        {
            return new ChatChannel(channelName, new ChatChannelOptions(), chatStateLock);
        }

        protected virtual ChatChannel CreateChannel(string channelName, ChatChannelOptions options)
        {
            return new ChatChannel(channelName, options ?? new ChatChannelOptions(), chatStateLock);
        }

        public virtual bool TryGetChannel(string channelName, out ChatChannel channel)
        {
            lock (chatStateLock)
            {
                return ChatChannels.TryGetValue(NormalizeChannelName(channelName), out channel);
            }
        }

        protected virtual bool TryGetChatUser(string username, out ChatUserPeerExtension user)
        {
            lock (chatStateLock)
            {
                return ChatUsers.TryGetValue(NormalizeUsername(username), out user);
            }
        }

        protected virtual bool IsChatUsernameTaken(string username)
        {
            lock (chatStateLock)
            {
                return ChatUsers.ContainsKey(NormalizeUsername(username));
            }
        }

        protected virtual bool IsChatChannelNameTaken(string channelName)
        {
            lock (chatStateLock)
            {
                return ChatChannels.ContainsKey(NormalizeChannelName(channelName));
            }
        }

        protected virtual List<ChatChannel> GetChatChannelsSnapshot()
        {
            lock (chatStateLock)
            {
                return ChatChannels.Values.ToList();
            }
        }

        public virtual bool InviteUserToChannel(string channelName, string username, ChatChannelPermission permissions = ChatChannelPermission.DefaultMember)
        {
            return TryGetChannel(channelName, out ChatChannel channel) && channel.InviteUser(NormalizeUsername(username), permissions);
        }

        public virtual bool AddUserToChannel(string channelName, string username, ChatChannelPermission permissions = ChatChannelPermission.DefaultMember)
        {
            username = NormalizeUsername(username);

            if (string.IsNullOrWhiteSpace(username))
                return false;

            if (!TryGetChannel(channelName, out ChatChannel channel))
                return false;

            if (!TryGetChatUser(username, out ChatUserPeerExtension user))
                return false;

            if (user.ContainsChannel(channel))
                return true;

            channel.SetPermissions(username, permissions);
            return channel.AddUser(user);
        }

        public virtual bool RevokeChannelInvite(string channelName, string username)
        {
            return TryGetChannel(channelName, out ChatChannel channel) && channel.RevokeInvite(NormalizeUsername(username));
        }

        public virtual bool SetChannelPermissions(string channelName, string username, ChatChannelPermission permissions)
        {
            if (!TryGetChannel(channelName, out ChatChannel channel))
            {
                return false;
            }

            channel.SetPermissions(NormalizeUsername(username), permissions);
            return true;
        }

        public virtual bool BanUserFromChannel(string channelName, string username)
        {
            return TryGetChannel(channelName, out ChatChannel channel) && channel.BanUser(NormalizeUsername(username));
        }

        public virtual bool RemoveUserFromChannel(string channelName, string username)
        {
            return TryGetChannel(channelName, out ChatChannel channel) && channel.RemoveMember(NormalizeUsername(username));
        }

        public virtual bool UnbanUserFromChannel(string channelName, string username)
        {
            return TryGetChannel(channelName, out ChatChannel channel) && channel.UnbanUser(NormalizeUsername(username));
        }

        protected virtual string NormalizeChannelName(string channelName)
        {
            return (channelName ?? string.Empty).Trim();
        }

        protected virtual string NormalizeUsername(string username)
        {
            return (username ?? string.Empty).Trim();
        }

        /// <summary>
        /// Removes existing chat user from all the channels, and creates a new 
        /// <see cref="ChatUserPeerExtension"/> with new username. If <see cref="joinSameChannels"/> is true, 
        /// user will be added to same channels
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="newUsername"></param>
        /// <param name="joinSameChannels"></param>
        public void ChangeUsername(IPeer peer, string newUsername, bool joinSameChannels = true)
        {
            newUsername = NormalizeUsername(newUsername);

            if (string.IsNullOrWhiteSpace(newUsername) || IsChatUsernameTaken(newUsername))
            {
                return;
            }

            var chatUser = peer.GetExtension<ChatUserPeerExtension>();

            if (!IsChatUserActive(chatUser))
            {
                return;
            }

            // Get previous chat user channels that one is connected to
            var prevChannels = chatUser.GetChannelsSnapshot();
            var prevChannelPermissions = prevChannels.ToDictionary(c => c, c => c.GetPermissions(chatUser.Username));

            // Get his default chat channel
            var defaultChannel = chatUser.DefaultChannel;

            // Remove the user from chat
            RemoveChatUser(chatUser);

            // Create a new chat user
            var newExtension = CreateChatUser(peer, newUsername);

            if (!AddChatUser(newExtension))
            {
                return;
            }

            // Replace with new user
            peer.AddExtension(newExtension);

            if (joinSameChannels)
            {
                foreach (var prevChannel in prevChannels)
                {
                    var channel = GetOrCreateChannel(prevChannel.Name);
                    if (channel != null)
                    {
                        if (prevChannelPermissions.TryGetValue(prevChannel, out ChatChannelPermission permissions))
                        {
                            channel.SetPermissions(newExtension.Username, permissions);
                        }

                        channel.AddUser(newExtension);
                    }
                }

                if (defaultChannel != null && defaultChannel.ContainsUser(newExtension))
                {
                    // If we were added to the chat, which is now set as our default chat
                    // It's safe to set the default channel
                    newExtension.DefaultChannel = defaultChannel;
                }
            }
        }

        /// <summary>
        /// Handles chat message.
        /// Returns true, if message was handled
        /// If it returns false, message sender will receive a "Not Handled" response.
        /// </summary>
        protected virtual bool TryHandleChatMessage(ChatMessagePacket chatMessage, ChatUserPeerExtension sender,
            IIncomingMessage message, CancellationToken cancellationToken, out Task persistenceTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            persistenceTask = Task.CompletedTask;

            try
            {
                // Set a true sender
                chatMessage.Sender = sender.Username;
                ClearClientProvidedPresentation(chatMessage);
                chatMessage.Message = chatMessage.Message ?? string.Empty;

                // Check if message contains a forbidden word
                //if (useCensorModule && censorModule != null && censorModule.HasCensoredWord(chatMessage.Message))
                //{
                //    chatMessage.Receiver = chatMessage.Sender;
                //    chatMessage.Sender = "Admin";
                //    chatMessage.MessageType = ChatMessageType.Private;
                //    chatMessage.Message = "Your text message contains forbidden word. Please be kind to all chat users";
                //}

                switch (chatMessage.MessageType)
                {
                    case ChatMessageType.Channel:
                        chatMessage.Receiver = NormalizeChannelName(chatMessage.Receiver);

                        if (string.IsNullOrEmpty(chatMessage.Receiver))
                        {
                            var defaultChannel = sender.DefaultChannel;

                            // If this is a local chat message (no receiver is provided)
                            if (defaultChannel == null)
                            {
                                message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_DEFAULT_CHANNEL_NOT_SET);
                                return false;
                            }

                            if (!CanSendChannelMessage(sender, defaultChannel, chatMessage, message, out string defaultChannelError))
                            {
                                message.RespondError(ResponseStatus.Error, defaultChannelError);
                                return false;
                            }

                            var outgoingMessage = CreateOutgoingChatMessage(chatMessage);
                            defaultChannel.BroadcastMessage(outgoingMessage);
                            OnMessageAccepted(sender, defaultChannel, outgoingMessage);
                            persistenceTask = PersistAcceptedChatMessage(chatMessage, defaultChannel);
                            LogAcceptedChatMessage("client_channel_default", chatMessage, outgoingMessage, defaultChannel, defaultChannel.UsersCount);
                            message.Respond(ResponseStatus.Success);
                            return true;
                        }

                        // Find the channel
                        if (!TryGetChannel(chatMessage.Receiver, out ChatChannel channel) || !sender.ContainsChannel(channel))
                        {
                            message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_MEMBERSHIP_REQUIRED);
                            return false;
                        }

                        if (!CanSendChannelMessage(sender, channel, chatMessage, message, out string channelError))
                        {
                            message.RespondError(ResponseStatus.Error, channelError);
                            return false;
                        }

                        var channelMessage = CreateOutgoingChatMessage(chatMessage);
                        channel.BroadcastMessage(channelMessage);
                        OnMessageAccepted(sender, channel, channelMessage);
                        persistenceTask = PersistAcceptedChatMessage(chatMessage, channel);
                        LogAcceptedChatMessage("client_channel", chatMessage, channelMessage, channel, channel.UsersCount);
                        message.Respond(ResponseStatus.Success);
                        return true;

                    case ChatMessageType.Private:
                        chatMessage.Receiver = NormalizeUsername(chatMessage.Receiver);

                        if (!CanSendPrivateMessage(sender, chatMessage, message, out string privateMessageError))
                        {
                            message.RespondError(ResponseStatus.Error, privateMessageError);
                            return false;
                        }

                        if (!TryGetChatUser(chatMessage.Receiver, out ChatUserPeerExtension receiver))
                        {
                            message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_USER_NOT_ONLINE);
                            return false;
                        }

                        var privateMessage = CreateOutgoingChatMessage(chatMessage);
                        receiver.Peer.SendMessage(MstOpCodes.ChatMessage, privateMessage);
                        persistenceTask = PersistAcceptedChatMessage(chatMessage);
                        LogAcceptedChatMessage("client_private", chatMessage, privateMessage, null, 1);
                        message.Respond(ResponseStatus.Success);
                        return true;

                    case ChatMessageType.Users:
                        logger.Warn($"Rejected users chat message from client sender={sender.Username}. Users messages can be sent only by trusted server peers");
                        message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_USERS_MESSAGE_FORBIDDEN);
                        return false;
                }

                message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_MESSAGE_TYPE_INVALID);
                return false;
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error($"Chat message handling failed. Error={e}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return false;
            }
        }

        protected virtual bool TrySendServerChatMessage(ServerChatMessagePacket serverPacket, IIncomingMessage request,
            CancellationToken cancellationToken, out string error, out Task persistenceTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            error = string.Empty;
            persistenceTask = Task.CompletedTask;

            serverPacket.Sender = NormalizeUsername(serverPacket.Sender);
            NormalizeServerProvidedPresentation(serverPacket);
            serverPacket.Message ??= string.Empty;

            if (string.IsNullOrWhiteSpace(serverPacket.Sender))
            {
                error = MstErrorCodes.CHAT_SENDER_REQUIRED;
                return false;
            }

            if (!TryGetChatUser(serverPacket.Sender, out ChatUserPeerExtension sender))
            {
                error = MstErrorCodes.CHAT_USER_NOT_ONLINE;
                return false;
            }

            switch (serverPacket.MessageType)
            {
                case ChatMessageType.Channel:
                    return TrySendServerChannelMessage(serverPacket, sender, request, cancellationToken,
                        out error, out persistenceTask);

                case ChatMessageType.Private:
                    return TrySendServerPrivateMessage(serverPacket, sender, request, cancellationToken,
                        out error, out persistenceTask);

                case ChatMessageType.Users:
                    return TrySendServerUsersMessage(serverPacket, sender, request, cancellationToken,
                        out error, out persistenceTask);
            }

            error = MstErrorCodes.CHAT_MESSAGE_TYPE_INVALID;
            return false;
        }

        protected virtual bool TrySendServerChannelMessage(ServerChatMessagePacket serverPacket,
            ChatUserPeerExtension sender, IIncomingMessage request, CancellationToken cancellationToken,
            out string error, out Task persistenceTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            error = string.Empty;
            persistenceTask = Task.CompletedTask;
            string channelName = NormalizeChannelName(serverPacket.Receiver);

            if (string.IsNullOrWhiteSpace(channelName))
            {
                error = MstErrorCodes.CHAT_CHANNEL_REQUIRED;
                return false;
            }

            if (!TryGetChannel(channelName, out ChatChannel channel))
            {
                error = MstErrorCodes.CHAT_CHANNEL_NOT_FOUND;
                return false;
            }

            if (!sender.ContainsChannel(channel))
            {
                error = MstErrorCodes.CHAT_CHANNEL_MEMBERSHIP_REQUIRED;
                return false;
            }

            var chatMessage = serverPacket.ToChatMessagePacket();
            chatMessage.MessageType = ChatMessageType.Channel;
            chatMessage.Receiver = channel.Name;
            chatMessage.Sender = sender.Username;
            chatMessage.Message ??= string.Empty;

            if (!CanSendChannelMessage(sender, channel, chatMessage, request, out error))
                return false;

            var outgoingMessage = CreateOutgoingChatMessage(chatMessage);
            channel.BroadcastMessage(outgoingMessage);

            OnMessageAccepted(sender, channel, outgoingMessage);
            persistenceTask = PersistAcceptedChatMessage(chatMessage, channel);
            LogAcceptedChatMessage("server_channel", chatMessage, outgoingMessage, channel, channel.UsersCount);
            logger.Debug($"Server channel chat message sent to channel {channel.Name}");
            return true;
        }

        protected virtual bool TrySendServerPrivateMessage(ServerChatMessagePacket serverPacket,
            ChatUserPeerExtension sender, IIncomingMessage request, CancellationToken cancellationToken,
            out string error, out Task persistenceTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            error = string.Empty;
            persistenceTask = Task.CompletedTask;
            string receiverName = NormalizeUsername(serverPacket.Receiver);

            if (string.IsNullOrWhiteSpace(receiverName))
            {
                error = MstErrorCodes.CHAT_RECEIVER_REQUIRED;
                return false;
            }

            if (!TryGetChatUser(receiverName, out ChatUserPeerExtension receiver))
            {
                error = MstErrorCodes.CHAT_USER_NOT_ONLINE;
                return false;
            }

            var chatMessage = serverPacket.ToChatMessagePacket();
            chatMessage.MessageType = ChatMessageType.Private;
            chatMessage.Receiver = receiver.Username;
            chatMessage.Sender = sender.Username;
            chatMessage.Message ??= string.Empty;

            if (!CanSendPrivateMessage(sender, chatMessage, request, out error))
                return false;

            var outgoingMessage = CreateOutgoingChatMessage(chatMessage);
            receiver.Peer.SendMessage(MstOpCodes.ChatMessage, outgoingMessage);

            if (!string.Equals(sender.Username, receiver.Username, StringComparison.OrdinalIgnoreCase))
                sender.Peer.SendMessage(MstOpCodes.ChatMessage, outgoingMessage);

            persistenceTask = PersistAcceptedChatMessage(chatMessage);
            LogAcceptedChatMessage("server_private", chatMessage, outgoingMessage, null, 1);
            logger.Debug($"Server private chat message sent from {sender.Username} to {receiver.Username}");
            return true;
        }

        protected virtual bool TrySendServerUsersMessage(ServerChatMessagePacket serverPacket,
            ChatUserPeerExtension sender, IIncomingMessage request, CancellationToken cancellationToken,
            out string error, out Task persistenceTask)
        {
            cancellationToken.ThrowIfCancellationRequested();
            error = string.Empty;
            persistenceTask = Task.CompletedTask;
            var recipients = GetNormalizedRecipients(serverPacket.Recipients);

            if (recipients.Count == 0)
            {
                error = MstErrorCodes.CHAT_RECIPIENTS_REQUIRED;
                return false;
            }

            var chatMessage = serverPacket.ToChatMessagePacket();
            chatMessage.MessageType = ChatMessageType.Users;
            chatMessage.Receiver = NormalizeChannelName(serverPacket.Receiver);
            chatMessage.Sender = sender.Username;
            chatMessage.Message ??= string.Empty;

            if (!CanSendUsersMessage(sender, chatMessage, request, out error))
                return false;

            var displayMessage = CreateOutgoingChatMessage(chatMessage);
            var outgoingMessage = MessageHelper.Create(MstOpCodes.ChatMessage, displayMessage);
            int sentCount = 0;

            foreach (string recipientName in recipients)
            {
                if (!TryGetChatUser(recipientName, out ChatUserPeerExtension recipient))
                    continue;

                recipient.Peer.SendMessage(outgoingMessage, DeliveryMethod.ReliableFragmentedSequenced);
                sentCount++;
            }

            if (sentCount == 0)
            {
                error = MstErrorCodes.CHAT_RECIPIENTS_OFFLINE;
                return false;
            }

            persistenceTask = PersistAcceptedChatMessage(chatMessage);
            LogAcceptedChatMessage("server_users", chatMessage, displayMessage, null, sentCount);
            logger.Debug($"Server users chat message sent to {sentCount} user(s)");
            return true;
        }

        protected virtual HashSet<string> GetNormalizedRecipients(IEnumerable<string> recipients)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (recipients == null)
                return result;

            foreach (string recipient in recipients)
            {
                string username = NormalizeUsername(recipient);

                if (!string.IsNullOrWhiteSpace(username))
                    result.Add(username);
            }

            return result;
        }

        protected virtual bool CanJoinChannel(ChatUserPeerExtension user, ChatChannel channel, IIncomingMessage message, out string error)
        {
            error = string.Empty;

            if (channel == null)
            {
                error = MstErrorCodes.CHAT_CHANNEL_NOT_FOUND;
                return false;
            }

            if (!channel.HasPermission(user, ChatChannelPermission.Join))
            {
                error = MstErrorCodes.CHAT_CHANNEL_PERMISSION_REQUIRED;
                return false;
            }

            return true;
        }

        protected virtual bool CanCreateChannel(ChatUserPeerExtension user, ChatChannelOptionsPacket packet, IIncomingMessage message, out string error)
        {
            error = string.Empty;

            if (user == null)
            {
                error = MstErrorCodes.CHAT_USER_NOT_IDENTIFIED;
                return false;
            }

            if (packet == null || string.IsNullOrWhiteSpace(packet.ChannelName))
            {
                error = MstErrorCodes.CHAT_CHANNEL_REQUIRED;
                return false;
            }

            if (IsChatChannelNameTaken(packet.ChannelName))
            {
                error = MstErrorCodes.CHAT_CHANNEL_ALREADY_EXISTS;
                return false;
            }

            if (!Enum.IsDefined(typeof(ChatChannelType), packet.Type) || packet.Type == ChatChannelType.System)
            {
                error = MstErrorCodes.CHAT_CHANNEL_TYPE_FORBIDDEN;
                return false;
            }

            if (!Enum.IsDefined(typeof(ChatHistoryVisibility), packet.HistoryVisibility))
            {
                error = MstErrorCodes.CHAT_HISTORY_VISIBILITY_INVALID;
                return false;
            }

            if (packet.MaxHistory < 0)
            {
                error = MstErrorCodes.CHAT_HISTORY_LIMIT_INVALID;
                return false;
            }

            if ((packet.DefaultMemberPermissions & ~ChatChannelPermission.DefaultMember) != ChatChannelPermission.None)
            {
                error = MstErrorCodes.CHAT_DEFAULT_PERMISSIONS_INVALID;
                return false;
            }

            var restrictedInvitedPermissions = ChatChannelPermission.Ban | ChatChannelPermission.ManageChannel;

            if ((packet.InvitedMemberPermissions & restrictedInvitedPermissions) != ChatChannelPermission.None)
            {
                error = MstErrorCodes.CHAT_INVITED_PERMISSIONS_INVALID;
                return false;
            }

            return true;
        }

        protected virtual bool HasPermissionToUseServerChatApi(IPeer peer)
        {
            var extension = peer.GetExtension<SecurityInfoPeerExtension>();
            return extension != null &&
                   (extension.HasPermission(MstPermissionKeys.RoomServer) ||
                    extension.HasAccountPermission(MstPermissionLevels.Admin));
        }

        protected virtual void LogRejectedServerChatApi(IPeer peer, string action)
        {
            var extension = peer.GetExtension<SecurityInfoPeerExtension>();
            int permissionLevel = extension != null ? extension.PermissionLevel : MstPermissionLevels.Default;
            logger.Warn($"Chat server API denied action={action} peerId={peer.Id} permissionLevel={permissionLevel} requiredPermission={MstPermissionKeys.RoomServer}");
        }

        protected virtual bool CanSendChannelMessage(ChatUserPeerExtension sender, ChatChannel channel, ChatMessagePacket packet, IIncomingMessage message, out string error)
        {
            error = string.Empty;

            if (channel == null)
            {
                error = MstErrorCodes.CHAT_CHANNEL_NOT_FOUND;
                return false;
            }

            if (!channel.HasPermission(sender, ChatChannelPermission.Send))
            {
                error = MstErrorCodes.CHAT_CHANNEL_PERMISSION_REQUIRED;
                return false;
            }

            return true;
        }

        protected virtual bool CanManageChannelUser(ChatUserPeerExtension actor, ChatChannel channel, string targetUsername, ChatChannelPermission requiredPermission, IIncomingMessage message, out string error)
        {
            error = string.Empty;
            targetUsername = NormalizeUsername(targetUsername);

            if (channel == null)
            {
                error = MstErrorCodes.CHAT_CHANNEL_NOT_FOUND;
                return false;
            }

            if (actor == null)
            {
                error = MstErrorCodes.CHAT_USER_NOT_IDENTIFIED;
                return false;
            }

            if (string.IsNullOrWhiteSpace(targetUsername))
            {
                error = MstErrorCodes.CHAT_TARGET_USERNAME_REQUIRED;
                return false;
            }

            if (!channel.HasPermission(actor, requiredPermission))
            {
                error = MstErrorCodes.CHAT_CHANNEL_PERMISSION_REQUIRED;
                return false;
            }

            if (channel.IsOwner(targetUsername) && !channel.IsOwner(actor))
            {
                error = MstErrorCodes.CHAT_OWNER_TARGET_PROTECTED;
                return false;
            }

            return true;
        }

        protected virtual bool CanGrantChannelPermissions(ChatUserPeerExtension actor, ChatChannel channel, ChatChannelPermission requestedPermissions, IIncomingMessage message, out string error)
        {
            error = string.Empty;

            if (channel == null)
            {
                error = MstErrorCodes.CHAT_CHANNEL_NOT_FOUND;
                return false;
            }

            if (channel.IsOwner(actor))
            {
                return true;
            }

            var actorPermissions = channel.GetPermissions(actor?.Username);

            if ((requestedPermissions & ~actorPermissions) != ChatChannelPermission.None)
            {
                error = MstErrorCodes.CHAT_PERMISSION_GRANT_EXCEEDS_OWN;
                return false;
            }

            var restrictedPermissions = ChatChannelPermission.ManageChannel | ChatChannelPermission.Ban;

            if ((requestedPermissions & restrictedPermissions) != ChatChannelPermission.None)
            {
                error = MstErrorCodes.CHAT_OWNER_PERMISSION_REQUIRED;
                return false;
            }

            return true;
        }

        protected virtual bool CanSendPrivateMessage(ChatUserPeerExtension sender, ChatMessagePacket packet, IIncomingMessage message, out string error)
        {
            error = string.Empty;
            return true;
        }

        protected virtual bool CanSendUsersMessage(ChatUserPeerExtension sender, ChatMessagePacket packet, IIncomingMessage message, out string error)
        {
            error = string.Empty;
            return true;
        }

        protected virtual bool CanViewChannelUsers(ChatUserPeerExtension user, ChatChannel channel, IIncomingMessage message, out string error)
        {
            error = string.Empty;

            if (channel == null)
            {
                error = MstErrorCodes.CHAT_CHANNEL_NOT_FOUND;
                return false;
            }

            if (!channel.HasPermission(user, ChatChannelPermission.ViewUsers))
            {
                error = MstErrorCodes.CHAT_CHANNEL_PERMISSION_REQUIRED;
                return false;
            }

            if (channel.IsClosed && !user.ContainsChannel(channel))
            {
                error = MstErrorCodes.CHAT_CHANNEL_MEMBERSHIP_REQUIRED;
                return false;
            }

            return true;
        }

        protected virtual bool CanSetDefaultChannel(ChatUserPeerExtension user, ChatChannel channel, IIncomingMessage message, out string error)
        {
            return CanJoinChannel(user, channel, message, out error);
        }

        protected virtual void OnMessageAccepted(ChatUserPeerExtension sender, ChatChannel channel, ChatMessagePacket packet)
        {
        }

        protected virtual Task PersistAcceptedChatMessage(ChatMessagePacket packet, ChatChannel channel = null)
        {
            if (packet == null)
                return Task.CompletedTask;

            IChatDatabaseAccessor chatDatabaseAccessor = GetChatDatabaseAccessor();

            if (chatDatabaseAccessor == null)
            {
                logger.Debug($"Chat persistence skipped type={packet.MessageType} sender={packet.Sender} receiver={packet.Receiver} reason=no_accessor");
                return Task.CompletedTask;
            }

            ChatMessageInfo messageInfo;

            try
            {
                messageInfo = ChatMessageInfo.FromPacket(packet);

                if (messageInfo.MessageType == ChatMessageType.Channel && string.IsNullOrWhiteSpace(messageInfo.Receiver) && channel != null)
                    messageInfo.Receiver = NormalizeChannelName(channel.Name);
            }
            catch (Exception e)
            {
                logger.Error("Failed to prepare chat message for persistence");
                logger.Error(e);
                return Task.CompletedTask;
            }

            logger.Debug($"Chat persistence queued type={messageInfo.MessageType} sender={messageInfo.Sender} receiver={messageInfo.Receiver} rawLength={messageInfo.Message.Length}");
            return SaveAcceptedChatMessageAsync(messageInfo);
        }

        protected virtual async Task SaveAcceptedChatMessageAsync(ChatMessageInfo messageInfo)
        {
            try
            {
                IChatDatabaseAccessor chatDatabaseAccessor = GetChatDatabaseAccessor();

                if (chatDatabaseAccessor == null)
                    return;

                // Delivery already happened, so persistence must reach a terminal result during shutdown.
                await chatDatabaseAccessor.SaveMessageAsync(messageInfo, CancellationToken.None);
                logger.Debug($"Chat persistence saved type={messageInfo.MessageType} sender={messageInfo.Sender} receiver={messageInfo.Receiver} rawLength={messageInfo.Message.Length}");
            }
            catch (Exception e)
            {
                logger.Error("Failed to save chat message");
                logger.Error(e);
            }
        }

        protected virtual IChatDatabaseAccessor GetChatDatabaseAccessor()
        {
            lock (chatStateLock)
            {
                if (databaseAccessor == null)
                    databaseAccessor = Mst.Server.DbAccessors.GetAccessor<IChatDatabaseAccessor>();

                return databaseAccessor;
            }
        }

        protected virtual ChatChannelInfoPacket CreateChannelInfoPacket(ChatChannel channel, ChatUserPeerExtension currentUser)
        {
            string username = NormalizeUsername(currentUser?.Username);

            return new ChatChannelInfoPacket
            {
                Name = channel.Name,
                OnlineCount = channel.UsersCount,
                Type = channel.Type,
                IsClosed = channel.IsClosed,
                IsArchived = channel.IsArchived,
                OwnerUsername = channel.OwnerUsername,
                IsInvited = channel.IsInvited(username),
                IsBanned = channel.IsBanned(username),
                CurrentUserPermissions = channel.GetPermissions(username)
            };
        }

        protected virtual bool TryGetChatUser(IIncomingMessage message, out ChatUserPeerExtension chatUser)
        {
            chatUser = message.Peer.GetExtension<ChatUserPeerExtension>();

            if (chatUser == null)
            {
                message.RespondError(ResponseStatus.Unauthorized, MstErrorCodes.CHAT_USER_NOT_IDENTIFIED);
                return false;
            }

            if (!IsChatUserActive(chatUser))
            {
                chatUser = null;
                message.RespondError(ResponseStatus.Unauthorized, MstErrorCodes.CHAT_USER_NOT_IDENTIFIED);
                return false;
            }

            return true;
        }

        protected virtual bool IsChatUserActive(ChatUserPeerExtension chatUser)
        {
            if (chatUser == null)
                return false;

            lock (chatStateLock)
            {
                string username = NormalizeUsername(chatUser.Username);
                return ChatUsers.TryGetValue(username, out ChatUserPeerExtension currentUser) && currentUser == chatUser;
            }
        }

        protected virtual void CensorMessage(ChatMessagePacket chatMessage)
        {
            if (useCensorModule && censorModule != null)
            {
                string rawMessage = chatMessage.Message;
                chatMessage.Message = censorModule.CensorMessage(chatMessage.Message);

                if (!string.Equals(rawMessage, chatMessage.Message, StringComparison.Ordinal))
                    logger.Debug($"Chat censor applied type={chatMessage.MessageType} sender={chatMessage.Sender} receiver={chatMessage.Receiver} rawLength={rawMessage?.Length ?? 0} censoredLength={chatMessage.Message?.Length ?? 0}");
            }
        }

        protected virtual ChatMessagePacket CreateOutgoingChatMessage(ChatMessagePacket source)
        {
            var outgoingMessage = CopyChatMessage(source);
            CensorMessage(outgoingMessage);
            return outgoingMessage;
        }

        protected virtual void LogAcceptedChatMessage(string route, ChatMessagePacket rawPacket, ChatMessagePacket outgoingPacket, ChatChannel channel, int recipientsCount)
        {
            if (rawPacket == null || outgoingPacket == null)
                return;

            bool censored = !string.Equals(rawPacket.Message, outgoingPacket.Message, StringComparison.Ordinal);
            string channelName = channel != null ? channel.Name : string.Empty;
            logger.Debug($"Chat message accepted route={route} type={rawPacket.MessageType} sender={rawPacket.Sender} receiver={rawPacket.Receiver} channel={channelName} recipients={recipientsCount} rawLength={rawPacket.Message?.Length ?? 0} outgoingLength={outgoingPacket.Message?.Length ?? 0} censored={censored}");
        }

        protected virtual ChatMessagePacket CopyChatMessage(ChatMessagePacket source)
        {
            return new ChatMessagePacket
            {
                MessageType = source.MessageType,
                Receiver = source.Receiver,
                Sender = source.Sender,
                SenderDisplayName = source.SenderDisplayName,
                ReceiverDisplayName = source.ReceiverDisplayName,
                SenderAvatar = source.SenderAvatar,
                ReceiverAvatar = source.ReceiverAvatar,
                Message = source.Message
            };
        }

        protected virtual void ClearClientProvidedPresentation(ChatMessagePacket message)
        {
            message.SenderDisplayName = string.Empty;
            message.ReceiverDisplayName = string.Empty;
            message.SenderAvatar = string.Empty;
            message.ReceiverAvatar = string.Empty;
        }

        protected virtual void NormalizeServerProvidedPresentation(ServerChatMessagePacket message)
        {
            message.SenderDisplayName = NormalizeDisplayName(message.SenderDisplayName);
            message.ReceiverDisplayName = NormalizeDisplayName(message.ReceiverDisplayName);
            message.SenderAvatar = NormalizeAvatar(message.SenderAvatar);
            message.ReceiverAvatar = NormalizeAvatar(message.ReceiverAvatar);
        }

        protected virtual string NormalizeDisplayName(string displayName)
        {
            displayName = displayName?.Trim() ?? string.Empty;

            return displayName.Length <= ChatMessagePacket.MaxDisplayNameLength
                ? displayName
                : displayName.Substring(0, ChatMessagePacket.MaxDisplayNameLength);
        }

        protected virtual string NormalizeAvatar(string avatar)
        {
            avatar = avatar?.Trim() ?? string.Empty;

            if (avatar.Length == 0 || avatar.Length > ChatMessagePacket.MaxAvatarLength)
                return string.Empty;

            if (!Uri.TryCreate(avatar, UriKind.Absolute, out Uri uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(uri.Host))
            {
                return string.Empty;
            }

            return avatar;
        }

        #region Event Handlers

        /// <summary>
        /// Fired when new user logged in and <see cref="useAuthModule"/> is set to true
        /// </summary>
        /// <param name="userPeerExtension"></param>
        protected virtual Task OnUserLoggedInEventHandler(IUserPeerExtension userPeerExtension,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Create new chat user
            var chatUser = CreateChatUser(userPeerExtension.Peer, userPeerExtension.Username);

            // Add him to chat users list
            if (AddChatUser(chatUser))
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Add the extension
                    userPeerExtension.Peer.AddExtension(chatUser);
                }
                catch (OperationCanceledException)
                {
                    RemoveChatUser(chatUser);
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Fired if existing user logged out and <see cref="useAuthModule"/> is set to true
        /// </summary>
        /// <param name="userPeerExtension"></param>
        protected virtual void OnUserLoggedOutEventHandler(IUserPeerExtension userPeerExtension)
        {
            // Get chat user from extensions list
            var chatUser = userPeerExtension.Peer.GetExtension<ChatUserPeerExtension>();
            logger.Debug($"Chat user logout username={userPeerExtension.Username} hasChatIdentity={chatUser != null}");

            // Remove it from chat users list if is not null
            if (chatUser != null)
            {
                RemoveChatUser(chatUser);
            }
        }

        /// <summary>
        /// Fired if existing client disconnected
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnClientDisconnected(IPeer peer)
        {
            peer.OnConnectionCloseEvent -= OnClientDisconnected;

            var chatUser = peer.GetExtension<ChatUserPeerExtension>();
            logger.Debug($"Chat peer disconnected peerId={peer.Id} hasChatIdentity={chatUser != null}");

            if (chatUser != null)
            {
                RemoveChatUser(chatUser);
            }
        }

        #endregion

        #region Message Handlers

        protected virtual Task OnCreateChatChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var packet = message.AsPacket<ChatChannelOptionsPacket>();
                if (packet != null)
                    packet.ChannelName = NormalizeChannelName(packet.ChannelName);

                if (!CanCreateChannel(chatUser, packet, message, out string errorCode))
                {
                    message.RespondError(ResponseStatus.Forbidden, errorCode);
                    return Task.CompletedTask;
                }

                var channel = GetOrCreateChannel(packet.ChannelName, packet.ToOptions(chatUser.Username));

                if (channel == null)
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_CREATE_FAILED);
                    return Task.CompletedTask;
                }

                channel.SetPermissions(chatUser.Username, channel.OwnerPermissions);

                if (!chatUser.ContainsChannel(channel) && !channel.AddUser(chatUser))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_JOIN_FAILED);
                    return Task.CompletedTask;
                }

                if (setFirstChannelAsLocal && chatUser.ChannelsCount == 1)
                {
                    chatUser.DefaultChannel = channel;
                }

                message.Respond(CreateChannelInfoPacket(channel, chatUser), ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnServerEnsureChatChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!HasPermissionToUseServerChatApi(message.Peer))
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_SERVER_API_FORBIDDEN);
                    LogRejectedServerChatApi(message.Peer, "ensure_channel");
                    return Task.CompletedTask;
                }

                var packet = message.AsPacket<ChatChannelOptionsPacket>();
                if (packet == null || string.IsNullOrWhiteSpace(packet.ChannelName))
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_REQUIRED);
                    return Task.CompletedTask;
                }

                packet.ChannelName = NormalizeChannelName(packet.ChannelName);

                if (!Enum.IsDefined(typeof(ChatChannelType), packet.Type))
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_TYPE_INVALID);
                    return Task.CompletedTask;
                }

                if (!Enum.IsDefined(typeof(ChatHistoryVisibility), packet.HistoryVisibility))
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_HISTORY_VISIBILITY_INVALID);
                    return Task.CompletedTask;
                }

                if (packet.MaxHistory < 0)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_HISTORY_LIMIT_INVALID);
                    return Task.CompletedTask;
                }

                var channel = GetOrCreateChannel(packet.ChannelName, packet.ToOptions(string.Empty));

                if (channel == null)
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_ENSURE_FAILED);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                logger.Debug($"Chat server API ensure_channel channel={packet.ChannelName} type={packet.Type}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnServerAddChatUserToChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!HasPermissionToUseServerChatApi(message.Peer))
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_SERVER_API_FORBIDDEN);
                    LogRejectedServerChatApi(message.Peer, "add_user_to_channel");
                    return Task.CompletedTask;
                }

                var packet = message.AsPacket<ChatChannelUserPacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED);
                    return Task.CompletedTask;
                }

                packet.ChannelName = NormalizeChannelName(packet.ChannelName);
                packet.Username = NormalizeUsername(packet.Username);

                if (string.IsNullOrWhiteSpace(packet.ChannelName) || string.IsNullOrWhiteSpace(packet.Username))
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_AND_USERNAME_REQUIRED);
                    return Task.CompletedTask;
                }

                if (!AddUserToChannel(packet.ChannelName, packet.Username, packet.Permissions))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_USER_ADD_FAILED);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                logger.Debug($"Chat server API add_user_to_channel channel={packet.ChannelName} username={packet.Username} permissions={packet.Permissions}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnServerRemoveChatUserFromChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!HasPermissionToUseServerChatApi(message.Peer))
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_SERVER_API_FORBIDDEN);
                    LogRejectedServerChatApi(message.Peer, "remove_user_from_channel");
                    return Task.CompletedTask;
                }

                var packet = message.AsPacket<ChatChannelUserPacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED);
                    return Task.CompletedTask;
                }

                packet.ChannelName = NormalizeChannelName(packet.ChannelName);
                packet.Username = NormalizeUsername(packet.Username);

                if (string.IsNullOrWhiteSpace(packet.ChannelName) || string.IsNullOrWhiteSpace(packet.Username))
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_AND_USERNAME_REQUIRED);
                    return Task.CompletedTask;
                }

                if (!RemoveUserFromChannel(packet.ChannelName, packet.Username))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_USER_REMOVE_FAILED);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                logger.Debug($"Chat server API remove_user_from_channel channel={packet.ChannelName} username={packet.Username}");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual async Task OnServerSendChatMessageToUsersRequestHandler(IIncomingMessage message,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!HasPermissionToUseServerChatApi(message.Peer))
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_SERVER_API_FORBIDDEN);
                    LogRejectedServerChatApi(message.Peer, "send_server_chat");
                    return;
                }

                var packet = message.AsPacket<ServerChatMessagePacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_SERVER_MESSAGE_PACKET_REQUIRED);
                    return;
                }

                if (!TrySendServerChatMessage(packet, message, cancellationToken,
                    out string error, out Task persistenceTask))
                {
                    message.RespondError(ResponseStatus.Error, error);
                    return;
                }

                message.Respond(ResponseStatus.Success);
                logger.Debug($"Chat server API send_server_chat type={packet.MessageType} sender={packet.Sender} receiver={packet.Receiver} recipients={packet.Recipients?.Count ?? 0}");
                await persistenceTask;
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        protected virtual Task OnGetChatChannelInfoRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var channelName = NormalizeChannelName(message.AsString());

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!channel.HasPermission(chatUser, ChatChannelPermission.View))
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_CHANNEL_PERMISSION_REQUIRED);
                    return Task.CompletedTask;
                }

                message.Respond(CreateChannelInfoPacket(channel, chatUser), ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnGetChatInvitesRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var channels = GetChatChannelsSnapshot()
                    .Where(channel => channel.IsInvited(chatUser.Username))
                    .Select(channel => new ChatChannelInfo
                    {
                        Name = channel.Name,
                        OnlineCount = channel.UsersCount
                    })
                    .ToList();

                message.Respond(new ChatChannelsListPacket { Channels = channels }, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnInviteToChatChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var packet = message.AsPacket<ChatChannelUserPacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED);
                    return Task.CompletedTask;
                }

                var channelName = NormalizeChannelName(packet.ChannelName);
                var targetUsername = NormalizeUsername(packet.Username);

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!CanManageChannelUser(chatUser, channel, targetUsername, ChatChannelPermission.Invite, message, out string error)
                    || !CanGrantChannelPermissions(chatUser, channel, packet.Permissions, message, out error))
                {
                    message.RespondError(ResponseStatus.Forbidden, error);
                    return Task.CompletedTask;
                }

                if (!channel.InviteUser(targetUsername, packet.Permissions))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_INVITE_FAILED);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnRevokeChatInviteRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var packet = message.AsPacket<ChatChannelUserPacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED);
                    return Task.CompletedTask;
                }

                var channelName = NormalizeChannelName(packet.ChannelName);
                var targetUsername = NormalizeUsername(packet.Username);

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!CanManageChannelUser(chatUser, channel, targetUsername, ChatChannelPermission.Invite, message, out string error))
                {
                    message.RespondError(ResponseStatus.Forbidden, error);
                    return Task.CompletedTask;
                }

                channel.RevokeInvite(targetUsername);
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnAcceptChatInviteRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var channelName = NormalizeChannelName(message.AsString());

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!CanJoinChannel(chatUser, channel, message, out string error))
                {
                    message.RespondError(ResponseStatus.Forbidden, error);
                    return Task.CompletedTask;
                }

                if (!chatUser.ContainsChannel(channel) && !channel.AddUser(chatUser))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_JOIN_FAILED);
                    return Task.CompletedTask;
                }

                if (setFirstChannelAsLocal && chatUser.ChannelsCount == 1)
                {
                    chatUser.DefaultChannel = channel;
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnDeclineChatInviteRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var channelName = NormalizeChannelName(message.AsString());

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                channel.RevokeInvite(chatUser.Username);
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnKickChatUserRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var packet = message.AsPacket<ChatChannelUserPacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED);
                    return Task.CompletedTask;
                }

                var channelName = NormalizeChannelName(packet.ChannelName);
                var targetUsername = NormalizeUsername(packet.Username);

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!CanManageChannelUser(chatUser, channel, targetUsername, ChatChannelPermission.Kick, message, out string error))
                {
                    message.RespondError(ResponseStatus.Forbidden, error);
                    return Task.CompletedTask;
                }

                channel.RemoveMember(targetUsername);
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnBanChatUserRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var packet = message.AsPacket<ChatChannelUserPacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED);
                    return Task.CompletedTask;
                }

                var channelName = NormalizeChannelName(packet.ChannelName);
                var targetUsername = NormalizeUsername(packet.Username);

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (string.Equals(NormalizeUsername(chatUser.Username), targetUsername, StringComparison.OrdinalIgnoreCase))
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_CANNOT_BAN_SELF);
                    return Task.CompletedTask;
                }

                if (!CanManageChannelUser(chatUser, channel, targetUsername, ChatChannelPermission.Ban, message, out string error))
                {
                    message.RespondError(ResponseStatus.Forbidden, error);
                    return Task.CompletedTask;
                }

                if (!channel.BanUser(targetUsername))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_BAN_FAILED);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnUnbanChatUserRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var packet = message.AsPacket<ChatChannelUserPacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED);
                    return Task.CompletedTask;
                }

                var channelName = NormalizeChannelName(packet.ChannelName);
                var targetUsername = NormalizeUsername(packet.Username);

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!CanManageChannelUser(chatUser, channel, targetUsername, ChatChannelPermission.Ban, message, out string error))
                {
                    message.RespondError(ResponseStatus.Forbidden, error);
                    return Task.CompletedTask;
                }

                channel.UnbanUser(targetUsername);
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnSetChatUserPermissionsRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var packet = message.AsPacket<ChatChannelUserPacket>();
                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_CHANNEL_USER_PACKET_REQUIRED);
                    return Task.CompletedTask;
                }

                var channelName = NormalizeChannelName(packet.ChannelName);
                var targetUsername = NormalizeUsername(packet.Username);

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!CanManageChannelUser(chatUser, channel, targetUsername, ChatChannelPermission.ManageChannel, message, out string error)
                    || !CanGrantChannelPermissions(chatUser, channel, packet.Permissions, message, out error))
                {
                    message.RespondError(ResponseStatus.Forbidden, error);
                    return Task.CompletedTask;
                }

                channel.SetPermissions(targetUsername, packet.Permissions);
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnPickUsernameRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!allowUsernamePicking)
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_USERNAME_PICKING_DISABLED);
                    return Task.CompletedTask;
                }

                var username = NormalizeUsername(message.AsString());

                if (string.IsNullOrWhiteSpace(username))
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_USERNAME_REQUIRED);
                    return Task.CompletedTask;
                }

                if (username.Contains(" "))
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_USERNAME_WHITESPACE_INVALID);
                    return Task.CompletedTask;
                }

                var chatUser = message.Peer.GetExtension<ChatUserPeerExtension>();

                if (chatUser != null)
                {
                    if (IsChatUserActive(chatUser))
                    {
                        message.RespondError(ResponseStatus.Conflict, MstErrorCodes.CHAT_USER_ALREADY_IDENTIFIED);
                        return Task.CompletedTask;
                    }

                    RemoveChatUser(chatUser);
                }

                if (IsChatUsernameTaken(username))
                {
                    message.RespondError(ResponseStatus.AlreadyExists, MstErrorCodes.CHAT_USERNAME_ALREADY_EXISTS);
                    return Task.CompletedTask;
                }

                chatUser = CreateChatUser(message.Peer, username);

                if (!AddChatUser(chatUser))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_USER_ADD_FAILED);
                    return Task.CompletedTask;
                }

                // Add the extension
                message.Peer.AddExtension(chatUser);

                // Send response
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnJoinChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                // Get channel name
                var channelName = NormalizeChannelName(message.AsString());

                // Trying to create channel with given name
                var channel = GetOrCreateChannel(channelName);

                if (!CanJoinChannel(chatUser, channel, message, out string joinError))
                {
                    message.RespondError(ResponseStatus.Error, joinError);
                    return Task.CompletedTask;
                }

                if (channel == null || !channel.AddUser(chatUser))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_JOIN_FAILED);
                    return Task.CompletedTask;
                }

                if (setFirstChannelAsLocal && chatUser.ChannelsCount == 1)
                {
                    chatUser.DefaultChannel = channel;
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnLeaveChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                // Get channel name
                var channelName = NormalizeChannelName(message.AsString());

                // Trying to get channel by name
                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                // Remove user from channel
                channel.RemoveUser(chatUser);

                if (setLastChannelAsLocal && chatUser.ChannelsCount == 1)
                {
                    chatUser.DefaultChannel = chatUser.FirstChannelOrDefault();
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnSetDefaultChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var channelName = NormalizeChannelName(message.AsString());
                var channel = GetOrCreateChannel(channelName);

                if (channel == null)
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.CHAT_CHANNEL_PERMISSION_REQUIRED);
                    return Task.CompletedTask;
                }

                if (!CanSetDefaultChannel(chatUser, channel, message, out string defaultChannelError))
                {
                    message.RespondError(ResponseStatus.Error, defaultChannelError);
                    return Task.CompletedTask;
                }

                if (!chatUser.ContainsChannel(channel) && !channel.AddUser(chatUser))
                {
                    message.RespondError(ResponseStatus.Error, MstErrorCodes.CHAT_CHANNEL_JOIN_FAILED);
                    return Task.CompletedTask;
                }

                // Set the property of default chat channel
                chatUser.DefaultChannel = channel;

                // Respond with a "success" status
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual Task OnGetUsersInChannelRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var channelName = NormalizeChannelName(message.AsString());

                if (!TryGetChannel(channelName, out ChatChannel channel))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.CHAT_CHANNEL_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!CanViewChannelUsers(chatUser, channel, message, out string viewUsersError))
                {
                    message.RespondError(ResponseStatus.Error, viewUsersError);
                    return Task.CompletedTask;
                }

                var users = channel.GetUsernamesSnapshot();
                message.Respond(users.ToBytes(), ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        protected virtual async Task OnChatMessageHandler(IIncomingMessage message,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return;

                var packet = message.AsPacket<ChatMessagePacket>();

                if (packet == null)
                {
                    message.RespondError(ResponseStatus.Invalid, MstErrorCodes.CHAT_MESSAGE_TYPE_INVALID);
                    return;
                }

                if (!TryHandleChatMessage(packet, chatUser, message, cancellationToken,
                    out Task persistenceTask))
                    return;

                await persistenceTask;
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        protected virtual Task OnGetCurrentChannelsRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!TryGetChatUser(message, out ChatUserPeerExtension chatUser))
                    return Task.CompletedTask;

                var channels = chatUser.GetChannelsSnapshot().Select(c =>
                {
                    return new ChatChannelInfo
                    {
                        Name = c.Name,
                        OnlineCount = c.UsersCount
                    };
                });

                message.Respond(new ChatChannelsListPacket()
                {
                    Channels = channels.ToList()
                }, ResponseStatus.Success);

                return Task.CompletedTask;
            }

            catch (Exception ex)
            {
                logger.Error($"Chat request failed. Error={ex}");
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
                return Task.CompletedTask;
            }
        }

        #endregion
    }
}
