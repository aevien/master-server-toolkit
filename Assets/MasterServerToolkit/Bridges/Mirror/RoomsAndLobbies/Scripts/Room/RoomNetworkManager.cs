#if MIRROR
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using Mirror;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public partial class RoomNetworkManager : NetworkManager
    {
        #region INSPECTOR

        [Header("Room Components"), SerializeField, Tooltip("Required MST room manager that owns room registration, limits, access validation, and peer-disconnect notifications. If empty, Awake resolves it from the same GameObject.")]
        protected RoomServerManager roomManager;

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Room Settings"), SerializeField, Tooltip("Minimum severity written by the Mirror room manager's MST logger. This changes diagnostics only and does not affect network behavior.")]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// Invokes when mirror server is started
        /// </summary>
        public event Action OnServerStartedEvent;

        /// <summary>
        /// Invokes when mirror server is stopped
        /// </summary>
        public event Action OnServerStoppedEvent;

        /// <summary>
        /// Invokes when mirror host is started
        /// </summary>
        public event Action OnHostStartedEvent;

        /// <summary>
        /// Invokes when mirror host is stopped
        /// </summary>
        public event Action OnHostStopEvent;

        /// <summary>
        /// Invokes when mirror client is started
        /// </summary>
        public event Action OnClientStartedEvent;

        /// <summary>
        /// Invokes when mirror client is stopped
        /// </summary>
        public event Action OnClientStoppedEvent;

        /// <summary>
        /// Called on clients when connected to a server
        /// </summary>
        public event Action<NetworkConnection> OnConnectedEvent;

        /// <summary>
        /// Called on clients when disconnected from a server
        /// </summary>
        public event Action OnDisconnectedEvent;

        /// <summary>
        /// This is called on the Server when a Client connects the Server
        /// </summary>
        public event Action<NetworkConnection> OnClientConnectedEvent;

        /// <summary>
        /// This is called on the Server when a Client disconnects from the Server
        /// </summary>
        public event Action<NetworkConnection> OnClientDisconnectedEvent;

        /// <summary>
        /// 
        /// </summary>
        public event Action<string> OnClientChangeSceneEvent;

        /// <summary>
        /// 
        /// </summary>
        public event Action OnClientSceneChangedEvent;

        /// <summary>
        /// Runs on server
        /// Networking is NOT initialized when this fires 
        /// </summary>
        public override void Awake()
        {
            // Start this as server only
            headlessStartMode = HeadlessStartOptions.AutoStartServer;

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            if (roomManager == null)
                roomManager = GetComponent<RoomServerManager>();

            base.Awake();
        }

        public override void Start()
        {
            OnBeforeServerStart();

            logger.Info($"Starting the room server: {networkAddress}:{roomManager.Options().RoomPort}");
            roomManager.OnRoomRegisterFailedEvent.AddListener(OnRoomRegisterFailed);

            base.Start();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            roomManager.OnRoomRegisterFailedEvent.RemoveListener(OnRoomRegisterFailed);
        }

        private void OnBeforeServerStart()
        {
            if (string.IsNullOrEmpty(onlineScene))
                onlineScene = SceneManager.GetActiveScene().name;

            var roomOptions = roomManager.Options();

            // Set online scene
            onlineScene = Mst.Args.AsString(Mst.Args.Names.RoomOnlineScene, onlineScene);

            // Set the max number of connections
            maxConnections = roomOptions.MaxPlayers;

            // Set room port and IP just for information purpose only
            SetAddress(roomOptions.RoomIp);
            SetPort(roomOptions.RoomPort);
        }

        private void OnRoomRegisterFailed()
        {
            StopServer();

            MstTimer.WaitForSeconds(1f, () =>
            {
                Mst.Runtime.Quit();
            });
        }

        #region SHARED

        /// <summary>
        /// 
        /// </summary>
        /// <param name="address"></param>
        public void SetAddress(string address)
        {
            networkAddress = address;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        public void SetPort(int port)
        {
            // Set room port
            if (Transport.active is PortTransport portTransport)
            {
                portTransport.Port = (ushort)port;
            }
        }

        #endregion

        #region MIRROR SERVER

        /// <summary>
        /// When mirror server is started
        /// </summary>
        public override void OnStartServer()
        {
            logger.Info($"The room server is now running and listening on {networkAddress}:{roomManager.Options().RoomPort}");

            base.OnStartServer();
            roomManager.StartServer();
            OnServerStartedEvent?.Invoke();
        }

        public override void OnStopServer()
        {
            logger.Info("The room server was just stopped");

            base.OnStopServer();
            roomManager.StopServer();
            OnServerStoppedEvent?.Invoke();
        }

        public override void OnStartHost()
        {
            base.OnStartHost();
            OnHostStartedEvent?.Invoke();
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            OnHostStopEvent?.Invoke();
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            logger.Info($"Client {conn.connectionId} has just joined the room");
            base.OnServerConnect(conn);
            OnClientConnectedEvent?.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            logger.Info($"Client {conn.connectionId} has just left the room");
            base.OnServerDisconnect(conn);
            roomManager.OnPeerDisconnected(conn.connectionId);
            OnClientDisconnectedEvent?.Invoke(conn);
        }

        #endregion

        #region MIRROR CLIENT

        public void SetClientReady()
        {
            if (!NetworkClient.ready)
                NetworkClient.Ready();

            if (autoCreatePlayer)
                NetworkClient.AddPlayer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            logger.Info($"Сlient started");
            OnClientStartedEvent?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            logger.Info($"Сlient stopped");
            OnClientStoppedEvent?.Invoke();
        }

        public override void OnClientConnect()
        {
            logger.Info($"Сlient connected");
            OnConnectedEvent?.Invoke(NetworkClient.connection);
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            logger.Info($"You have left a room");
            OnDisconnectedEvent?.Invoke();
        }

        /// <summary>
        /// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows client to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        /// <param name="sceneOperation">Scene operation that's about to happen</param>
        /// <param name="customHandling">true to indicate that scene loading will be handled through overrides</param>
        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            base.OnClientChangeScene(newSceneName, sceneOperation, customHandling);
            OnClientChangeSceneEvent?.Invoke(newSceneName);
        }

        /// <summary>
        /// Called on clients when a scene has completed loaded, when the scene load was initiated by the server.
        /// <para>Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.</para>
        /// </summary>
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            OnClientSceneChangedEvent?.Invoke();
        }

        #endregion
    }
}
#endif
