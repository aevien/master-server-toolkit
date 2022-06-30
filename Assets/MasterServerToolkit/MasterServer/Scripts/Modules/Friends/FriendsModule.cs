using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class FriendsModule : BaseServerModule
    {
        #region INSPECTOR

        [Header("General Settings")]
        [SerializeField, Tooltip("If true, this module will subscribe to auth module, and automatically setup users when they log in")]
        protected bool useAuthModule = true;

        /// <summary>
        /// Database accessor factory that helps to create integration with friends db
        /// </summary>
        [Header("Components"), Tooltip("Database accessor factory that helps to create integration with friends db"), SerializeField]
        protected DatabaseAccessorFactory friendsAccessorFactory;

        #endregion

        /// <summary>
        /// Auth module for listening to auth events
        /// </summary>
        protected AuthModule authModule;

        /// <summary>
        /// Chat module for listening to chat events
        /// </summary>
        protected ChatModule chatModule;

        /// <summary>
        /// List of friends
        /// </summary>
        private Dictionary<string, HashSet<string>> friends;

        /// <summary>
        /// List of incoming requests for friendship
        /// </summary>
        private Dictionary<string, HashSet<string>> incomingFriendshipRequsts;

        /// <summary>
        /// List of outgoing requests for friendship
        /// </summary>
        private Dictionary<string, HashSet<string>> outgoingFriendshipRequsts;

        /// <summary>
        /// 
        /// </summary>
        private IFriendsDatabaseAccessor friendsDatabaseAccessor;

        //protected override void Awake()
        //{
        //    base.Awake();

        //    // Add auth module as a dependency of this module
        //    AddDependency<AuthModule>();

        //    // Add chat module as a dependency of this module
        //    AddDependency<ChatModule>();

        //    // Init friends and requests list
        //    friends = new Dictionary<string, HashSet<string>>();
        //    incomingFriendshipRequsts = new Dictionary<string, HashSet<string>>();
        //    outgoingFriendshipRequsts = new Dictionary<string, HashSet<string>>();
        //}

        public override void Initialize(IServer server)
        {
            //friendsAccessorFactory?.CreateAccessors();

            //// Get auth module dependency
            //authModule = server.GetModule<AuthModule>();

            ////TODO Get chat module dependency. Use this to add friends to private chat chennel
            //chatModule = server.GetModule<ChatModule>();

            //if (useAuthModule)
            //{
            //    if (authModule)
            //    {
            //        authModule.OnUserLoggedInEvent += OnUserLoggedInEventHandler;
            //    }
            //    else
            //    {
            //        logger.Error($"{GetType().Name} was set to use {nameof(AuthModule)}, but {nameof(AuthModule)} was not found");
            //    }
            //}

            ////
            //friendsDatabaseAccessor = Mst.Server.DbAccessors.GetAccessor<IFriendsDatabaseAccessor>();

            //server.RegisterMessageHandler((ushort)MstOpCodes.RequestFriendship, RequestFriendshipHandler);
            //server.RegisterMessageHandler((ushort)MstOpCodes.AcceptFriendship, AcceptFriendshipHandler);
            //server.RegisterMessageHandler((ushort)MstOpCodes.DeclineFriendship, DeclineFriendshipHandler);
            //server.RegisterMessageHandler((ushort)MstOpCodes.GetDeclinedFriendships, GetDeclinedFriendshipsHandler);

            //server.RegisterMessageHandler((ushort)MstOpCodes.GetFriends, GetFriendsHandler);
            //server.RegisterMessageHandler((ushort)MstOpCodes.InspectFriend, InspectFriendHandler);
            //server.RegisterMessageHandler((ushort)MstOpCodes.BlockFriends, BlockFriendsHandler);
            //server.RegisterMessageHandler((ushort)MstOpCodes.RemoveFriends, RemoveFriendsHandler);
        }

        public override MstProperties Info()
        {
            MstProperties info = base.Info();
            info.Set("Description", "This is a friends module that helps users to make friendship requests, accept friendships, and receive a list of friends.");
            info.Add("Database Accessor", friendsAccessorFactory != null ? "Connected" : "Not Connected");
            info.Add("Users with friends", friends.Count);
            info.Add("Incoming friendship requests", incomingFriendshipRequsts.Count);
            info.Add("Outgoing friendship requests", outgoingFriendshipRequsts.Count);
            return info;
        }

        /// <summary>
        /// Invoked when user logs in
        /// </summary>
        /// <param name="user"></param>
        protected virtual void OnUserLoggedInEventHandler(IUserPeerExtension user)
        {
            //
            user.Peer.OnConnectionCloseEvent += Peer_OnPeerDisconnectedEvent;

            // If user already loaded friends
            if (!friends.ContainsKey(user.UserId))
            {
                // Loads user friends from db
                //string[] friendIds = await friendsDatabaseAccessor.GetFriends(user.UserId);

                //if (friendIds != null && friendIds.Length > 0)
                //    friends[user.UserId] = friendIds;
            }

            // If user already loaded incoming friendship requests
            if (!incomingFriendshipRequsts.ContainsKey(user.UserId))
            {
                // Loads user friendship requests from db
                //string[] incomingFriendshipRequestIds = await friendsDatabaseAccessor.GetIncomingFriendshipRequests(user.UserId);

                //if (incomingFriendshipRequestIds != null && incomingFriendshipRequestIds.Length > 0)
                //    incomingFriendshipRequsts[user.UserId] = incomingFriendshipRequestIds;
            }

            // If user already loaded outgoing friendship requests
            if (!outgoingFriendshipRequsts.ContainsKey(user.UserId))
            {
                // Loads user friendship requests from db
                //string[] outgoingFriendshipRequestIds = await friendsDatabaseAccessor.GetOutgoingFriendshipRequests(user.UserId);

                //if (outgoingFriendshipRequestIds != null && outgoingFriendshipRequestIds.Length > 0)
                //    outgoingFriendshipRequsts[user.UserId] = outgoingFriendshipRequestIds;
            }
        }

        protected virtual void Peer_OnPeerDisconnectedEvent(IPeer peer)
        {
            peer.OnConnectionCloseEvent -= Peer_OnPeerDisconnectedEvent;

            var userExtension = peer.GetExtension<IUserPeerExtension>();

            if (userExtension != null)
            {
                friends.Remove(userExtension.UserId);
                outgoingFriendshipRequsts.Remove(userExtension.UserId);
                incomingFriendshipRequsts.Remove(userExtension.UserId);
            }
        }

        #region MESSAGE HANDLERS

        protected virtual async void RequestFriendshipHandler(IIncomingMessage message)
        {
            try
            {
                // Get id of requested user
                string requestedUserId = message.AsString();

                // Get user extension
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                // If this request was not made by active user
                if (userExtension == null)
                {
                    message.Respond("Unauthorized request", ResponseStatus.Unauthorized);
                    return;
                }

                // If user already have request list
                if (outgoingFriendshipRequsts.ContainsKey(userExtension.UserId))
                {
                    // Get user request list
                    HashSet<string> currentOutgoingIds = outgoingFriendshipRequsts[userExtension.UserId];

                    // Check if this request exists ins list
                    if (!currentOutgoingIds.Add(requestedUserId))
                    {
                        message.Respond("You have already sent this user a friendship request", ResponseStatus.Unauthorized);
                        return;
                    }
                }
                else
                {
                    outgoingFriendshipRequsts[userExtension.UserId] = new HashSet<string> { requestedUserId };
                }

                // Save new list to db
                await friendsDatabaseAccessor.UpdateOutgoingFriendshipRequests(userExtension.UserId, outgoingFriendshipRequsts[userExtension.UserId]);

                message.Respond(ResponseStatus.Success);
            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        protected virtual async void AcceptFriendshipHandler(IIncomingMessage message)
        {
            try
            {
                // Get id of accepted user
                string acceptedUserId = message.AsString();

                // Get user extension
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                // If this request was not made by active user
                if (userExtension == null)
                {
                    message.Respond("Unauthorized request", ResponseStatus.Unauthorized);
                    return;
                }

                // Add friend to accepter user
                friends[userExtension.UserId].Add(acceptedUserId);

                // Remove accepter user requests
                incomingFriendshipRequsts[userExtension.UserId].Remove(acceptedUserId);

                // Remove accepter user requests
                outgoingFriendshipRequsts[userExtension.UserId].Remove(acceptedUserId);

                // Save new list to db
                await friendsDatabaseAccessor.UpdateOutgoingFriendshipRequests(userExtension.UserId, outgoingFriendshipRequsts[userExtension.UserId]);

                // Respond to accepter
                message.Respond(ResponseStatus.Success);

                // Check if accepted user is online
                if (authModule && authModule.TryGetLoggedInUserById(acceptedUserId, out IUserPeerExtension acceptedUser))
                {
                    acceptedUser.Peer.SendMessage((ushort)MstOpCodes.FriendAdded, userExtension.Account.Username);
                }

                // Add friend to accepted user
                friends[acceptedUserId].Add(userExtension.UserId);
                // Remove accepted user requests
                outgoingFriendshipRequsts[acceptedUserId].Remove(userExtension.UserId);
                incomingFriendshipRequsts[acceptedUserId].Remove(userExtension.UserId);


                await friendsDatabaseAccessor.UpdateOutgoingFriendshipRequests(acceptedUserId, outgoingFriendshipRequsts[acceptedUserId]);

            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        protected virtual void DeclineFriendshipHandler(IIncomingMessage message)
        {
            try
            {

            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        protected virtual void GetDeclinedFriendshipsHandler(IIncomingMessage message)
        {
            try
            {

            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }


        protected virtual async void GetFriendsHandler(IIncomingMessage message)
        {
            try
            {
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null)
                {
                    message.Respond("Unauthorized", ResponseStatus.Unauthorized);
                    return;
                }

                IFriendsInfoData friendsInfo = await friendsDatabaseAccessor.RestoreFriends(userExtension.UserId);

                if (friendsInfo == null)
                {
                    message.Respond("No friends found", ResponseStatus.Failed);
                    return;
                }

                var usersList = authModule.GetLoggedInUsersByIds(friendsInfo.UsersIds);

                if (usersList.Count() == 0)
                {
                    message.Respond("No friends found", ResponseStatus.Failed);
                    return;
                }

                var friends = new FriendsInfoDataPacket();
                friends.LastUpdate = friendsInfo.LastUpdate;
                friends.Properties = friendsInfo.Properties;

                foreach (var user in authModule.GetLoggedInUsersByIds(friendsInfo.UsersIds))
                {
                    //friends.Users
                }
            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        private void BlockFriendsHandler(IIncomingMessage message)
        {
            throw new NotImplementedException();
        }

        private void RemoveFriendsHandler(IIncomingMessage message)
        {
            try
            {

            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        protected virtual void InspectFriendHandler(IIncomingMessage message)
        {
            try
            {

            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        #endregion
    }
}