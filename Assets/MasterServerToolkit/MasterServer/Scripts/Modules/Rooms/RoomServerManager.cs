using MasterServerToolkit.Extensions;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public delegate ObservableServerProfile ProfileFactoryDelegate(string userId);

    public class RoomServerManager : BaseClientBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Loads player profile after he joined the room
        /// </summary>
        [Header("Room Settings"), SerializeField, Tooltip("Loads player profile after he joined the room")]
        protected bool autoLoadUserProfile = true;
        [SerializeField]
        protected bool forceClientMode = false;

        /// <summary>
        /// Allows guest users to be connected to room
        /// </summary>
        [SerializeField, Tooltip("Allows guest users to be connected to room")]
        protected bool allowGuestUsers = true;

        [Header("Termination Settings"), SerializeField]
        protected bool terminateRoomWhenDisconnected = true;
        [SerializeField]
        protected bool terminateRoomWhenLastPlayerQuits = true;
        [SerializeField]
        protected float terminateRoomDelay = 5f;

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

        /// <summary>
        /// List of all players
        /// </summary>
        public IEnumerable<RoomPlayer> Players => playersByRoomPeerId.Values;

        /// <summary>
        /// Check if room has players
        /// </summary>
        public bool HasPlayers => Players != null && Players.Count() > 0;

        /// <summary>
        /// By default, profiles module will use this factory to create a profile for users.
        /// If you're using profiles, you will need to change this factory to construct the
        /// structure of a profile.
        /// </summary>
        public ProfileFactoryDelegate ProfileFactory { get; set; }

        protected override void Awake()
        {
            base.Awake();

            Mst.Client.Rooms.IsClientMode = forceClientMode;

            playersByUsername = new Dictionary<string, RoomPlayer>();
            playersByMasterPeerId = new Dictionary<int, RoomPlayer>();
            playersByRoomPeerId = new Dictionary<int, RoomPlayer>();

            ProfileFactory = (userId) => new ObservableServerProfile(userId);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            StopAllCoroutines();
            CancelInvoke();
            Connection?.RemoveConnectionOpenListener(OnConnectedToMasterEventHandler);
            Connection?.RemoveConnectionCloseListener(OnDisconnectedFromMasterEventHandler);
            Connection?.Close();
        }

        protected override void OnInitialize()
        {
            base.OnInitialize();

            // If we are on client side. It can be main menu, etc.
            if (Mst.Client.Rooms.IsClientMode)
            {
                logger.Debug("Server cannot be started in client mode...");
                return;
            }

            // Listen to master server connection status
            Connection.AddConnectionOpenListener(OnConnectedToMasterEventHandler);
            Connection.AddConnectionCloseListener(OnDisconnectedFromMasterEventHandler, false);
        }

        private void OnConnectedToMasterEventHandler()
        {
            logger.Info("Room server connected to master server as client");

            if (terminateRoomWhenDisconnected)
            {
                StopCoroutine(TerminateRoomAfterDelay());
            }

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
                    // 
                    BeforeRoomRegistering();

                    // Invoke notification
                    OnBeforeRoomRegisterEvent?.Invoke(RoomOptions);
                }
            });
        }

        private void OnDisconnectedFromMasterEventHandler()
        {
            if (terminateRoomWhenDisconnected)
            {
                StartCoroutine(TerminateRoomAfterDelay());
            }
        }

        /// <summary>
        /// Terminates room if delay time is over
        /// </summary>
        /// <returns></returns>
        private IEnumerator TerminateRoomAfterDelay()
        {
            yield return new WaitForSecondsRealtime(terminateRoomDelay);
            Mst.Runtime.Quit();
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roomPeerId"></param>
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

                    OnPlayerLeftRoom(player);

                    // Inform subscribers about this bad guy
                    OnPlayerLeftRoomEvent?.Invoke(player);
                }
                else
                {
                    logger.Debug($"Room server client {roomPeerId} left the room");
                }

                if (terminateRoomWhenLastPlayerQuits && playersByRoomPeerId.Count == 0)
                {
                    terminateRoomDelay = 0.1f;
                    StartCoroutine(TerminateRoomAfterDelay());
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
            // If room is public or provate
            bool isPublic = !Mst.Args.RoomIsPrivate;

            // If room using lobby
            bool isUsingLobby = Mst.Args.IsProvided(Mst.Args.Names.LobbyId);

            RoomOptions = new RoomOptions
            {
                IsPublic = !isUsingLobby && isPublic,

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
                Region = Mst.Args.RoomRegion
            };

            // If room was spawned by task
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

                RoomOptions.CustomOptions = SpawnTaskController.Options.FindByKey("-room.");
            }
            else
            {
                string[] keys = Mst.Args.FindKeys("-room.");
                var properties = new MstProperties();

                foreach (string key in keys)
                {
                    if (Mst.Args.IsProvided(key))
                        properties.Set(key, Mst.Args.AsString(key));
                }

                RoomOptions.CustomOptions = properties;
            }
        }

        /// <summary>
        /// Start registering our room server
        /// </summary>
        protected virtual void RegisterRoom()
        {
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
                RoomController.SaveOptions();

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
                RoomMaxConnections = RoomController.Options.MaxConnections,
                CustomOptions = RoomController.Options.CustomOptions,
                Token = Mst.Helper.CreateGuidString(),
                SceneName = SceneManager.GetActiveScene().name
            }, null);
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

                OnPlayerJoinedRoom(player);

                // Inform subscribers about this player
                OnPlayerJoinedRoomEvent?.Invoke(player);
            }
        }

        /// <summary>
        /// Invoked when player joins a room
        /// </summary>
        /// <param name="player"></param>
        protected virtual void OnPlayerJoinedRoom(RoomPlayer player)
        {
            MstTimer.WaitForSeconds(2f, () =>
            {
                Mst.Server.Notifications.NotifyRecipient(player.MasterPeerId,
                            $"Hi, {player.Username.ToRed()}!\nWelcome to \"{RoomOptions.Name}\" server", null);
            });

            Mst.Server.Notifications.NotifyRoom(RoomController.RoomId,
                    new int[] { player.MasterPeerId },
                    $"Player {player.Username} has just joined the room",
                    null);
        }

        /// <summary>
        /// Invoked when player leaves a room
        /// </summary>
        /// <param name="player"></param>
        protected virtual void OnPlayerLeftRoom(RoomPlayer player)
        {
            Mst.Server.Notifications.NotifyRoom(RoomController.RoomId,
                    new int[] { player.MasterPeerId },
                    $"Player {player.Username} has just left the room",
                    null);
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
        /// 
        /// </summary>
        /// <param name="peerId"></param>
        /// <param name="roomPlayer"></param>
        /// <returns></returns>
        public bool TryGetRoomPlayerByRoomPeer(int peerId, out RoomPlayer roomPlayer)
        {
            roomPlayer = GetRoomPlayerByRoomPeer(peerId);
            return roomPlayer != null;
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="roomPlayer"></param>
        /// <returns></returns>
        public bool TryGetRoomPlayerByUsername(string username, out RoomPlayer roomPlayer)
        {
            roomPlayer = GetRoomPlayerByUsername(username);
            return roomPlayer != null;
        }
    }
}