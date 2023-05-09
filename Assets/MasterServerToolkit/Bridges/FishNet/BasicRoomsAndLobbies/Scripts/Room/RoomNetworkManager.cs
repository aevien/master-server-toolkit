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

        [Header("Components"), SerializeField]
        protected RoomServerManager roomServerManager;
        [SerializeField]
        protected NetworkManager networkManager;

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Settings"), SerializeField]
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