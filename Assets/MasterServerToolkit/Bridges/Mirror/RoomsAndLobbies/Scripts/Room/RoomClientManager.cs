#if MIRROR
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using Mirror;
using Mirror.SimpleWeb;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomClientManager : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField, Tooltip("Minimum severity written by this room client's MST logger. This changes diagnostics only and does not affect connection behavior.")]
        protected LogLevel logLevel = LogLevel.Info;

        [Header("Components"), SerializeField, Tooltip("Legacy room-manager reference retained by the Mirror sample prefab. The current client connection flow reads room access from Mst.Client.Rooms and does not use this field.")]
        private RoomServerManager roomManager;

        #endregion

        protected RoomNetworkManager networkManager;
        protected Logging.Logger logger;
        protected float roomConnectionTimeout = 60f;

        private string currentOnlineRoomScene = string.Empty;
        private string currentOfflineRoomScene = string.Empty;

        #region UNITY

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        private void Start()
        {
            networkManager = NetworkManager.singleton as RoomNetworkManager;

            if (Mst.Client.Rooms.HasAccess)
            {
                StartClient(Mst.Client.Rooms.ReceivedAccess);
            }

            if (networkManager != null)
            {
                networkManager.OnConnectedEvent += NetworkManager_OnConnectedEvent;
                networkManager.OnDisconnectedEvent += NetworkManager_OnDisconnectedEvent;
            }
            else
            {
                logger.Error($"Before using {typeof(RoomNetworkManager).Name} add it to scene");
            }
        }

        private void OnDestroy()
        {
            if (networkManager)
            {
                networkManager.OnConnectedEvent -= NetworkManager_OnConnectedEvent;
                networkManager.OnDisconnectedEvent -= NetworkManager_OnDisconnectedEvent;
            }
        }

        #endregion

        #region MIRROR CALLBACKS

        /// <summary>
        /// Invoked when client connected to room server
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void NetworkManager_OnConnectedEvent(NetworkConnection conn)
        {
            //logger.Info($"Waiting for access data. Timeout in {roomConnectionTimeout} sec.");

            //MstTimer.WaitWhile(() => !Mst.Client.Rooms.HasAccess, (isSuccess) =>
            //{
            //    if (!isSuccess)
            //    {
            //        logger.Error("Room connection timeout");
            //        Disconnect();
            //        return;
            //    }

            //    logger.Info($"Validating access to room server with token [{Mst.Client.Rooms.ReceivedAccess.Token}]");

            //    // Register listener for access validation message from mirror room server
            //    NetworkClient.RegisterHandler<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler, false);

            //    // Send validation message to room server
            //    conn.Send(new ValidateRoomAccessRequestMessage()
            //    {
            //        token = Mst.Client.Rooms.ReceivedAccess.Token
            //    });

            //    logger.Info($"You have joined the room at {Mst.Client.Rooms.ReceivedAccess.Ip}:{Mst.Client.Rooms.ReceivedAccess.RoomPort}");
            //}, roomConnectionTimeout);
        }

        /// <summary>
        /// Invoked when client disconnected from room server
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void NetworkManager_OnDisconnectedEvent()
        {
            //logger.Info("You have just been disconnected from the server");

            //NetworkClient.UnregisterHandler<ValidateRoomAccessResultMessage>();
            //LoadOfflineScene();
        }

        #endregion

        #region API

        public virtual void StartClient(RoomAccessPacket receivedAccess)
        {
            bool connectionIsSecured = receivedAccess.ExtraParameters.AsBool(MstParamKeys.ROOM_CONNECTION_IS_SECURE);

            networkManager.networkAddress = receivedAccess.Ip;

            if (networkManager.transport is PortTransport portTransport)
                portTransport.Port = receivedAccess.Port;

            if (networkManager.transport is SimpleWebTransport webTransport)
                webTransport.clientUseWss = connectionIsSecured;

            if (Mst.Client.Rooms.IsClient)
            {
                networkManager.StartClient();
            }
            else
            {
                networkManager.StartHost();
            }
        }

        #endregion
    }
}
#endif
