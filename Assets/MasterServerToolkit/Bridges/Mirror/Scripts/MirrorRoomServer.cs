#if MIRROR
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.Mirror
{
    public class MirrorRoomServer : BaseClientBehaviour, ITerminatableRoom
    {
        #region INSPECTOR

        /// <summary>
        /// Loads player profile after he joined the room
        /// </summary>
        [Header("Server Player Settings"), SerializeField, Tooltip("Loads player profile after he joined the room")]
        protected bool autoLoadUserProfile = true;

        /// <summary>
        /// If true, than if profile loadingis failed server will disconnect user
        /// </summary>
        [SerializeField, Tooltip("If true, than if profile loadingis failed server will disconnect user")]
        protected bool disconnectIfProfileFailed = false;

        /// <summary>
        /// Master server IP address to connect room server to master server as client
        /// </summary>
        [Header("Master Connection Settings"), SerializeField, Tooltip("Master server IP address to connect room server to master server as client")]
        protected string masterIp = "127.0.0.1";

        /// <summary>
        /// Master server port to connect room server to master server as client
        /// </summary>
        [SerializeField, Tooltip("Master server port to connect room server to master server as client")]
        protected int masterPort = 5000;

        /// <summary>
        /// Allows guest users to be connected to room
        /// </summary>
        [SerializeField, Tooltip("Allows guest users to be connected to room")]
        protected bool allowGuestUsers = true;

        [Header("Editor Settings"), SerializeField]
        private HelpBox editorHelp = new HelpBox()
        {
            Text = "This settings works only in editor. They are for test purpose only",
            Type = HelpBoxType.Info
        };

        /// <summary>
        /// This will start server in editor automatically
        /// </summary>
        [SerializeField, Tooltip("This will start server in editor automatically")]
        protected bool autoStartInEditor = true;

        /// <summary>
        /// If true this will start server as host in test mode
        /// </summary>
        [SerializeField, Tooltip("If true this will start server as host in test mode")]
        protected bool startServerAsHost = true;

        #endregion

        /// <summary>
        /// The instance of the <see cref="MirrorRoomServer"/>
        /// </summary>
        public static MirrorRoomServer Instance { get; protected set; }

        /// <summary>
        /// List of players filtered by MSF peer Id
        /// </summary>
        protected Dictionary<int, MirrorRoomPlayer> roomPlayersByMsfPeerId;

        /// <summary>
        /// List of players filtered by Mirror peer Id
        /// </summary>
        protected Dictionary<int, MirrorRoomPlayer> roomPlayersByMirrorPeerId;

        /// <summary>
        /// List of players filtered by username
        /// </summary>
        protected Dictionary<string, MirrorRoomPlayer> roomPlayersByUsername;

        /// <summary>
        /// Options of this room we must share with clients
        /// </summary>
        protected RoomOptions roomOptions;

        /// <summary>
        /// Mirror network manager
        /// </summary>
        public NetworkManager MirrorNetworkManager { get; set; }

        /// <summary>
        /// Controller of the room
        /// </summary>
        public RoomController RoomController { get; private set; }

        /// <summary>
        /// Fires when server room is successfully registered
        /// </summary>
        public event Action OnRoomServerRegisteredEvent;

        /// <summary>
        /// Fires when new playerjoined room
        /// </summary>
        public event Action<MirrorRoomPlayer> OnPlayerJoinedRoomEvent;

        /// <summary>
        /// Fires when existing player left room
        /// </summary>
        public event Action<MirrorRoomPlayer> OnPlayerLeftRoomEvent;

        /// <summary>
        /// Call this when you use <see cref="RoomTerminator"/> and want to check termination conditions
        /// </summary>
        public event Action OnCheckTerminationConditionEvent;

        protected override void Awake()
        {
            base.Awake();

            // Only one room server can exist in scene
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            // Create simple singleton
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Do not initialize if we are in client mode
            if (Mst.Client.Rooms.ForceClientMode) return;

            // Create filtered lists of players
            roomPlayersByMsfPeerId = new Dictionary<int, MirrorRoomPlayer>();
            roomPlayersByMirrorPeerId = new Dictionary<int, MirrorRoomPlayer>();
            roomPlayersByUsername = new Dictionary<string, MirrorRoomPlayer>();

            // If master IP is provided via cmd arguments
            masterIp = Mst.Args.ExtractValue(Mst.Args.Names.MasterIp, masterIp);

            // If master port is provided via cmd arguments
            masterPort = Mst.Args.ExtractValueInt(Mst.Args.Names.MasterPort, masterPort);
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void OnApplicationQuit()
        {
            if (Connection != null)
                Connection.Disconnect();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Remove connection listeners
            Connection?.RemoveConnectionListener(OnConnectedToMasterServerEventHandler);
            Connection?.RemoveDisconnectionListener(OnDisconnectedFromMasterServerEventHandler);

            // Start listenin to OnServerStartedEvent of our MirrorNetworkManager
            if (MirrorNetworkManager is MirrorNetworkManager manager)
            {
                manager.OnServerStartedEvent -= OnMirrorServerStartedEventHandler;
                manager.OnClientDisconnectedEvent -= OnMirrorClientDisconnectedEvent;
                manager.OnServerStoppedEvent -= OnMirrorServerStoppedEventHandler;
            }

            // Unregister handlers
            NetworkServer.UnregisterHandler<ValidateRoomAccessRequestMessage>();
        }

        protected override void OnInitialize()
        {
            if (Mst.Client.Rooms.ForceClientMode) return;

            // Get mirror network manager
            MirrorNetworkManager = NetworkManager.singleton;

            // Start listening to OnServerStartedEvent of our MirrorNetworkManager
            if (MirrorNetworkManager is MirrorNetworkManager manager)
            {
                manager.OnServerStartedEvent += OnMirrorServerStartedEventHandler;
                manager.OnClientDisconnectedEvent += OnMirrorClientDisconnectedEvent;
                manager.OnServerStoppedEvent += OnMirrorServerStoppedEventHandler;
            }
            else
            {
                logger.Error("We cannot register listeners of MirrorNetworkManager events because we cannot find it onscene");
            }

            // Set room oprions
            roomOptions = SetRoomOptions();

            // Set port of the Mirror server
            SetPort(roomOptions.RoomPort);

            // Add master server connection and disconnection listeners
            Connection.AddConnectionListener(OnConnectedToMasterServerEventHandler, true);
            Connection.AddDisconnectionListener(OnDisconnectedFromMasterServerEventHandler, false);

            // If connection to master server is not established
            if (!Connection.IsConnected && !Connection.IsConnecting)
            {
                Connection.Connect(masterIp, masterPort);
            }
        }

        /// <summary>
        /// Check if server is allowed to be started in editor. This feature is for testing purpose only
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsAllowedToBeStartedInEditor()
        {
            return !Mst.Client.Rooms.ForceClientMode
                && Mst.Runtime.IsEditor
                   && autoStartInEditor;
        }

        #region MIRROR EVENTS

        /// <summary>
        /// Invokes when mirror server is started
        /// </summary>
        protected virtual void OnMirrorServerStartedEventHandler()
        {
            // Register handler to listen to client access validation request
            NetworkServer.RegisterHandler<ValidateRoomAccessRequestMessage>(ValidateRoomAccessRequestHandler, false);

            // Start room registration
            RegisterRoomServer();
        }

        /// <summary>
        /// This is called on the Server when a Mirror Client disconnects from the Server
        /// </summary>
        /// <param name="obj"></param>
        private void OnMirrorClientDisconnectedEvent(NetworkConnection connection)
        {
            MstTimer.WaitForSeconds(0.2f, () =>
            {
                // Try to find player in filtered list
                if (roomPlayersByMirrorPeerId.TryGetValue(connection.connectionId, out MirrorRoomPlayer player))
                {
                    logger.Debug($"Room server player {player.Username} with room client Id {connection.connectionId} left the room");

                    // Remove thisplayer from filtered list
                    roomPlayersByMirrorPeerId.Remove(player.MirrorPeer.connectionId);
                    roomPlayersByMsfPeerId.Remove(player.MasterPeerId);
                    roomPlayersByUsername.Remove(player.Username);

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
                    logger.Debug($"Room server client {connection.connectionId} left the room");
                }
            });
        }

        /// <summary>
        /// Fired when mirror host was stopped.
        /// This is usefull in test mode.
        /// </summary>
        private void OnMirrorServerStoppedEventHandler()
        {
            // Register handler to listen to client access validation request
            NetworkServer.UnregisterHandler<ValidateRoomAccessRequestMessage>();
            RoomController?.Destroy();
        }

        #endregion

        #region MSF CONNECTION EVENTS

        /// <summary>
        /// Invokes when room server is successfully connected to master server as client
        /// </summary>
        private void OnConnectedToMasterServerEventHandler()
        {
            logger.Debug("Room server is successfully connected to master server");

            // If this room was spawned
            if (Mst.Server.Spawners.IsSpawnedProccess)
            {
                // Try to register spawned process first
                RegisterSpawnedProcess();
            }

            // If we are testing our room in editor
            if (IsAllowedToBeStartedInEditor())
            {
                StartServerInEditor();
            }
        }

        /// <summary>
        /// Fired when this room server is disconnected from master as client
        /// </summary>
        protected virtual void OnDisconnectedFromMasterServerEventHandler()
        {
            // Quit the room if we are not in editor
            if (!Mst.Runtime.IsEditor)
                Mst.Runtime.Quit();
        }

        #endregion

        /// <summary>
        /// This will start server in test mode
        /// </summary>
        protected virtual void StartServerInEditor()
        {
            if (startServerAsHost)
            {
                MirrorNetworkManager.StopHost();
                MstTimer.WaitForSeconds(0.2f, () => MirrorNetworkManager.StartHost());
            }
            else
            {
                MirrorNetworkManager.StopServer();
                MstTimer.WaitForSeconds(0.2f, () => MirrorNetworkManager.StartServer());
            }
        }

        /// <summary>
        /// Sets this room options. Remember that this will be overridden if room was spawned by spawner system
        /// </summary>
        /// <returns></returns>
        protected virtual RoomOptions SetRoomOptions()
        {
            return new RoomOptions
            {
                // Set room as public
                IsPublic = true,

                // This is for controlling max number of players that may be connected
                MaxConnections = Mst.Args.ExtractValueInt(Mst.Args.Names.RoomMaxConnections, MirrorNetworkManager.maxConnections),

                // Just the name of the room
                Name = Mst.Args.ExtractValue(Mst.Args.Names.RoomName, $"Room[{Mst.Helper.CreateRandomString(5)}]"),

                // If room uses the password
                Password = Mst.Args.RoomPassword,

                // Room IP that will be used by players to connect to this room
                RoomIp = Mst.Args.RoomIp,

                // Room port that will be used by players to connect to this room
                RoomPort = Mst.Args.ExtractValueInt(Mst.Args.Names.RoomPort, GetPort()),

                // Region that this room may use to filter it in the games list
                Region = Mst.Args.ExtractValue(Mst.Args.Names.RoomRegion, string.Empty)
            };
        }

        /// <summary>
        /// Before we register our room we need to register spawned process if required
        /// </summary>
        protected void RegisterSpawnedProcess()
        {
            // Let's register this process
            Mst.Server.Spawners.RegisterSpawnedProcess(Mst.Args.SpawnTaskId, Mst.Args.SpawnTaskUniqueCode, (taskController, error) =>
            {
                if (taskController == null)
                {
                    logger.Error($"Room server process cannot be registered. The reason is: {error}");
                    return;
                }

                // If max players was given from spawner task
                if (taskController.Options.Has(MstDictKeys.roomName))
                {
                    roomOptions.Name = taskController.Options.AsString(MstDictKeys.roomName);
                }

                // If room is public or not
                if (taskController.Options.Has(MstDictKeys.roomIsPublic))
                {
                    roomOptions.IsPublic = taskController.Options.AsBool(MstDictKeys.roomIsPublic);
                }

                // If max players param was given from spawner task
                if (taskController.Options.Has(MstDictKeys.roomMaxPlayers))
                {
                    roomOptions.MaxConnections = taskController.Options.AsInt(MstDictKeys.roomMaxPlayers);
                }

                // If password was given from spawner task
                if (taskController.Options.Has(MstDictKeys.roomPassword))
                {
                    roomOptions.Password = taskController.Options.AsString(MstDictKeys.roomPassword);
                }

                // Set port of the Mirror server
                SetPort(roomOptions.RoomPort);

                // Finalize spawn task before we start mirror server 
                taskController.FinalizeTask(new MstProperties(), () =>
                {
                    // Start Mirror server
                    MirrorNetworkManager.StartServer();
                });
            });
        }

        /// <summary>
        /// Start registering our room server
        /// </summary>
        protected virtual void RegisterRoomServer()
        {
            Mst.Server.Rooms.RegisterRoom(roomOptions, (controller, error) =>
            {
                if (controller == null)
                {
                    logger.Error(error);

                    // Quit the room if we are not in editor
                    if (!Mst.Runtime.IsEditor)
                        Mst.Runtime.Quit();

                    return;
                }

                // Save our room controller
                RoomController = controller;

                OnRoomServerRegisteredEvent?.Invoke();

                logger.Info($"Room {controller.RoomId} is successfully registered with options {roomOptions}");
            });
        }

        /// <summary>
        /// Fires when client that wants to connect to this room made request to validate the access token
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        protected virtual void ValidateRoomAccessRequestHandler(NetworkConnection conn, ValidateRoomAccessRequestMessage msg)
        {
            logger.Debug($"Room client {conn.connectionId} asked to validate access token [{msg.Token}]");

            // Triying to validate given token
            Mst.Server.Rooms.ValidateAccess(RoomController.RoomId, msg.Token, (usernameAndPeerId, error) =>
            {
                // If token is not valid
                if (usernameAndPeerId == null)
                {
                    logger.Error(error);

                    conn.Send(new ValidateRoomAccessResultMessage()
                    {
                        Error = error,
                        Status = ResponseStatus.Failed
                    });

                    MstTimer.WaitForSeconds(1f, () => conn.Disconnect());

                    return;
                }

                logger.Debug($"Client {conn.connectionId} is successfully validated");
                logger.Debug("Getting his account info...");

                Mst.Server.Auth.GetPeerAccountInfo(usernameAndPeerId.PeerId, (accountInfo, accountError) =>
                {
                    if (accountInfo == null)
                    {
                        logger.Error(accountError);

                        conn.Send(new ValidateRoomAccessResultMessage()
                        {
                            Error = accountError,
                            Status = ResponseStatus.Error
                        });

                        MstTimer.WaitForSeconds(1f, () => conn.Disconnect());

                        return;
                    }

                    // If we do not want guest users to play in our room
                    if (!allowGuestUsers && accountInfo.CustomOptions.Has(MstDictKeys.userIsGuest) && accountInfo.CustomOptions.AsBool(MstDictKeys.userIsGuest))
                    {
                        logger.Error("Guest users cannot play this room. Hands off...");

                        conn.Send(new ValidateRoomAccessResultMessage()
                        {
                            Error = "Guest users cannot play this room. Hands off...",
                            Status = ResponseStatus.Error
                        });

                        MstTimer.WaitForSeconds(1f, () => conn.Disconnect());

                        return;
                    }

                    // Create new room player
                    var player = new MirrorRoomPlayer(usernameAndPeerId.PeerId, conn, accountInfo.Username, accountInfo.CustomOptions)
                    {
                        Profile = ProfileFactory(accountInfo.Username)
                    };

                    // Add this player to filtered lists
                    roomPlayersByMsfPeerId.Add(usernameAndPeerId.PeerId, player);
                    roomPlayersByMirrorPeerId.Add(conn.connectionId, player);
                    roomPlayersByUsername.Add(accountInfo.Username, player);

                    // If server is required user profile
                    if (autoLoadUserProfile)
                    {
                        LoadPlayerProfile(accountInfo.Username, (isLoadProfileSuccess, loadProfileError) =>
                        {
                            if (isLoadProfileSuccess)
                            {
                                FinalizePlayerJoining(conn);
                            }
                        });
                    }
                    else
                    {
                        FinalizePlayerJoining(conn);
                    }
                });
            });
        }

        /// <summary>
        /// This will create room server player profile with all its properties
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        protected virtual ObservableServerProfile ProfileFactory(string username)
        {
            return new ObservableServerProfile(username);
        }

        /// <summary>
        /// Finalize player joining to server room
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void FinalizePlayerJoining(NetworkConnection conn)
        {
            if (roomPlayersByMirrorPeerId.ContainsKey(conn.connectionId))
            {
                MirrorRoomPlayer player = roomPlayersByMirrorPeerId[conn.connectionId];

                logger.Debug($"Client {conn.connectionId} has become a player of this room. Congratulations to {player.Username}");

                conn.Send(new ValidateRoomAccessResultMessage()
                {
                    Error = string.Empty,
                    Status = ResponseStatus.Success
                });

                // Inform subscribers about this player
                OnPlayerJoinedRoomEvent?.Invoke(player);
            }
        }

        /// <summary>
        /// Set network transport port
        /// </summary>
        /// <param name="port"></param>
        public virtual void SetPort(int port)
        {
            if (Transport.activeTransport is TelepathyTransport transport)
            {
                transport.port = (ushort)port;
            }
            else
            {
                logger.Error("You are trying to use TelepathyTransport. But it is not found on the scene. Try to override this method to create you own implementation");
            }
        }

        /// <summary>
        /// Get network transport port
        /// </summary>
        /// <returns></returns>
        public virtual int GetPort()
        {
            if (Transport.activeTransport is TelepathyTransport transport)
            {
                return (int)transport.port;
            }
            else
            {
                logger.Error("You are trying to use TelepathyTransport. But it is not found on the scene. Try to override this method to create you own implementation");
                return 0;
            }
        }

        /// <summary>
        /// Loads player profile
        /// </summary>
        /// <param name="successCallback"></param>
        public void LoadPlayerProfile(string username, SuccessCallback successCallback)
        {
            if (roomPlayersByUsername.ContainsKey(username))
            {
                MirrorRoomPlayer player = roomPlayersByUsername[username];

                Mst.Server.Profiles.FillProfileValues(player.Profile, (isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        logger.Error("Room server cannot retrieve player profile from master server");
                        successCallback?.Invoke(false, "Room server cannot retrieve player profile from master server");

                        if (disconnectIfProfileFailed)
                        {
                            MstTimer.WaitForSeconds(1f, () => player.MirrorPeer.Disconnect());
                        }

                        return;
                    }

                    logger.Debug($"Profile of player {username} is successfully loaded. Player info: {player}");
                    successCallback?.Invoke(true, string.Empty);
                });
            }
        }

        /// <summary>
        /// Get <see cref="MirrorRoomPlayer"/> by Mirror peer Id
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public MirrorRoomPlayer GetRoomPlayerByMirrorPeer(NetworkConnection connection)
        {
            roomPlayersByMirrorPeerId.TryGetValue(connection.connectionId, out MirrorRoomPlayer player);
            return player;
        }

        /// <summary>
        /// Get <see cref="MirrorRoomPlayer"/> by Msf peer Id
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public MirrorRoomPlayer GetRoomPlayerByMsfPeer(int connection)
        {
            roomPlayersByMsfPeerId.TryGetValue(connection, out MirrorRoomPlayer player);
            return player;
        }

        /// <summary>
        /// Get <see cref="MirrorRoomPlayer"/> by Msf username
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public MirrorRoomPlayer GetRoomPlayerByUsername(string username)
        {
            roomPlayersByUsername.TryGetValue(username, out MirrorRoomPlayer player);
            return player;
        }

        /// <summary>
        /// Check if room satisfies the conditions to be terminated
        /// </summary>
        /// <returns></returns>
        public bool IsAllowedToBeTerminated()
        {
            return roomPlayersByMirrorPeerId.Count <= 0;
        }
    }
}
#endif