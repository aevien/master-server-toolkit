#if FISHNET
using FishNet.Component.Scenes;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.Bridges.FishNetworking
{
    public class RoomNetworkManager : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField, Tooltip("Required MST room manager that owns room registration, connection limits, access validation, and peer-disconnect notifications. If empty, Awake resolves it from the same GameObject.")]
        protected RoomServerManager roomServerManager;
        [SerializeField, Tooltip("Required FishNet NetworkManager used to start and stop the room server and access transport, scene, server, and client managers. If empty, Awake resolves it from the same GameObject.")]
        protected NetworkManager networkManager;

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Settings"), SerializeField, Tooltip("Minimum severity written by the FishNet room manager's MST logger. This changes diagnostics only and does not affect network behavior.")]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;
        /// <summary>
        /// 
        /// </summary>
        protected DefaultScene defaultScene;

        private void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            if (networkManager == null)
                networkManager = GetComponent<NetworkManager>();

            if (roomServerManager == null)
                roomServerManager = GetComponent<RoomServerManager>();
        }

        private void Start()
        {
            if (!defaultScene)
                defaultScene = GetComponent<DefaultScene>();

            networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        }

        public void StartRoomServer()
        {
            string onlineScene = Mst.Args.AsString(Mst.Args.Names.RoomOnlineScene, SceneManager.GetActiveScene().name);
            defaultScene.SetOnlineScene(onlineScene);

            TransportManager transportManager = networkManager.TransportManager;
            Transport transport = transportManager.Transport;

            // Set the max number of connections
            transport.SetMaximumClients(roomServerManager.RoomOptions.MaxConnections);
            // Set room IP just for information purpose only
            transport.SetClientAddress(roomServerManager.RoomOptions.RoomIp);
            // Set room port
            transport.SetPort(roomServerManager.RoomOptions.RoomPort);

            logger.Info($"Starting Room Server: {transport.GetServerBindAddress(IPAddressType.IPv4)}:{roomServerManager.RoomOptions.RoomPort}");
            logger.Info($"Online Scene: {onlineScene}");

            // Start server
            networkManager.ServerManager.StartConnection();
        }

        public void StopRoomServer()
        {
            networkManager.ServerManager.StopConnection(true);
        }

        #region FISHNET CALLBACKS

        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Started)
            {
                logger.Info($"Room Server started and listening to: {networkManager.TransportManager.Transport.GetServerBindAddress(IPAddressType.IPv4)}:{roomServerManager.RoomOptions.RoomPort}");

                networkManager.ServerManager.RegisterBroadcast<ValidateRoomAccessRequestMessage>(ValidateRoomAccessRequestHandler);

                if (roomServerManager != null)
                {
                    roomServerManager.OnServerStarted();
                }
            }
            else if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                logger.Info("Room Server stopped");

                networkManager.ServerManager.UnregisterBroadcast<ValidateRoomAccessRequestMessage>(ValidateRoomAccessRequestHandler);

                if (roomServerManager)
                {
                    roomServerManager.OnServerStopped();
                }
            }
        }

        private void ServerManager_OnRemoteConnectionState(NetworkConnection con, RemoteConnectionStateArgs state)
        {
            if (state.ConnectionState == RemoteConnectionState.Stopped)
            {
                logger.Info($"Client {con.ClientId} has just left the room");

                if (roomServerManager)
                    roomServerManager.OnPeerDisconnected(con.ClientId);
            }

            if (state.ConnectionState == RemoteConnectionState.Started)
            {
                logger.Info($"Client {con.ClientId} has just joined the room");
            }
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        private void ValidateRoomAccessRequestHandler(NetworkConnection connection, ValidateRoomAccessRequestMessage message, Channel channel)
        {
            if (!roomServerManager)
            {
                logger.Error("Room access validation is unavailable because the room manager is missing");
                connection.Broadcast(CreateErrorResult(
                    ResponseStatus.NotConnected,
                    MstErrorCodes.ROOM_ACCESS_UNAVAILABLE));
                MstTimer.WaitForSeconds(1f, () => connection.Disconnect(true));
                return;
            }

            roomServerManager.ValidateRoomAccess(connection.ClientId, message.Token, (isSuccess, error) =>
            {
                try
                {
                    if (!isSuccess)
                    {
                        logger.Warn("Room access validation rejected the client");
                        connection.Broadcast(CreateErrorResult(
                            ResponseStatus.Unauthorized,
                            MstErrorCodes.ROOM_ACCESS_DENIED));

                        MstTimer.WaitForSeconds(1f, () => connection.Disconnect(true));
                        return;
                    }

                    connection.Broadcast(new ValidateRoomAccessResultMessage
                    {
                        Status = ResponseStatus.Success
                    });
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    connection.Broadcast(CreateErrorResult(
                        ResponseStatus.Error,
                        MstErrorCodes.INTERNAL_ERROR));

                    MstTimer.WaitForSeconds(1f, () => connection.Disconnect(true));
                }
            });
        }

        private static ValidateRoomAccessResultMessage CreateErrorResult(
            ResponseStatus status,
            string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);

            return new ValidateRoomAccessResultMessage
            {
                Error = properties.ToBytes(),
                Status = status
            };
        }
    }
}
#endif
