using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class RoomEvent : UnityEvent<RoomOptions> { }
    [Serializable]
    public class RoomRegisteredEvent : UnityEvent<RoomController> { }
    [Serializable]
    public class RoomPlayerEvent : UnityEvent<RoomPlayer> { }

    public class RoomServerManager : BaseClientBehaviour, ITerminatableRoom
    {
        #region INSPECTOR

        /// <summary>
        /// Loads player profile after he joined the room
        /// </summary>
        [Header("Room Settings"), SerializeField, Tooltip("Loads player profile after he joined the room")]
        protected bool autoLoadUserProfile = true;

        /// <summary>
        /// Allows guest users to be connected to room
        /// </summary>
        [SerializeField, Tooltip("Allows guest users to be connected to room")]
        protected bool allowGuestUsers = true;

        /// <summary>
        /// Fires before server room registeration process
        /// </summary>
        [Header("Events")]
        public RoomEvent OnBeforeRoomRegisterEvent;

        /// <summary>
        /// Fires when server room is successfully registered
        /// </summary>
        public RoomRegisteredEvent OnRoomRegisteredEvent;

        /// <summary>
        /// Fires when server room registration failed
        /// </summary>
        public UnityEvent OnRoomRegisterFailedEvent;

        /// <summary>
        /// Fires when new playerjoined room
        /// </summary>
        public RoomPlayerEvent OnPlayerJoinedRoomEvent;

        /// <summary>
        /// Fires when existing player left room
        /// </summary>
        public RoomPlayerEvent OnPlayerLeftRoomEvent;

        #endregion

        /// <summary>
        /// List of users by username
        /// </summary>
        private Dictionary<string, RoomPlayer> playersByUsername;
        /// <summary>
        /// List of users by master peer id
        /// </summary>
        private Dictionary<int, RoomPlayer> playersByMasterPeerId;
        /// <summary>
        /// List of users by room peer id
        /// </summary>
        private Dictionary<int, RoomPlayer> playersByRoomPeerId;

        /// <summary>
        /// Options of this room we must share with clients
        /// </summary>
        public RoomOptions RoomOptions { get; protected set; }

        /// <summary>
        /// Controller of the room
        /// </summary>
        public RoomController RoomController { get; protected set; }

        /// <summary>
        /// Spawner task controller
        /// </summary>
        public SpawnTaskController SpawnTaskController { get; protected set; }

        public event Action OnCheckTerminationConditionEvent;

        protected override void Awake()
        {
            base.Awake();

            playersByUsername = new Dictionary<string, RoomPlayer>();
            playersByMasterPeerId = new Dictionary<int, RoomPlayer>();
            playersByRoomPeerId = new Dictionary<int, RoomPlayer>();
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            // If we are on client room side
            if (Mst.Client.Rooms.ForceClientMode)
            {
                logger.Debug("Server cannot be started in client mode...");
                return;
            }

            // Set room options at start
            RoomOptions = SetRoomOptions();

            // Listen to master server connection status
            Connection.AddConnectionListener(OnConnectedToMasterEventHandler);
        }

        private void OnConnectedToMasterEventHandler()
        {
            MstTimer.WaitForEndOfFrame(() =>
            {
                // If this room was spawned
                if (Mst.Server.Spawners.IsSpawnedProccess)
                {
                    // Try to register spawned process first
                    RegisterSpawnedProcess();
                }
                else
                {
                    OnBeforeRoomRegisterEvent?.Invoke(RoomOptions);
                }
            });
        }

        /// <summary>
        /// Invoke this method when server is started
        /// </summary>
        public virtual void OnServerStarted()
        {
            // Now register the room
            RegisterRoom();
        }

        /// <summary>
        /// Invoke this method when server is stopped
        /// </summary>
        public virtual void OnServerStopped() { }

        public virtual void OnPeerDisconnected(int roomPeerId)
        {
            MstTimer.WaitForSeconds(0.2f, () =>
            {
                // Try to find player in filtered list
                if (playersByRoomPeerId.TryGetValue(roomPeerId, out RoomPlayer player))
                {
                    logger.Debug($"Room server player {player.Username} with room client Id {roomPeerId} left the room");

                    // Remove thisplayer from filtered list
                    playersByRoomPeerId.Remove(player.RoomPeerId);
                    playersByMasterPeerId.Remove(player.MasterPeerId);
                    playersByUsername.Remove(player.Username);

                    // Notify master server about disconnected player
                    if (RoomController.IsActive)
                        RoomController.NotifyPlayerLeft(player.MasterPeerId);

                    // Dispose profile
                    player.Profile?.Dispose();

                    // Inform subscribers about this bad guy
                    OnPlayerLeftRoomEvent?.Invoke(player);

                    // Calling termination conditions check
                    OnCheckTerminationConditionEvent?.Invoke();
                }
                else
                {
                    logger.Debug($"Room server client {roomPeerId} left the room");
                }
            });
        }

        /// <summary>
        /// Invoke this method to validate access token
        /// </summary>
        /// <param name="token"></param>
        public virtual void ValidateRoomAccess(int roomPeerId, string token, SuccessCallback callback)
        {
            // Triying to validate given token
            Mst.Server.Rooms.ValidateAccess(RoomController.RoomId, token, (usernameAndPeerId, error) =>
            {
                try
                {
                    // If token is not valid
                    if (usernameAndPeerId == null)
                    {
                        throw new Exception(error);
                    }

                    logger.Debug($"Client {roomPeerId} is successfully validated");
                    logger.Debug("Getting his account info...");

                    Mst.Server.Auth.GetPeerAccountInfo(usernameAndPeerId.PeerId, (accountInfo, accountError) =>
                    {
                        try
                        {
                            if (accountInfo == null)
                            {
                                throw new Exception(accountError);
                            }

                            // If we do not want guest users to play in our room
                            if (!allowGuestUsers && accountInfo.Properties.Has(MstDictKeys.USER_IS_GUEST) && accountInfo.Properties.AsBool(MstDictKeys.USER_IS_GUEST))
                            {
                                // Remove guest player from room on master server
                                if (RoomController.IsActive)
                                    RoomController.NotifyPlayerLeft(usernameAndPeerId.PeerId);

                                throw new Exception("Guest users cannot play this room. Hands off...");
                            }

                            // Create new room player
                            var player = new RoomPlayer(usernameAndPeerId.PeerId, roomPeerId, accountInfo.UserId, accountInfo.Username, accountInfo.Properties)
                            {
                                Profile = ProfileFactory(accountInfo.UserId)
                            };

                            // Add this player to filtered lists
                            playersByMasterPeerId.Add(usernameAndPeerId.PeerId, player);
                            playersByRoomPeerId.Add(roomPeerId, player);
                            playersByUsername.Add(accountInfo.Username, player);

                            // If server is required user profile
                            if (autoLoadUserProfile)
                            {
                                LoadPlayerProfile(accountInfo.Username, (isLoadProfileSuccess, loadProfileError) =>
                                {
                                    if (isLoadProfileSuccess)
                                    {
                                        FinalizePlayerJoining(roomPeerId);
                                        callback?.Invoke(true, string.Empty);
                                    }
                                });
                            }
                            else
                            {
                                FinalizePlayerJoining(roomPeerId);
                                callback?.Invoke(true, string.Empty);
                            }
                        }
                        catch (Exception e)
                        {
                            logger.Error(e.Message);
                            callback?.Invoke(false, e.Message);
                        }
                    });
                }
                catch (Exception e)
                {
                    callback?.Invoke(false, e.Message);
                }
            });
        }

        /// <summary>
        /// Sets this room options. If you need your own options just override this method
        /// </summary>
        /// <returns></returns>
        protected virtual RoomOptions SetRoomOptions()
        {
            // If room is public or provate
            bool isPublic = !Mst.Args.RoomIsPrivate;

            // If room using lobby
            bool isUsingLobby = Mst.Args.IsProvided(Mst.Args.Names.LobbyId);

            return new RoomOptions
            {
                IsPublic = isUsingLobby ? false : isPublic,

                // This is for controlling max number of players that may be connected
                MaxConnections = Mst.Args.RoomMaxConnections,

                // Just the name of the room
                Name = Mst.Args.RoomName,

                // If room uses the password
                Password = Mst.Args.RoomPassword,

                // Room IP that will be used by players to connect to this room
                RoomIp = Mst.Args.RoomIp,

                // Room port that will be used by players to connect to this room
                RoomPort = Mst.Args.RoomPort,

                // Region that this room may use to filter it in games list
                Region = Mst.Args.RoomRegion,

                //// Allows access request or does not
                //AllowUsersRequestAccess = !isUsingLobby
            };
        }

        /// <summary>
        /// Before we register our room we need to register spawned process if required
        /// </summary>
        protected void RegisterSpawnedProcess()
        {
            logger.Info("Registering spawned process...");

            // Let's register this process
            Mst.Server.Spawners.RegisterSpawnedProcess(Mst.Args.SpawnTaskId, Mst.Args.SpawnTaskUniqueCode, (taskController, error) =>
            {
                if (taskController == null)
                {
                    logger.Error($"Room server process cannot be registered. The reason is: {error}");
                    return;
                }

                SpawnTaskController = taskController;

                logger.Info($"Spawned process registered with task ID: {Mst.Args.SpawnTaskId}");

                BeforeRoomRegistering();

                // Invoke notification
                OnBeforeRoomRegisterEvent?.Invoke(RoomOptions);
            });
        }

        /// <summary>
        /// This method is called before creating a room. It can be used to
        /// extract some parameters from cmd args or from span task properties
        /// </summary>
        protected virtual void BeforeRoomRegistering()
        {
            if (SpawnTaskController != null)
            {
                // If max players param was given from spawner task
                if (SpawnTaskController.Options.Has(MstDictKeys.ROOM_MAX_CONNECTIONS))
                {
                    RoomOptions.MaxConnections = SpawnTaskController.Options.AsInt(MstDictKeys.ROOM_MAX_CONNECTIONS);
                }

                // If password was given from spawner task
                if (SpawnTaskController.Options.Has(MstDictKeys.ROOM_PASSWORD))
                {
                    RoomOptions.Password = SpawnTaskController.Options.AsString(MstDictKeys.ROOM_PASSWORD);
                }

                // If max players was given from spawner task
                if (SpawnTaskController.Options.Has(MstDictKeys.ROOM_NAME))
                {
                    RoomOptions.Name = SpawnTaskController.Options.AsString(MstDictKeys.ROOM_NAME);
                }
            }
        }

        /// <summary>
        /// Start registering our room server
        /// </summary>
        protected virtual void RegisterRoom()
        {
            OnBeforeRoomRegisterEvent?.Invoke(RoomOptions);

            logger.Info($"Registering room to list...");

            Mst.Server.Rooms.RegisterRoom(RoomOptions, (controller, error) =>
            {
                if (controller == null)
                {
                    logger.Error(error);
                    OnRoomRegisterFailedEvent?.Invoke();
                    return;
                }

                // Registered room controller
                RoomController = controller;

                // Set access provider
                RoomController.AccessProvider = CreateAccessProvider;

                // And save them
                controller.SaveOptions();

                logger.Info($"Room registered successfully. Room ID: {controller.RoomId}, {RoomOptions}");

                // If this room was spawned
                if (SpawnTaskController != null)
                    SpawnTaskController.FinalizeTask(CreateSpawnFinalizationData());

                // Notify listeners
                OnRoomRegisteredEvent?.Invoke(RoomController);
            });
        }

        /// <summary>
        /// This <see cref="MstProperties"/> will be sent to "master server" when we want 
        /// notify "master" server that Spawn Process is completed
        /// </summary>
        /// <returns></returns>
        protected virtual MstProperties CreateSpawnFinalizationData()
        {
            var properties = new MstProperties();
            properties.Set(MstDictKeys.ROOM_ID, RoomController.RoomId);
            properties.Set(MstDictKeys.ROOM_PASSWORD, RoomController.RoomId);
            return properties;
        }

        /// <summary>
        /// Override, if you want to manually handle creation of access'es
        /// </summary>
        /// <param name="accessCheckOptions"></param>
        /// <param name="giveAccess"></param>
        protected virtual void CreateAccessProvider(RoomAccessProviderCheck accessCheckOptions, RoomAccessProviderCallback giveAccess)
        {
            // Use accessCheckOptions to check user that requested access to room

            giveAccess.Invoke(new RoomAccessPacket()
            {
                RoomId = RoomController.RoomId,
                RoomIp = RoomController.Options.RoomIp,
                RoomPort = RoomController.Options.RoomPort,
                CustomOptions = RoomController.Options.CustomOptions,
                Token = Mst.Helper.CreateGuidString(),
                SceneName = SceneManager.GetActiveScene().name
            }, null);
        }

        /// <summary>
        /// This will create room server player profile with all its properties
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        protected virtual ObservableServerProfile ProfileFactory(string userId)
        {
            return new ObservableServerProfile(userId);
        }

        /// <summary>
        /// Finalize player joining to server room
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void FinalizePlayerJoining(int roomPeerId)
        {
            if (playersByRoomPeerId.ContainsKey(roomPeerId))
            {
                RoomPlayer player = playersByRoomPeerId[roomPeerId];
                logger.Debug($"Client {roomPeerId} has become a player of this room. Congratulations to {player.Username}");

                // Inform subscribers about this player
                OnPlayerJoinedRoomEvent?.Invoke(player);
            }
        }

        /// <summary>
        /// Loads player profile
        /// </summary>
        /// <param name="successCallback"></param>
        public void LoadPlayerProfile(string username, SuccessCallback successCallback)
        {
            if (playersByUsername.TryGetValue(username, out RoomPlayer player))
            {
                Mst.Server.Profiles.FillProfileValues(player.Profile, (isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        string message = "Room server cannot retrieve player profile from master server";
                        logger.Error(message);
                        successCallback?.Invoke(false, message);
                        return;
                    }

                    logger.Debug($"Profile of player {username} is successfully loaded. Player info: {player}");
                    successCallback?.Invoke(true, string.Empty);
                }, Connection);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public bool IsAllowedToBeTerminated()
        {
            return false;
        }

        /// <summary>
        /// Get <see cref="RoomPlayer"/> by room peer Id
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        public RoomPlayer GetRoomPlayerByRoomPeer(int peerId)
        {
            playersByRoomPeerId.TryGetValue(peerId, out RoomPlayer player);
            return player;
        }

        /// <summary>
        /// Get <see cref="RoomPlayer"/> by master peer Id
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        public RoomPlayer GetRoomPlayerByMasterPeer(int peerId)
        {
            playersByMasterPeerId.TryGetValue(peerId, out RoomPlayer player);
            return player;
        }

        /// <summary>
        /// Get <see cref="RoomPlayer"/> by master username
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public RoomPlayer GetRoomPlayerByUsername(string username)
        {
            playersByUsername.TryGetValue(username, out RoomPlayer player);
            return player;
        }
    }
}