#if FISHNET
using FishNet;
using FishNet.Component.Scenes;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Scened;
using FishNet.Transporting;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
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
        private NetworkManager NetworkManager => InstanceFinder.NetworkManager;
        /// <summary>
        /// InstanceFinder.ClientManager.
        /// </summary>
        private ClientManager ClientManager => InstanceFinder.ClientManager;
        /// <summary>
        /// InstanceFinder.SceneManager.
        /// </summary>
        private SceneManager SceneManager => InstanceFinder.SceneManager;

        private DefaultScene defaultScene;
        private LocalConnectionState connectionState;

        protected override void Start()
        {
            base.Start();

            // Start listening to connection and scenes loaded events for client.
            if (NetworkManager != null)
            {
                defaultScene = NetworkManager.GetComponent<DefaultScene>();

                ClientManager.OnClientConnectionState += ClientManager_OnClientConnectionState;
                SceneManager.OnLoadStart += SceneManager_OnLoadStart;
                SceneManager.OnLoadPercentChange += SceneManager_OnLoadPercentChange;
                SceneManager.OnLoadEnd += SceneManager_OnLoadEnd;
            }
            else
            {
                logger.Error($"Before using {typeof(RoomNetworkManager).Name} add it to scene");
            }
        }

        private void SceneManager_OnLoadStart(SceneLoadStartEventArgs _)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene 0% ... Please wait!");
        }

        private void SceneManager_OnLoadPercentChange(SceneLoadPercentEventArgs args)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(args.Percent * 100f)}% ... Please wait!");
        }

        private void SceneManager_OnLoadEnd(SceneLoadEndEventArgs _)
        {
            Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
        }

        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs state)
        {
            connectionState = state.ConnectionState;

            if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                logger.Info("You have just been disconnected from the server");

                ClientManager.UnregisterBroadcast<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler);
            }
            else if (state.ConnectionState == LocalConnectionState.Started)
            {
                logger.Debug($"Validating access to room server with token [{Mst.Client.Rooms.ReceivedAccess.Token}]");

                // Register listener for access validation message from room server
                ClientManager.RegisterBroadcast<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler);

                // Send validation message to room server
                ClientManager.Broadcast(new ValidateRoomAccessRequestMessage()
                {
                    Token = Mst.Client.Rooms.ReceivedAccess.Token
                });

                logger.Info($"You have joined the room at {Mst.Client.Rooms.ReceivedAccess.RoomIp}:{Mst.Client.Rooms.ReceivedAccess.RoomPort}");
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (NetworkManager != null)
            {
                ClientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
                SceneManager.OnLoadStart -= SceneManager_OnLoadStart;
                SceneManager.OnLoadPercentChange -= SceneManager_OnLoadPercentChange;
                SceneManager.OnLoadEnd -= SceneManager_OnLoadEnd;
            }
        }

        protected override void StartConnection(RoomAccessPacket access)
        {
            if (isChangingZone)
                StartDisconnection();

            if (ClientManager && !ClientManager.Started)
            {
                defaultScene.SetOnlineScene(access.SceneName);

                logger.Info($"Start joining a room at {access.RoomIp}:{access.RoomPort}. Scene: {access.SceneName}");
                logger.Info($"Custom info: {access.CustomOptions}");

                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Start joining a room at {access.RoomIp}:{access.RoomPort}");

                MstTimer.WaitWhile(() => connectionState != LocalConnectionState.Stopped, (isSuccess) =>
                {
                    ClientManager.StartConnection(access.RoomIp, access.RoomPort);
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

            if (ClientManager)
                ClientManager.StopConnection();

            isChangingZone = false;
        }

        /// <summary>
        /// Fires when room server send message about access validation result
        /// </summary>
        /// <param name="message"></param>
        /// <param name="channel"></param>
        protected virtual void ValidateRoomAccessResultHandler(ValidateRoomAccessResultMessage message, Channel channel)
        {
            if (message.Status != ResponseStatus.Success)
            {
                logger.Error(message.Error);
                StartDisconnection();
                return;
            }

            logger.Debug("Access to server room is successfully validated");
        }
    }
}

#endif