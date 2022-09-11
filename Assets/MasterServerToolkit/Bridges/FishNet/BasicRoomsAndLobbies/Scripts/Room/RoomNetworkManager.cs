#if FISHNET
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

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Network Manager Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

        public static RoomNetworkManager Instance { get; private set; }
        /// <summary>
        /// Logger assigned to this module
        /// </summary>
        protected Logging.Logger logger;
        /// <summary>
        /// The NetworkManager on this object.
        /// </summary>
        private NetworkManager _networkManager;
        /// <summary>
        /// 
        /// </summary>
        protected RoomServerManager roomServerManager;
        /// <summary>
        /// 
        /// </summary>
        protected DefaultScene defaultScene;

        private void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            _networkManager = GetComponent<NetworkManager>();
        }

        private void Start()
        {
            if (!defaultScene)
                defaultScene = GetComponent<DefaultScene>();

            _networkManager.ServerManager.OnServerConnectionState += ServerManager_OnServerConnectionState;
            _networkManager.ServerManager.OnRemoteConnectionState += ServerManager_OnRemoteConnectionState;
        }

        public void StartRoomServer()
        {
            // Find room server if it is not assigned in inspector
            if (!roomServerManager)
                roomServerManager = GetComponent<RoomServerManager>();

            string onlineScene = Mst.Args.AsString(Mst.Args.Names.RoomOnlineScene, SceneManager.GetActiveScene().name);
            defaultScene.SetOnlineScene(onlineScene);

            TransportManager transportManager = _networkManager.TransportManager;
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
            _networkManager.ServerManager.StartConnection();
        }

        public void StopRoomServer()
        {
            _networkManager.ServerManager.StopConnection(true);
        }

        #region FISHNET CALLBACKS

        private void ServerManager_OnServerConnectionState(ServerConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Started)
            {
                logger.Info($"Room Server started and listening to: {_networkManager.TransportManager.Transport.GetServerBindAddress(IPAddressType.IPv4)}:{roomServerManager.RoomOptions.RoomPort}");

                _networkManager.ServerManager.RegisterBroadcast<ValidateRoomAccessRequestMessage>(ValidateRoomAccessRequestHandler);

                if (roomServerManager) roomServerManager.OnServerStarted();
            }
            else if (state.ConnectionState == LocalConnectionState.Stopped)
            {

                logger.Info("Room Server stopped");

                _networkManager.ServerManager.UnregisterBroadcast<ValidateRoomAccessRequestMessage>(ValidateRoomAccessRequestHandler);

                if (roomServerManager) roomServerManager.OnServerStopped();
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

        private void ValidateRoomAccessRequestHandler(NetworkConnection conn, ValidateRoomAccessRequestMessage mess)
        {
            if (roomServerManager)
            {
                roomServerManager.ValidateRoomAccess(conn.ClientId, mess.Token, (isSuccess, error) =>
                {
                    try
                    {
                        if (!isSuccess)
                        {
                            Debug.LogError(error);
                            conn.Broadcast(new ValidateRoomAccessResultMessage()
                            {
                                Error = error,
                                Status = ResponseStatus.Error
                            });

                            MstTimer.WaitForSeconds(1f, () => conn.Disconnect(true));

                            return;
                        }

                        conn.Broadcast(new ValidateRoomAccessResultMessage()
                        {
                            Error = string.Empty,
                            Status = ResponseStatus.Success
                        });
                    }
                    // If we got another exception
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                        conn.Broadcast(new ValidateRoomAccessResultMessage()
                        {
                            Error = e.Message,
                            Status = ResponseStatus.Error
                        });

                        MstTimer.WaitForSeconds(1f, () => conn.Disconnect(true));
                    }
                });
            }
        }
    }
}
#endif