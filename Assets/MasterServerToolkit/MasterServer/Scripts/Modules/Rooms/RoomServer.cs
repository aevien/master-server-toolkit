using MasterServerToolkit.Networking;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class RoomServer : ServerBehaviour
    {
        [Header("Components"), SerializeField]
        private RoomServerManager roomServerManager;

        protected override void Start()
        {
            base.Start();

            autoStartInEditor = false;
            RegisterMessageHandler(MstOpCodes.ValidateRoomAccessRequest, ValidateRoomAccessRequestHandler);
        }

        public override void StartServer()
        {
            // Find room server if it is not assigned in inspector
            if (!roomServerManager) roomServerManager = GetComponent<RoomServerManager>();

            // Set the max number of connections
            maxConnections = (ushort)roomServerManager.RoomOptions.MaxConnections;

            // Start server with room options
            StartServer(roomServerManager.RoomOptions.RoomIp, roomServerManager.RoomOptions.RoomPort);
        }

        public override void StopServer()
        {
            base.StopServer();

            MstTimer.Instance.WaitForSeconds(1f, () =>
            {
                Mst.Runtime.Quit();
            });
        }

        protected override void OnStartedServer()
        {
            logger.Info($"Room Server started and listening to: {serverIp}:{serverPort}");
            base.OnStartedServer();
            if (roomServerManager) roomServerManager.OnServerStarted();
        }

        protected override void OnStoppedServer()
        {
            logger.Info("Room Server stopped");
            base.OnStoppedServer();
            if (roomServerManager) roomServerManager.OnServerStopped();
        }

        protected override void OnPeerDisconnected(IPeer peer)
        {
            if (roomServerManager) roomServerManager.OnPeerDisconnected(peer.Id);
        }

        #region MESSAGE_HANDLERS

        protected virtual void ValidateRoomAccessRequestHandler(IIncomingMessage message)
        {
            if (roomServerManager)
            {
                roomServerManager.ValidateRoomAccess(message.Peer.Id, message.AsString(), (isSuccess, error) =>
                {
                    if (!isSuccess)
                    {
                        logger.Error("Unauthorized access to room was rejected");
                        message.Peer.Disconnect("Unauthorized access to room was rejected");
                    }

                    message.Respond(ResponseStatus.Success);
                });
            }
            else
            {
                message.Peer.Disconnect("Room is invalid");
            }
        }

        #endregion
    }
}