using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class RoomServerPlayerEvent : UnityEvent<IRoomPlayerPeerExtension> { }
    [Serializable]
    public class RoomServerEvent : UnityEvent<RoomServer> { }

    public class RoomServer : ServerBehaviour, ITerminatableRoom
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        protected bool doNotDestroyOnLoad = true;

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
        /// Allows guest users to be connected to room
        /// </summary>
        [SerializeField, Tooltip("Allows guest users to be connected to room")]
        protected bool allowGuestUsers = true;

        #endregion

        /// <summary>
        /// Main connection to server
        /// </summary>
        public IClientSocket Connection { get; protected set; }

        /// <summary>
        /// Options of this room we must share with clients
        /// </summary>
        protected RoomOptions roomOptions;

        /// <summary>
        /// List of players filtered by master peer id
        /// </summary>
        protected Dictionary<int, IRoomPlayerPeerExtension> roomPlayersByMstPeerId;

        /// <summary>
        /// List of players filtered by room peer id
        /// </summary>
        protected Dictionary<int, IRoomPlayerPeerExtension> roomPlayersByRoomPeerId;

        /// <summary>
        /// List of players filtered by username
        /// </summary>
        protected Dictionary<string, IRoomPlayerPeerExtension> roomPlayersByUsername;

        /// <summary>
        /// Controller of the room
        /// </summary>
        public RoomController RoomController { get; protected set; }

        /// <summary>
        /// Fires when server room is successfully registered
        /// </summary>
        public RoomServerEvent OnRoomRegisteredEvent;

        /// <summary>
        /// Fires when new playerjoined room
        /// </summary>
        public RoomServerPlayerEvent OnPlayerJoinedRoomEvent;

        /// <summary>
        /// Fires when existing player left room
        /// </summary>
        public RoomServerPlayerEvent OnPlayerLeftRoomEvent;

        /// <summary>
        /// Call this when you use <see cref="RoomTerminator"/> and want to check termination conditions
        /// </summary>
        public event Action OnCheckTerminationConditionEvent;

        protected override void Awake()
        {
            if (doNotDestroyOnLoad)
            {
                // Find another instance of this behaviour
                var clientInstance = FindObjectOfType<RoomServer>();

                if (clientInstance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                DontDestroyOnLoad(gameObject);
            }

            base.Awake();

            // Do not initialize if we are in client mode
            if (Mst.Client.Rooms.ForceClientMode) return;

            // Set connection if it is null
            if (Connection == null) Connection = ConnectionFactory();

            // Initialize lists
            roomPlayersByMstPeerId = new Dictionary<int, IRoomPlayerPeerExtension>();
            roomPlayersByRoomPeerId = new Dictionary<int, IRoomPlayerPeerExtension>();
            roomPlayersByUsername = new Dictionary<string, IRoomPlayerPeerExtension>();

            // If master IP is provided via cmd arguments
            masterIp = Mst.Args.AsString(Mst.Args.Names.MasterIp, masterIp);

            // If master port is provided via cmd arguments
            masterPort = Mst.Args.AsInt(Mst.Args.Names.MasterPort, masterPort);
        }

        protected override void Start()
        {
            if (Mst.Client.Rooms.ForceClientMode) return;

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
                Connection.UseSsl = MstApplicationConfig.Instance.UseSecure || Mst.Args.UseSecure;
                Connection.Connect(masterIp, masterPort);
            }
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
        }

        /// <summary>
        /// Check if server is allowed to be started in editor. This feature is for testing purpose only
        /// </summary>
        /// <returns></returns>
        protected override bool IsAllowedToBeStartedInEditor()
        {
            return !Mst.Client.Rooms.ForceClientMode && base.IsAllowedToBeStartedInEditor();
        }

        /// <summary>
        /// Set this room options
        /// </summary>
        /// <returns></returns>
        protected virtual RoomOptions SetRoomOptions()
        {
            return new RoomOptions
            {
                // Let's make this room as private until it is successfully registered. 
                // This is useful to prevent players connection to this room before registration process finished.
                IsPublic = false,

                // This is for controlling max number of players that may be connected
                MaxConnections = Mst.Args.AsInt(Mst.Args.Names.RoomMaxConnections, maxConnections),

                // Just the name of the room
                Name = Mst.Args.AsString(Mst.Args.Names.RoomName, "Room_" + Mst.Helper.CreateRandomAlphanumericString(5)),

                // If room uses the password
                Password = Mst.Args.AsString(Mst.Args.Names.RoomPassword, string.Empty),

                // Room IP that will be used by players to connect to this room
                RoomIp = Mst.Args.AsString(Mst.Args.Names.RoomIp, serverIP),

                // Room port that will be used by players to connect to this room
                RoomPort = Mst.Args.AsInt(Mst.Args.Names.RoomPort, serverPort),

                // Region that this room may use to filter it in games list
                Region = Mst.Args.AsString(Mst.Args.Names.RoomRegion, string.Empty)
            };
        }

        /// <summary>
        /// Returns the connection to server
        /// </summary>
        /// <returns></returns>
        protected virtual IClientSocket ConnectionFactory()
        {
            return Mst.Client.Connection;
        }

        /// <summary>
        /// Before we register our room we need to register spawned process if required
        /// </summary>
        protected virtual void RegisterSpawnedProcess()
        {
            // Let's register this process
            Mst.Server.Spawners.RegisterSpawnedProcess(Mst.Args.SpawnTaskId, Mst.Args.SpawnTaskUniqueCode, (taskController, error) =>
            {
                if (taskController == null)
                {
                    logger.Error($"Room server process cannot be registered. The reason is: {error}");
                    return;
                }

                // If max players param was given from spawner task
                if (taskController.Options.Has(MstDictKeys.ROOM_MAX_PLAYERS))
                {
                    roomOptions.MaxConnections = taskController.Options.AsInt(MstDictKeys.ROOM_MAX_PLAYERS);
                }

                // If password was given from spawner task
                if (taskController.Options.Has(MstDictKeys.ROOM_PASSWORD))
                {
                    roomOptions.Password = taskController.Options.AsString(MstDictKeys.ROOM_PASSWORD);
                }

                // If max players was given from spawner task
                if (taskController.Options.Has(MstDictKeys.ROOM_NAME))
                {
                    roomOptions.Name = taskController.Options.AsString(MstDictKeys.ROOM_NAME);
                }

                // Set port of the server
                SetPort(roomOptions.RoomPort);

                // Finalize spawn task before we start mirror server 
                taskController.FinalizeTask(new MstProperties(), () =>
                {
                    // Start server
                    StartServer();
                });
            });
        }

        /// <summary>
        /// Start registering our room server
        /// </summary>
        protected virtual void RegisterRoom(UnityAction successCallback = null)
        {
            logger.Info($"Registering room to list with options: {roomOptions}");

            Mst.Server.Rooms.RegisterRoom(roomOptions, (controller, error) =>
            {
                if (controller == null)
                {
                    logger.Error(error);
                    return;
                }

                // When registration process is successfully finished we can change options of the registered room
                roomOptions.IsPublic = !Mst.Args.RoomIsPrivate;

                // And save them
                controller.SaveOptions();

                // Registered room controller
                RoomController = controller;

                logger.Info($"Room created and registered successfully. Room ID: {controller.RoomId}, {roomOptions}");

                successCallback?.Invoke();
            });
        }

        #region ROOM SERVER EVENTS

        /// <summary>
        /// Fires when room server is started
        /// </summary>
        protected override void OnStartedServer()
        {
            base.OnStartedServer();

            // If this room was spawned
            if (Mst.Server.Spawners.IsSpawnedProccess)
            {
                // Try to register spawned process first
                RegisterSpawnedProcess();
            }
            else
            {
                RegisterRoom(() =>
                {
                    logger.Info("Ok!");
                    OnRoomRegisteredEvent?.Invoke(this);
                });
            }
        }

        /// <summary>
        /// Invokes when server started
        /// </summary>
        protected override void OnStoppedServer()
        {
            base.OnStoppedServer();
            RoomController?.Destroy();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        protected override void OnPeerConnected(IPeer peer)
        {
            base.OnPeerConnected(peer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        protected override void OnPeerDisconnected(IPeer peer)
        {
            MstTimer.WaitForSeconds(0.2f, () =>
            {
                // Try to find player in filtered list
                if (roomPlayersByRoomPeerId.TryGetValue(peer.Id, out IRoomPlayerPeerExtension player))
                {
                    logger.Debug($"Room server player {player.Username} with room client Id {peer.Id} left the room");

                    // Remove this player from filtered list
                    roomPlayersByRoomPeerId.Remove(player.Peer.Id);
                    roomPlayersByMstPeerId.Remove(player.MasterPeerId);
                    roomPlayersByUsername.Remove(player.Username);

                    // Notify master server about disconnected player
                    if (RoomController.IsActive)
                        RoomController.NotifyPlayerLeft(player.MasterPeerId);

                    // Inform subscribers about this bad guy
                    OnPlayerLeftRoomEvent?.Invoke(player);

                    // Calling termination conditions check
                    OnCheckTerminationConditionEvent?.Invoke();
                }
                else
                {
                    logger.Debug($"Room server client {peer.Id} left the room");
                }
            });
        }

        #endregion

        #region MSF CONNECTION EVENTS

        /// <summary>
        /// Invokes when room server is successfully connected to master server as client
        /// </summary>
        protected virtual void OnConnectedToMasterServerEventHandler()
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
                StartServer();
            }
        }

        /// <summary>
        /// Fired when this room server is disconnected from master as client
        /// </summary>
        protected virtual void OnDisconnectedFromMasterServerEventHandler()
        {
            // Remove listener after disconnection
            Connection?.RemoveDisconnectionListener(OnDisconnectedFromMasterServerEventHandler);

            // Quit the room. Master Server will restart the room
            Mst.Runtime.Quit();
        }

        #endregion

        /// <summary>
        /// Get connected user by its msf peer id
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        public IRoomPlayerPeerExtension GetUserByMsfPeerId(int peerId)
        {
            roomPlayersByMstPeerId.TryGetValue(peerId, out IRoomPlayerPeerExtension user);
            return user;
        }

        /// <summary>
        /// Get connected user by its room peer id
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        public IRoomPlayerPeerExtension GetUserByRoomPeerId(int peerId)
        {
            roomPlayersByRoomPeerId.TryGetValue(peerId, out IRoomPlayerPeerExtension user);
            return user;
        }

        /// <summary>
        /// Get connected user by its username
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public IRoomPlayerPeerExtension GetUserByUsername(string username)
        {
            roomPlayersByUsername.TryGetValue(username, out IRoomPlayerPeerExtension user);
            return user;
        }

        /// <summary>
        /// Check if room satisfies the conditions to be terminated
        /// </summary>
        /// <returns></returns>
        public bool IsAllowedToBeTerminated()
        {
            return roomPlayersByRoomPeerId.Count <= 0;
        }
    }
}