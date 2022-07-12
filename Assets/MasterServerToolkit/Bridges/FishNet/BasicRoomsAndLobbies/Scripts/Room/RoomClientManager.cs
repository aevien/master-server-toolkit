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
                _sceneManager.OnClientLoadedStartScenes += SceneManager_OnClientLoadedStartScenes;
            }
            else
            {
                logger.Error($"Before using {typeof(RoomNetworkManager).Name} add it to scene");
            }
        }

        /// <summary>
        /// Called when a client loads the starting scenes.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="asServer"></param>
        private void SceneManager_OnClientLoadedStartScenes(NetworkConnection conn, bool asServer)
        {
            if (conn.IsLocalClient)
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

        private void ClientManager_OnClientConnectionState(ClientConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                logger.Info("Room client was disconnected from room server");
                _clientManager?.UnregisterBroadcast<ValidateRoomAccessResultMessage>(ValidateRoomAccessResultHandler);
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_networkManager != null)
            {
                _clientManager.OnClientConnectionState -= ClientManager_OnClientConnectionState;
                _sceneManager.OnClientLoadedStartScenes -= SceneManager_OnClientLoadedStartScenes;
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