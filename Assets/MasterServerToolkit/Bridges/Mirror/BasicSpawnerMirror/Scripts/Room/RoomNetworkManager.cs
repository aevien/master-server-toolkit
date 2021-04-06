#if MIRROR
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using Mirror;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomNetworkManager : NetworkManager
    {
        #region INSPECTOR

        [Header("Mirror Network Manager Settings"), SerializeField]
        private HelpBox help = new HelpBox()
        {
            Text = "This is extension of NetworkManager",
            Type = HelpBoxType.Info
        };

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [SerializeField]
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
        /// This is called on the Server when a Client disconnects from the Server
        /// </summary>
        public event Action<NetworkConnection> OnClientDisconnectedEvent;

        /// <summary>
        /// Called on clients when connected to a server
        /// </summary>
        public event Action<NetworkConnection> OnConnectedEvent;

        /// <summary>
        /// Called on clients when disconnected from a server
        /// </summary>
        public event Action<NetworkConnection> OnDisconnectedEvent;

        public override void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            // Prevent to create player automatically
            autoCreatePlayer = false;

            // Prevent start network manager in headless mode automatically
            autoStartServerBuild = false;

            base.Awake();
        }

        #region MIRROR CALLBACKS

        /// <summary>
        /// When mirror server is started
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();

            // Register handler to listen to player creation message
            NetworkServer.RegisterHandler<CreatePlayerMessage>(CreatePlayerRequestHandler, false);
            OnServerStartedEvent?.Invoke();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
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

        public override void OnStartClient()
        {
            base.OnStartClient();
            OnClientStartedEvent?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            OnClientStoppedEvent?.Invoke();
        }

        public override void OnServerDisconnect(NetworkConnection conn)
        {
            base.OnServerDisconnect(conn);
            OnClientDisconnectedEvent?.Invoke(conn);
        }

        public override void OnClientConnect(NetworkConnection conn)
        {
            base.OnClientConnect(conn);
            OnConnectedEvent?.Invoke(conn);
        }

        public override void OnClientDisconnect(NetworkConnection conn)
        {
            base.OnClientDisconnect(conn);
            OnDisconnectedEvent?.Invoke(conn);
        }

        #endregion

        /// <summary>
        /// Invokes when client requested to create player on mirror server
        /// </summary>
        /// <param name="arg1"></param>
        /// <param name="arg2"></param>
        protected virtual void CreatePlayerRequestHandler(NetworkConnection connection, CreatePlayerMessage message)
        {
            // Try to get old player
            GameObject oldPlayer = null;

            if(connection.identity != null)
            {
                oldPlayer = connection.identity.gameObject;
            }

            // Get start position of player
            Transform startPos = GetStartPosition();

            // Create new player
            GameObject player = startPos != null ? Instantiate(playerPrefab, startPos.position, startPos.rotation) : Instantiate(playerPrefab);

            if (oldPlayer)
            {
                NetworkServer.ReplacePlayerForConnection(connection, player);
            }
            else
            {
                NetworkServer.AddPlayerForConnection(connection, player);
            }
        }
    }
}
#endif