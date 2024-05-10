#if MIRROR
using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

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

        private string currentOnlineRoomScene = string.Empty;
        private string currentOfflineRoomScene = string.Empty;

        /// <summary>
        /// Mirror network manager
        /// </summary>
        protected RoomNetworkManager NetworkManager => Mirror.NetworkManager.singleton as RoomNetworkManager;

        protected override void Awake()
        {
            base.Awake();
            currentOfflineRoomScene = offlineRoomScene;
        }

        protected override void Start()
        {
            base.Start();

            if (NetworkManager)
            {
                NetworkManager.OnConnectedEvent += NetworkManager_OnConnectedEvent;
                NetworkManager.OnDisconnectedEvent += NetworkManager_OnDisconnectedEvent;
            }
            else
            {
                logger.Error($"Before using {typeof(RoomNetworkManager).Name} add it to scene");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (NetworkManager)
            {
                NetworkManager.OnConnectedEvent -= NetworkManager_OnConnectedEvent;
                NetworkManager.OnDisconnectedEvent -= NetworkManager_OnDisconnectedEvent;
            }
        }

        private void OnApplicationQuit()
        {
            StartDisconnection();
        }

        protected override void StartConnection(RoomAccessPacket access)
        {
            if (isChangingZone)
                StartDisconnection();

            if (NetworkManager && !NetworkClient.isConnected)
            {
                // Set room IP
                NetworkManager.SetAddress(access.RoomIp);
                NetworkManager.SetPort(access.RoomPort);

                logger.Info($"Start joining a room at {access.RoomIp}:{access.RoomPort}. Scene: {access.SceneName}");
                logger.Info($"Custom info: {access.CustomOptions}");

                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Start joining a room at {access.RoomIp}:{access.RoomPort}");
                NetworkManager.StartClient();
            }
            else
            {
                logger.Info("Already connected");
            }
        }

        /// <summary>
        /// Disconnects client from room server
        /// </summary>
        protected override void StartDisconnection()
        {
            if (isChangingZone)
                currentOfflineRoomScene = SceneManager.GetActiveScene().name;
            else
                currentOfflineRoomScene = offlineRoomScene;

            if (NetworkManager)
                NetworkManager.StopClient();

            isChangingZone = false;
        }

        /// <summary>
        /// Invoked when client connected to room server
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void NetworkManager_OnConnectedEvent(NetworkConnection conn)
        {
            logger.Info($"Waiting for access data. Timeout in {roomConnectionTimeout} sec.");

            MstTimer.WaitWhile(() => !Mst.Client.Rooms.HasAccess, (isSuccess) =>
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

                logger.Info($"You have joined the room at {Mst.Client.Rooms.ReceivedAccess.RoomIp}:{Mst.Client.Rooms.ReceivedAccess.RoomPort}");
            }, roomConnectionTimeout);
        }

        /// <summary>
        /// Invoked when client disconnected from room server
        /// </summary>
        /// <param name="conn"></param>
        protected virtual void NetworkManager_OnDisconnectedEvent(NetworkConnection conn)
        {
            logger.Info("You have just been disconnected from the server");

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
            currentOnlineRoomScene = Mst.Client.Rooms.ReceivedAccess.SceneName;

            logger.Info($"Loading online scene {currentOnlineRoomScene}".ToGreen());

            ScenesLoader.LoadSceneByName(currentOnlineRoomScene, (progressValue) =>
            {
                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% Please wait!");
            },
            () =>
            {
                if (!NetworkClient.ready)
                    NetworkClient.Ready();

                if (NetworkManager.autoCreatePlayer)
                    NetworkClient.AddPlayer();
            });
        }

        /// <summary>
        /// Stops match scene
        /// </summary>
        protected virtual void LoadOfflineScene()
        {
            if (!string.IsNullOrEmpty(currentOfflineRoomScene))
            {
                ScenesLoader.LoadSceneByName(currentOfflineRoomScene, (progressValue) =>
                {
                    Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% ... Please wait!");
                }, null);
            }
        }
    }
}
#endif