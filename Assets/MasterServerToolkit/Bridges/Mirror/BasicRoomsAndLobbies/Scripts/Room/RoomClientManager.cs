#if MIRROR
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

            if (RoomNetworkManager is RoomNetworkManager manager)
            {
                manager.OnConnectedEvent -= Manager_OnConnectedEvent;
                manager.OnDisconnectedEvent -= Manager_OnDisconnectedEvent;
            }
        }

        protected override void StartConnection(RoomAccessPacket access)
        {
            if (RoomNetworkManager is RoomNetworkManager manager)
            {
                // Set room IP
                manager.SetAddress(access.RoomIp);
                manager.SetPort(access.RoomPort);

                // Set max connections(This is for Unet and Mirror only
                manager.maxConnections = access.RoomMaxConnections;

                // Start client
                if (!NetworkClient.isConnected)
                {
                    logger.Info($"Start connection to room server at {access.RoomIp}:{access.RoomPort}");
                    manager.StartClient();
                }
                else
                {
                    logger.Info("Already connected");
                }
            }
            else
            {
                logger.Error("Connection error");
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
            logger.Info($"Waitin for access data. Timeout in {roomConnectionTimeout} sec.");

            MstTimer.Instance.WaitWhile(() => !Mst.Client.Rooms.HasAccess, (isSuccess) =>
            {
                if (!isSuccess)
                {
                    logger.Error("Room connection timeout");
                    Disconnect();
                    return;
                }

                logger.Info($"Validating access to room server with token [{Mst.Client.Rooms.ReceivedAccess.Token}]");

                // Register listener for access validation message from mirror room server
                NetworkClient.RegisterHandler<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler, false);

                // Send validation message to room server
                conn.Send(new ValidateRoomAccessRequestMessage()
                {
                    Token = Mst.Client.Rooms.ReceivedAccess.Token
                });

                //logger.Info($"You have joined a room at {Mst.Client.Rooms.ReceivedAccess.RoomIp}:{Mst.Client.Rooms.ReceivedAccess.RoomPort}");
            }, roomConnectionTimeout);
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
            ScenesLoader.LoadSceneByName(Mst.Client.Rooms.ReceivedAccess.SceneName, (progressValue) =>
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
#endif