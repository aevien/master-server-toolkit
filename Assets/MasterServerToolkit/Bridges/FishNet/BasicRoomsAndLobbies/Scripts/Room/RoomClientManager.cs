#if FISHNET
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Managing.Scened;
using FishNet.Managing.Transporting;
using FishNet.Transporting;
using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.Bridges.FishNetworking
{
    public class RoomClientManager : RoomClient<RoomClientManager>
    {
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

        protected override void Start()
        {
            base.Start();

            // Start listening to connection and scenes loaded events for client.
            if (_networkManager != null)
            {
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

        private void _sceneManager_OnLoadStart(SceneLoadStartEventArgs obj)
        {
            //Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene 0% ... Please wait!");
        }

        private void _sceneManager_OnLoadPercentChange(SceneLoadPercentEventArgs obj)
        {
            //Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(obj.Percent * 100f)}% ... Please wait!");
        }

        private void _sceneManager_OnLoadEnd(SceneLoadEndEventArgs obj)
        {
            //MstTimer.Instance.WaitForSeconds(1f, () =>
            //{
            //    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
            //});
        }

        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                logger.Info("Room client was disconnected from room server");

                _clientManager?.UnregisterBroadcast<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler);
            }
            else if (state.ConnectionState == LocalConnectionState.Started)
            {
                logger.Info("Room client has just joined a room server");
                logger.Debug($"Validating access to room server with token [{Mst.Client.Rooms.ReceivedAccess.Token}]");

                // Register listener for access validation message from room server
                _clientManager.RegisterBroadcast<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler);

                // Send validation message to room server
                _clientManager.Broadcast(new ValidateRoomAccessRequestMessage()
                {
                    Token = Mst.Client.Rooms.ReceivedAccess.Token
                });
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
            if (_clientManager && !_clientManager.Started)
            {
                logger.Info($"Start joining a room at {access.RoomIp}:{access.RoomPort}");
                logger.Info($"Custom info: {access.CustomOptions}");

                _clientManager.StartConnection(access.RoomIp, (ushort)access.RoomPort);
            }
        }

        /// <summary>
        /// Disconnects client from room server
        /// </summary>
        protected override void StartDisconnection()
        {
            _clientManager?.StopConnection();
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