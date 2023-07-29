using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.MasterServer
{
    public class RoomServer : ServerBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private RoomServerManager roomServerManager;

        #endregion

        /// <summary>
        /// Singleton instance of the room server behaviour
        /// </summary>
        public static RoomServer Instance { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            // If instance of the server is already running
            if (Instance != null)
            {
                // Destroy, if this is not the first instance
                Destroy(gameObject);
                return;
            }

            // Create new instance
            Instance = this;

            // Move to root, so that it won't be destroyed
            // In case this instance is a child of another gameobject
            if (transform.parent != null)
                transform.SetParent(null);

            // Set server behaviour to be able to use in all levels
            DontDestroyOnLoad(gameObject);
        }

        protected override void Start()
        {
            base.Start();

            RegisterMessageHandler(MstOpCodes.ValidateRoomAccessRequest, ValidateRoomAccessRequestHandler);
        }

        public override void StartServer()
        {
            // Find room server if it is not assigned in inspector
            if (!roomServerManager) roomServerManager = GetComponent<RoomServerManager>();

            // Set the max number of connections
            maxConnections = roomServerManager.RoomOptions.MaxConnections;

            // Start server with room options
            StartServer(roomServerManager.RoomOptions.RoomIp, roomServerManager.RoomOptions.RoomPort);
        }

        protected override void OnStartedServer()
        {
            base.OnStartedServer();

            string sceneName = Mst.Args.AsString(Mst.Args.Names.RoomOnlineScene, SceneManager.GetActiveScene().name);

            logger.Info($"Loading server online scene... Scene: {sceneName}");

            ScenesLoader.LoadSceneByName(sceneName, null, () =>
            {
                logger.Info($"Room Server started and listening to: {serverIp}:{serverPort}");

                if (roomServerManager)
                    roomServerManager.OnServerStarted();
            });
        }

        protected override void OnStoppedServer()
        {
            logger.Info("Room Server stopped");
            base.OnStoppedServer();
            if (roomServerManager) roomServerManager.OnServerStopped();
        }

        protected override void OnPeerDisconnected(IPeer peer)
        {
            logger.Info($"Peer {peer.Id} disconnected");

            if (roomServerManager)
                roomServerManager.OnPeerDisconnected(peer.Id);
        }

        #region MESSAGE_HANDLERS

        protected virtual void ValidateRoomAccessRequestHandler(IIncomingMessage message)
        {
            if (roomServerManager)
            {
                roomServerManager.ValidateRoomAccess(message.Peer.Id, message.AsString(), (isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        logger.Error("Unauthorized access to room was rejected");
                        message.Peer.Disconnect("Unauthorized access to room was rejected");
                    }

                    message.Respond(ResponseStatus.Success);
                });
            }
            else
            {
                message.Peer.Disconnect("Room is invalid");
            }
        }

        #endregion
    }
}