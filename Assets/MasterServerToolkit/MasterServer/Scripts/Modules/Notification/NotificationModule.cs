using MasterServerToolkit.Networking;
using System;
using System.Collections;
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

        /// <summary>
        /// List of recipients
        /// </summary>
        protected Dictionary<string, NotificationRecipient> registeredRecipients = new Dictionary<string, NotificationRecipient>();

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

            server.RegisterMessageHandler((short)MstMessageCodes.SubscribeToNotifications, OnSubscribeToNotificationsMessageHandler);
            server.RegisterMessageHandler((short)MstMessageCodes.UnsubscribeFromNotifications, OnUnsubscribeToNotificationsMessageHandler);
            server.RegisterMessageHandler((short)MstMessageCodes.Notification, OnNotificationMessageHandler);
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
        /// <param name="user"></param>
        protected virtual void OnUserLoggedInEventHandler(IUserPeerExtension user)
        {
            AddRecipient(user);
        }

        /// <summary>
        /// Invoked when user logs out
        /// </summary>
        /// <param name="user"></param>
        protected virtual void OnUserLoggedOutEventHandler(IUserPeerExtension user)
        {
            RemoveRecipient(user.UserId);
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
        /// Adds new authorized user to list of recipients
        /// </summary>
        /// <param name="user"></param>
        public void AddRecipient(IUserPeerExtension user)
        {
            if (!HasRecipient(user.UserId))
            {
                registeredRecipients.Add(user.UserId, new NotificationRecipient(user.UserId, user.Peer));
            }
        }

        /// <summary>
        /// Removes user from list of recipients
        /// </summary>
        /// <param name="userId"></param>
        public void RemoveRecipient(string userId)
        {
            registeredRecipients.Remove(userId);
        }

        /// <summary>
        /// Sends notice to room players and ignores players given in <paramref name="ignoreRecipients"/>
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="ignoreRecipients"></param>
        /// <param name="message"></param>
        public virtual void NoticeToRoom(int roomId, List<int> ignoreRecipients, string message)
        {
            // If rooms module not found
            if (!roomsModule)
                throw new MstMessageHandlerException($"This message is for room users, but rooms module is not found");

            // Let's get all users from room
            var players = roomsModule.GetPlayersOfRoom(roomId);

            foreach (var player in players)
            {
                var userExtension = player.GetExtension<IUserPeerExtension>();

                if (userExtension != null && !ignoreRecipients.Contains(userExtension.Peer.Id))
                {
                    var recipient = GetRecipient(userExtension.UserId);

                    if (recipient != null) recipient.Notify(message);
                }
            }
        }

        /// <summary>
        /// Sends notice to players given in <paramref name="recipients"/>
        /// </summary>
        /// <param name="recipients"></param>
        /// <param name="message"></param>
        public virtual void NoticeToRecipients(List<int> recipients, string message)
        {
            if (!authModule)
                throw new MstMessageHandlerException($"This message is for authorized users, but auth module is not found");

            foreach (int recipientId in recipients)
            {
                var peer = Server.GetPeer(recipientId);

                if (peer != null)
                {
                    var userExtension = peer.GetExtension<IUserPeerExtension>();

                    if (userExtension != null)
                    {
                        var recipient = GetRecipient(userExtension.UserId);

                        if (recipient != null) recipient.Notify(message);
                    }
                }
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
                    throw new MstMessageHandlerException("Unauthorized request", ResponseStatus.Unauthorized);

                // If is already subscribed
                if (HasRecipient(userExtension.UserId))
                    throw new MstMessageHandlerException("You are already subscribed to notifications", ResponseStatus.NotHandled);

                // Add new recipient
                AddRecipient(userExtension);

                // Respond about successfull subscription
                message.Respond(ResponseStatus.Success);
            }
            // If we got system exception
            catch (MstMessageHandlerException e)
            {
                logger.Error(e.Message);
                message.Respond(e.Message, e.Status);
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error(e.Message);
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        protected virtual void OnUnsubscribeToNotificationsMessageHandler(IIncomingMessage message)
        {
            try
            {
                // Get logged in user
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                // If unauthorized user is trying to subscribe the notifications
                if (userExtension == null)
                    throw new MstMessageHandlerException("Unauthorized request", ResponseStatus.Unauthorized);

                // Add new recipient
                RemoveRecipient(userExtension.UserId);

                // Respond about successfull unsubscription
                message.Respond(ResponseStatus.Success);
            }
            // If we got system exception
            catch (MstMessageHandlerException e)
            {
                logger.Error(e.Message);
                message.Respond(e.Message, e.Status);
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error(e.Message);
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        protected virtual void OnNotificationMessageHandler(IIncomingMessage message)
        {
            try
            {
                if (!HasPermissionToNotify(message.Peer))
                    throw new MstMessageHandlerException($"Unauthorized request", ResponseStatus.Unauthorized);

                // Parse notification
                var notification = message.Deserialize(new NotificationPacket());

                if (string.IsNullOrEmpty(notification.Message))
                    throw new MstMessageHandlerException($"Message cannot be empty");

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
            // If we got system exception
            catch (MstMessageHandlerException e)
            {
                logger.Error(e.Message);
                message.Respond(e.Message, e.Status);
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error(e.Message);
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        #endregion
    }
}