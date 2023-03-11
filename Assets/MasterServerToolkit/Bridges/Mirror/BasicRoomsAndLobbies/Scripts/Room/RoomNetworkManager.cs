#if MIRROR
using kcp2k;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using Mirror;
using Mirror.SimpleWeb;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomNetworkManager : NetworkManager
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        protected RoomServerManager roomServerManager;

        /// <summary>
        /// Log levelof this module
        /// </summary>
        [Header("Settings"), SerializeField]
        protected LogLevel logLevel = LogLevel.Info;

        #endregion

        public static new RoomNetworkManager singleton { get; private set; }

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

        /// <summary>
        /// 
        /// </summary>
        public event Action OnClientSceneChangedEvent;

        /// <summary>
        /// Runs on both Server and Client
        /// Networking is NOT initialized when this fires
        /// </summary>
        public override void Awake()
        {
            base.Awake();

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            if (roomServerManager == null)
                roomServerManager = GetComponent<RoomServerManager>();

            // Prevent start network manager in headless mode automatically
            autoStartServerBuild = false;
        }

        public void StartRoomServer()
        {
            // Set online scene
            onlineScene = Mst.Args.AsString(Mst.Args.Names.RoomOnlineScene, SceneManager.GetActiveScene().name);

            // Set the max number of connections
            maxConnections = roomServerManager.RoomOptions.MaxConnections;

            // Set room IP just for information purpose only
            SetAddress(roomServerManager.RoomOptions.RoomIp);

            // Set room port
            SetPort(roomServerManager.RoomOptions.RoomPort);

            logger.Info($"Starting Room Server: {networkAddress}:{roomServerManager.RoomOptions.RoomPort}");
            logger.Info($"Online Scene: {onlineScene}");

#if UNITY_EDITOR
            StartHost();
#else
            StartServer();
#endif
        }

        public void StopRoomServer()
        {
            StopServer();

            MstTimer.WaitForSeconds(1f, () =>
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
            if (Transport.active is KcpTransport kcpTransport)
            {
                kcpTransport.Port = (ushort)port;
            }
            else if (Transport.active is TelepathyTransport telepathyTransport)
            {
                telepathyTransport.port = (ushort)port;
            }
            else if (Transport.active is SimpleWebTransport swTransport)
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
            logger.Info($"Room Server started and listening to: {networkAddress}:{roomServerManager.RoomOptions.RoomPort}");

            base.OnStartServer();
            NetworkServer.RegisterHandler<ValidateRoomAccessRequestMessage>(ValidateRoomAccessRequestHandler, false);
            if (roomServerManager) roomServerManager.OnServerStarted();
            OnServerStartedEvent?.Invoke();
        }

        public override void OnStopServer()
        {
            logger.Info("Room Server stopped");

            base.OnStopServer();
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

        /// <summary>
        /// Called from ClientChangeScene immediately before SceneManager.LoadSceneAsync is executed
        /// <para>This allows client to do work / cleanup / prep before the scene changes.</para>
        /// </summary>
        /// <param name="newSceneName">Name of the scene that's about to be loaded</param>
        /// <param name="sceneOperation">Scene operation that's about to happen</param>
        /// <param name="customHandling">true to indicate that scene loading will be handled through overrides</param>
        public override void OnClientChangeScene(string newSceneName, SceneOperation sceneOperation, bool customHandling)
        {
            logger.Info($"Client is loading scene {newSceneName}");
        }

        /// <summary>
        /// Called on clients when a scene has completed loaded, when the scene load was initiated by the server.
        /// <para>Scene changes can cause player objects to be destroyed. The default implementation of OnClientSceneChanged in the NetworkManager is to add a player object for the connection if no player object exists.</para>
        /// </summary>
        public override void OnClientSceneChanged()
        {
            logger.Info($"Client scene loaded");
            OnClientSceneChangedEvent?.Invoke();
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

                            MstTimer.WaitForSeconds(1f, () => conn.Disconnect());
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

                        MstTimer.WaitForSeconds(1f, () => conn.Disconnect());
                    }
                });
            }
        }

        #endregion
    }
}
#endif