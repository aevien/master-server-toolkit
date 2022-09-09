#if FISHNET
using FishNet;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using UnityEngine;
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

namespace MasterServerToolkit.Bridges.FishNetworking
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
        /// InstanceFinder.NetworkManager.
        /// </summary>
        private NetworkManager _networkManager => InstanceFinder.NetworkManager;
        /// <summary>
        /// InstanceFinder.ClientManager.
        /// </summary>
        private ClientManager _clientManager => InstanceFinder.ClientManager;
        /// <summary>
        /// InstanceFinder.SceneManager.
        /// </summary>
        private SceneManager _sceneManager => InstanceFinder.SceneManager;

        private DefaultScene defaultScene;
        private LocalConnectionState connectionState;

        protected override void Start()
        {
            base.Start();

            // Start listening to connection and scenes loaded events for client.
            if (_networkManager != null)
            {
                defaultScene = _networkManager.GetComponent<DefaultScene>();

                _clientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
                _sceneManager.OnLoadStart += _sceneManager_OnLoadStart;
                _sceneManager.OnLoadPercentChange += _sceneManager_OnLoadPercentChange;
                _sceneManager.OnLoadEnd += _sceneManager_OnLoadEnd;
            }
            else
            {
                logger.Error($"Before using {typeof(RoomNetworkManager).Name} add it to scene");
            }
        }

        private void _sceneManager_OnLoadStart(SceneLoadStartEventArgs _)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene 0% ... Please wait!");
        }

        private void _sceneManager_OnLoadPercentChange(SceneLoadPercentEventArgs args)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(args.Percent * 100f)}% ... Please wait!");
        }

        private void _sceneManager_OnLoadEnd(SceneLoadEndEventArgs _)
        {
            Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
        }

        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs state)
        {
            connectionState = state.ConnectionState;

            if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                logger.Info("You have just been disconnected from the server");

                _clientManager.UnregisterBroadcast<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler);
            }
            else if (state.ConnectionState == LocalConnectionState.Started)
            {
                logger.Debug($"Validating access to room server with token [{Mst.Client.Rooms.ReceivedAccess.Token}]");

                // Register listener for access validation message from room server
                _clientManager.RegisterBroadcast<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler);

                // Send validation message to room server
                _clientManager.Broadcast(new ValidateRoomAccessRequestMessage()
                {
                    Token = Mst.Client.Rooms.ReceivedAccess.Token
                });

                logger.Info($"You have joined the room at {Mst.Client.Rooms.ReceivedAccess.RoomIp}:{Mst.Client.Rooms.ReceivedAccess.RoomPort}");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_networkManager != null)
            {
                _clientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
                _sceneManager.OnLoadStart -= _sceneManager_OnLoadStart;
                _sceneManager.OnLoadPercentChange -= _sceneManager_OnLoadPercentChange;
                _sceneManager.OnLoadEnd -= _sceneManager_OnLoadEnd;
            }
        }

        protected override void StartConnection(RoomAccessPacket access)
        {
            if (isChangingZone)
                StartDisconnection();

            if (_clientManager && !_clientManager.Started)
            {
                defaultScene.SetOnlineScene(access.SceneName);

                logger.Info($"Start joining a room at {access.RoomIp}:{access.RoomPort}. Scene: {access.SceneName}");
                logger.Info($"Custom info: {access.CustomOptions}");

                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Start joining a room at {access.RoomIp}:{access.RoomPort}");

                MstTimer.WaitWhile(() => connectionState != LocalConnectionState.Stopped, (isSuccess) =>
                {
                    _clientManager.StartConnection(access.RoomIp, access.RoomPort);
                }, 10f);
            }
        }

        /// <summary>
        /// Disconnects client from room server
        /// </summary>
        protected override void StartDisconnection()
        {
            if (isChangingZone)
                defaultScene.SetOfflineScene(UnitySceneManager.GetActiveScene().name);
            else
                defaultScene.SetOfflineScene(offlineRoomScene);

            if (_clientManager)
                _clientManager.StopConnection();

            isChangingZone = false;
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
                StartDisconnection();
                return;
            }

            logger.Debug("Access to server room is successfully validated");
        }
    }
}

#endif