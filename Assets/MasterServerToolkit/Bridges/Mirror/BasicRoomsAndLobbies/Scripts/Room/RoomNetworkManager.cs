#if MIRROR
using kcp2k;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using Mirror;
using Mirror.SimpleWeb;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomNetworkManager : NetworkManager
    {
        #region INSPECTOR

        [Header("Mirror Network Manager Components"), SerializeField]
        protected RoomServerManager roomServerManager;

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Mirror Network Manager Settings"), SerializeField]
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
        public event Action<NetworkConnection> OnDisconnectedEvent;

        /// <summary>
        /// This is called on the Server when a Client connects the Server
        /// </summary>
        public event Action<NetworkConnection> OnClientConnectedEvent;

        /// <summary>
        /// This is called on the Server when a Client disconnects from the Server
        /// </summary>
        public event Action<NetworkConnection> OnClientDisconnectedEvent;

        public override void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            // Prevent start network manager in headless mode automatically
            autoStartServerBuild = false;

            base.Awake();
        }

        public void StartRoomServer()
        {
            // Find room server if it is not assigned in inspector
            if (!roomServerManager) roomServerManager = GetComponent<RoomServerManager>();

            // Set the max number of connections
            maxConnections = (ushort)roomServerManager.RoomOptions.MaxConnections;

            // Set room IP just for information purpose only
            SetAddress(roomServerManager.RoomOptions.RoomIp);
            // Set room port
            SetPort(roomServerManager.RoomOptions.RoomPort);

            logger.Info($"Starting Room Server: {networkAddress}:{roomServerManager.RoomOptions.RoomPort}");

#if UNITY_EDITOR
            StartHost();
#else
            StartServer();
#endif
        }

        public void StopRoomServer()
        {
            StopServer();

            MstTimer.Instance.WaitForSeconds(1f, () =>
            {
                Mst.Runtime.Quit();
            });
        }

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
            if (Transport.activeTransport is KcpTransport kcpTransport)
            {
                kcpTransport.Port = (ushort)port;
            }
            else if (Transport.activeTransport is TelepathyTransport telepathyTransport)
            {
                telepathyTransport.port = (ushort)port;
            }
            else if (Transport.activeTransport is SimpleWebTransport swTransport)
            {
                swTransport.port = (ushort)port;
            }
        }

        #region MIRROR SERVER

        /// <summary>
        /// When mirror server is started
        /// </summary>
        public override void OnStartServer()
        {
            base.OnStartServer();

            logger.Info($"Room Server started and listening to: {networkAddress}:{roomServerManager.RoomOptions.RoomPort}");

            NetworkServer.RegisterHandler<ValidateRoomAccessRequestMessage>(ValidateRoomAccessRequestHandler, false);

            if (roomServerManager) roomServerManager.OnServerStarted();
            OnServerStartedEvent?.Invoke();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();

            logger.Info("Room Server stopped");

            NetworkServer.UnregisterHandler<ValidateRoomAccessRequestMessage>();

            if (roomServerManager) roomServerManager.OnServerStopped();
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
            if (roomServerManager) roomServerManager.OnPeerDisconnected(conn.connectionId);
            OnClientDisconnectedEvent?.Invoke(conn);
        }

        #endregion

        #region MIRROR CLIENT

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
            OnDisconnectedEvent?.Invoke(NetworkClient.connection);
        }

        #endregion

        #region ROOM SERVER

        private void ValidateRoomAccessRequestHandler(NetworkConnection conn, ValidateRoomAccessRequestMessage mess)
        {
            if (roomServerManager)
            {
                roomServerManager.ValidateRoomAccess(conn.connectionId, mess.Token, (isSuccess, error) =>
                {
                    try
                    {
                        if (!isSuccess)
                        {
                            logger.Error(error);

                            conn.Send(new ValidateRoomAccessResultMessage()
                            {
                                Error = error,
                                Status = ResponseStatus.Failed
                            });

                            MstTimer.Instance.WaitForSeconds(1f, () => conn.Disconnect());
                            return;
                        }

                        conn.Send(new ValidateRoomAccessResultMessage()
                        {
                            Error = string.Empty,
                            Status = ResponseStatus.Success
                        });
                    }
                    // If we got another exception
                    catch (Exception e)
                    {
                        Debug.LogError(e.Message);
                        conn.Send(new ValidateRoomAccessResultMessage()
                        {
                            Error = e.Message,
                            Status = ResponseStatus.Error
                        });

                        MstTimer.Instance.WaitForSeconds(1f, () => conn.Disconnect());
                    }
                });
            }
        }

        #endregion
    }
}
#endif