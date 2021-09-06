using kcp2k;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using Mirror;
using Mirror.SimpleWeb;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomClientManager : RoomClient<RoomClientManager>
    {
        #region INSPECTOR

        /// <summary>
        /// Name of the room that will be loaded after a match is successfully created
        /// </summary>
        [Header("Settings"), SerializeField, Tooltip("The name of the room that will be loaded after the client leaves the room")]
        private string offlineRoomScene = "Client";

        #endregion

        /// <summary>
        /// Mirror network manager
        /// </summary>
        public NetworkManager RoomNetworkManager => NetworkManager.singleton;

        protected override void Start()
        {
            base.Start();

            // Start listening to OnServerStartedEvent of our MirrorNetworkManager
            if (NetworkManager.singleton is RoomNetworkManager manager)
            {
                manager.OnConnectedEvent += Manager_OnConnectedEvent;
                manager.OnDisconnectedEvent += Manager_OnDisconnectedEvent;
            }
            else
            {
                logger.Error($"Before using {typeof(RoomNetworkManager).Name} add it to scene");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (NetworkManager.singleton is RoomNetworkManager manager)
            {
                manager.OnConnectedEvent -= Manager_OnConnectedEvent;
                manager.OnDisconnectedEvent -= Manager_OnDisconnectedEvent;
            }
        }

        protected override void StartConnection(RoomAccessPacket access)
        {
            // Save access
            AccessData = access;

            logger.Info($"Start joining a room at {access.RoomIp}:{access.RoomPort}");

            // Set room IP
            NetworkManager.singleton.networkAddress = access.RoomIp;

            logger.Info($"Custom info: {access.CustomOptions}");

            // Set max connections(This is for Unet and Mirror only
            NetworkManager.singleton.maxConnections = access.RoomMaxConnections;

            // Set room port
            if (Transport.activeTransport is KcpTransport kcpTransport)
            {
                kcpTransport.Port = (ushort)access.RoomPort;
            }
            else if (Transport.activeTransport is TelepathyTransport telepathyTransport)
            {
                telepathyTransport.port = (ushort)access.RoomPort;
            }
            else if (Transport.activeTransport is SimpleWebTransport swTransport)
            {
                swTransport.port = (ushort)access.RoomPort;
            }

            // Start client
            if (!NetworkClient.isConnected)
            {
                NetworkManager.singleton.StartClient();
            }
            else
            {
                logger.Warn($"You have already joined a room at {access.RoomIp}:{access.RoomPort}");
                NetworkManager.singleton.OnClientConnect(NetworkClient.connection);
            }
        }

        /// <summary>
        /// Disconnects client from room server
        /// </summary>
        protected override void StartDisconnection()
        {
            // Stop mirror client
            if (RoomNetworkManager) RoomNetworkManager.StopClient();
        }

        /// <summary>
        /// Invoked when client connected to room server
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void Manager_OnConnectedEvent(NetworkConnection conn)
        {
            logger.Info("Room client has just joined a room server");
            logger.Debug($"Validating access to room server with token [{AccessData.Token}]");

            // Register listener for access validation message from mirror room server
            NetworkClient.RegisterHandler<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler, false);

            // Send validation message to room server
            conn.Send(new ValidateRoomAccessRequestMessage()
            {
                Token = AccessData.Token
            });
        }

        /// <summary>
        /// Invoked when client disconnected from room server
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void Manager_OnDisconnectedEvent(NetworkConnection conn)
        {
            logger.Info("Room client was disconnected from room server");

            NetworkClient.UnregisterHandler<ValidateRoomAccessResultMessage>();
            LoadOfflineScene();
        }

        /// <summary>
        /// Fires when room server send message about access validation result
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="msg"></param>
        protected virtual void ValidateRoomAccessResultHandler(ValidateRoomAccessResultMessage msg)
        {
            if (msg.Status != ResponseStatus.Success)
            {
                logger.Error(msg.Error);
                return;
            }

            logger.Debug("Access to server room is successfully validated");
            LoadOnlineScene();
        }

        /// <summary>
        /// Starts match scene
        /// </summary>
        protected virtual void LoadOnlineScene()
        {
            ScenesLoader.LoadSceneByName(AccessData.SceneName, (progressValue) =>
            {
                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% ... Please wait!");
            },
            () =>
            {
                if (!NetworkClient.ready) NetworkClient.Ready();

                if (RoomNetworkManager.autoCreatePlayer)
                {
                    NetworkClient.AddPlayer();
                }
            });
        }

        /// <summary>
        /// Stops match scene
        /// </summary>
        protected virtual void LoadOfflineScene()
        {
            if (!string.IsNullOrEmpty(offlineRoomScene))
            {
                ScenesLoader.LoadSceneByName(offlineRoomScene, (progressValue) =>
                {
                    Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% ... Please wait!");
                }, null);
            }
        }
    }
}