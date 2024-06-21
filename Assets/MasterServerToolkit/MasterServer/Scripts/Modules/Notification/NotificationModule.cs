using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class NotificationModule : BaseServerModule
    {
        /// <summary>
        /// If true, notification module will subscribe to auth module, and automatically setup recipients when they log in
        /// </summary>
        [Header("General Settings")]
        [SerializeField, Tooltip("If true, notification module will subscribe to auth module, and automatically setup recipients when they log in")]
        protected bool useAuthModule = true;
        [SerializeField, Tooltip("If true, notification module will subscribe to rooms module to be able to send notifications to room players")]
        protected bool useRoomsModule = true;
        [SerializeField, Tooltip("Permission level to be able to send notifications")]
        protected int notifyPermissionLevel = 1;
        [SerializeField]
        private int maxPromisedMessages = 10;

        /// <summary>
        /// List of recipients
        /// </summary>
        protected ConcurrentDictionary<string, NotificationRecipient> registeredRecipients = new ConcurrentDictionary<string, NotificationRecipient>();

        /// <summary>
        /// List of messages to be sent to newly logged in users
        /// </summary>
        protected List<string> promisedMessages = new List<string>();

        /// <summary>
        /// 
        /// </summary>
        protected AuthModule authModule;

        /// <summary>
        /// 
        /// </summary>
        protected RoomsModule roomsModule;

        protected override void Awake()
        {
            base.Awake();

            // Init auth module
            AddOptionalDependency<AuthModule>();
            // Init rooms module
            AddOptionalDependency<RoomsModule>();
        }

        public override void Initialize(IServer server)
        {
            authModule = server.GetModule<AuthModule>();
            roomsModule = server.GetModule<RoomsModule>();

            if (useAuthModule && authModule)
            {
                authModule.OnUserLoggedInEvent += OnUserLoggedInEventHandler;
                authModule.OnUserLoggedOutEvent += OnUserLoggedOutEventHandler;
            }
            else if (useAuthModule && !authModule)
            {
                logger.Error($"{GetType().Name} was set to use {nameof(AuthModule)}, but {nameof(AuthModule)} was not found");
            }

            if (useRoomsModule && !roomsModule)
            {
                logger.Error($"{GetType().Name} was set to use {nameof(RoomsModule)}, but {nameof(RoomsModule)} was not found. It means that room players cannot receive notifications");
            }

            server.RegisterMessageHandler(MstOpCodes.SubscribeToNotifications, OnSubscribeToNotificationsMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.UnsubscribeFromNotifications, OnUnsubscribeFromNotificationsMessageHandler);
            server.RegisterMessageHandler(MstOpCodes.Notification, OnNotificationMessageHandler);
        }

        /// <summary>
        /// Checks if peer has permission to notify another users
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        protected virtual bool HasPermissionToNotify(IPeer peer)
        {
            var extension = peer.GetExtension<SecurityInfoPeerExtension>();
            return extension != null && extension.PermissionLevel >= notifyPermissionLevel;
        }

        /// <summary>
        /// Invoked when user logs in
        /// </summary>
        /// <param name="userPeerExtension"></param>
        protected virtual void OnUserLoggedInEventHandler(IUserPeerExtension userPeerExtension)
        {
            var r = AddRecipient(userPeerExtension);

            foreach (var message in promisedMessages)
                r.Notify(message);
        }

        /// <summary>
        /// Invoked when user logs out
        /// </summary>
        /// <param name="userPeerExtension"></param>
        protected virtual void OnUserLoggedOutEventHandler(IUserPeerExtension userPeerExtension)
        {
            RemoveRecipient(userPeerExtension.UserId);
        }

        public override MstJson JsonInfo()
        {
            var json = base.JsonInfo();

            try
            {
                json.AddField("description", $"This is the {nameof(NotificationModule)} theat helps rooms send notifications to their players or helps admins of Master server send notifications to list of recipients");
                json.AddField("recipients", registeredRecipients.Count);
            }
            catch (Exception e)
            {
                json.AddField("error", e.ToString());
            }

            return json;
        }

        public override MstProperties Info()
        {
            var info = base.Info();
            info.Set("Description", $"This is the {nameof(NotificationModule)} theat helps rooms send notifications to their players or helps admins of Master server send notifications to list of recipients");
            info.Add("Recipients", registeredRecipients.Count);
            return info;
        }

        /// <summary>
        /// Checks if given recipient is already subscribed
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public bool HasRecipient(string userId)
        {
            return registeredRecipients.ContainsKey(userId);
        }

        /// <summary>
        ///  Gets recipient by its userId
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public NotificationRecipient GetRecipient(string userId)
        {
            registeredRecipients.TryGetValue(userId, out NotificationRecipient r);
            return r;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="recipient"></param>
        /// <returns></returns>
        public bool TryGetRecipient(string userId, out NotificationRecipient recipient)
        {
            recipient = GetRecipient(userId);
            return recipient != null;
        }

        /// <summary>
        /// Adds new authorized user to list of recipients
        /// </summary>
        /// <param name="user"></param>
        public NotificationRecipient AddRecipient(IUserPeerExtension user)
        {
            if (!HasRecipient(user.UserId))
            {
                var r = new NotificationRecipient(user.UserId, user.Peer);
                registeredRecipients.TryAdd(user.UserId, r);
                return r;
            }
            else
            {
                return GetRecipient(user.UserId);
            }
        }

        /// <summary>
        /// Removes user from list of recipients
        /// </summary>
        /// <param name="userId"></param>
        public void RemoveRecipient(string userId)
        {
            registeredRecipients.TryRemove(userId, out _);
        }

        /// <summary>
        /// Sends notice to room players and ignores players given in <paramref name="ignoreRecipients"/>
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="ignoreRecipients"></param>
        /// <param name="textMessage"></param>
        public virtual void NoticeToRoom(int roomId, List<int> ignoreRecipients, string textMessage)
        {
            // If rooms module not found
            if (!roomsModule)
            {
                logger.Error($"This message is for room users, but rooms module is not found");
                return;
            }

            // Let's get all users from room
            var players = roomsModule.GetPlayersOfRoom(roomId);

            foreach (var player in players)
            {
                var userExtension = player.GetExtension<IUserPeerExtension>();

                if (userExtension != null
                    && !ignoreRecipients.Contains(userExtension.Peer.Id)
                    && TryGetRecipient(userExtension.UserId, out var recipient))
                {
                    recipient.Notify(textMessage);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="recipient"></param>
        /// <param name="textMessage"></param>
        public virtual void NoticeToRecipient(int recipient, string textMessage)
        {
            NoticeToRecipients(new List<int> { recipient }, textMessage);
        }

        /// <summary>
        /// Sends notice to players given in <paramref name="recipients"/>
        /// </summary>
        /// <param name="recipients"></param>
        /// <param name="textMessage"></param>
        public virtual void NoticeToRecipients(List<int> recipients, string textMessage)
        {
            if (!authModule)
            {
                logger.Error($"This message is for authorized users, but auth module is not found");
                return;
            }

            foreach (int recipientId in recipients)
            {
                var peer = Server.GetPeer(recipientId);

                if (peer != null)
                {
                    var userExtension = peer.GetExtension<IUserPeerExtension>();

                    if (userExtension != null && TryGetRecipient(userExtension.UserId, out var recipient))
                    {
                        recipient.Notify(textMessage);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="textMessage"></param>
        /// <param name="addToPromise"></param>
        public virtual void NoticeToAll(string textMessage, bool addToPromise = false)
        {
            if (!authModule)
            {
                logger.Error($"This message is for authorized users, but auth module is not found");
                return;
            }

            foreach (var recipient in registeredRecipients.Values)
            {
                recipient.Notify(textMessage);
            }

            if (addToPromise && !promisedMessages.Contains(textMessage))
            {
                if (promisedMessages.Count >= maxPromisedMessages)
                {
                    promisedMessages.RemoveAt(0);
                }

                promisedMessages.Add(textMessage);
            }
        }

        #region MESSAGE HANDLERS

        protected virtual void OnSubscribeToNotificationsMessageHandler(IIncomingMessage message)
        {
            try
            {
                // Get logged in user
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                // If unauthorized user is trying to subscribe the notifications
                if (userExtension == null)
                {
                    message.Respond(ResponseStatus.Unauthorized);
                    logger.Error("Unauthorized user is trying to subscribe to notifications");
                    return;
                }

                // If is already subscribed
                if (HasRecipient(userExtension.UserId))
                {
                    message.Respond(ResponseStatus.Success);
                    return;
                }

                // Add new recipient
                AddRecipient(userExtension);

                // Respond about successfull subscription
                message.Respond(ResponseStatus.Success);
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error(e.Message);
                message.Respond(ResponseStatus.Error);
            }
        }

        protected virtual void OnUnsubscribeFromNotificationsMessageHandler(IIncomingMessage message)
        {
            try
            {
                // Get logged in user
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                // If unauthorized user is trying to subscribe the notifications
                if (userExtension != null)
                {
                    // Remove recipient
                    RemoveRecipient(userExtension.UserId);
                }

                // Respond about successfull unsubscription
                message.Respond(ResponseStatus.Success);
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error(e.Message);
                message.Respond(ResponseStatus.Error);
            }
        }

        protected virtual void OnNotificationMessageHandler(IIncomingMessage message)
        {
            try
            {
                if (!HasPermissionToNotify(message.Peer))
                {
                    message.Respond(ResponseStatus.Unauthorized);
                    logger.Error("The room tries to send a notification, but does not have the right to do so");
                    return;
                }

                // Parse notification
                var notification = message.AsPacket<NotificationPacket>();

                if (string.IsNullOrEmpty(notification.Message))
                {
                    message.Respond(ResponseStatus.Invalid);
                    logger.Error("Message cannot be empty");
                    return;
                }

                // Check if notification for room users
                if (useRoomsModule && notification.RoomId >= 0)
                {
                    NoticeToRoom(notification.RoomId, notification.IgnoreRecipients, notification.Message);
                }
                else if (notification.Recipients.Count > 0)
                {
                    NoticeToRecipients(notification.Recipients, notification.Message);
                }

                message.Respond(ResponseStatus.Success);
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error(e.Message);
                message.Respond(ResponseStatus.Error);
            }
        }

        #endregion
    }
}