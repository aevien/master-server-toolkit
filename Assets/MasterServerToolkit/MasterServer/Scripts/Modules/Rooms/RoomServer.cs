using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class RoomServer : ServerBehaviour
    {
        [Header("Components"), SerializeField]
        private RoomServerManager roomServerManager;

        protected override void Awake()
        {
            base.Awake();
            autoStartInEditor = false;
            RegisterMessageHandler((short)MstMessageCodes.ValidateRoomAccessRequest, ValidateRoomAccessRequestHandler);
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

            MstTimer.WaitForSeconds(1f, () =>
            {
                Mst.Runtime.Quit();
            });
        }

        protected override void OnStartedServer()
        {
            logger.Info($"Room Server started and listening to: {serverIP}:{serverPort}");
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
                    try
                    {
                        if (!isSuccess)
                        {
                            throw new MstMessageHandlerException(error, ResponseStatus.Failed);
                        }

                        message.Respond(ResponseStatus.Success);
                    }
                    // If we got system exception
                    catch (MstMessageHandlerException e)
                    {
                        logger.Error(e.Message);
                        message.Respond(e.Message, e.Status);
                        MstTimer.WaitForSeconds(1f, () =>
                        {
                            message.Peer.Disconnect("Unauthorized access to room was rejected");
                        });
                    }
                    // If we got another exception
                    catch (Exception e)
                    {
                        logger.Error(e.Message);
                        message.Respond(e.Message, ResponseStatus.Error);
                        MstTimer.WaitForSeconds(1f, () =>
                        {
                            message.Peer.Disconnect(e.Message);
                        });
                    }
                });
            }
        }

        #endregion
    }
}