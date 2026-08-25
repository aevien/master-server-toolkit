using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using Mirror;
using System;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public class RoomAuthenticator : NetworkAuthenticator
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField, Tooltip("Minimum severity written by the room authenticator's MST logger. This changes diagnostics only and does not affect access decisions.")]
        protected LogLevel logLevel = LogLevel.Info;

        [Header("Components"), SerializeField, Tooltip("Required MST room manager used on the server to validate each client's room access token and report room availability.")]
        private RoomServerManager roomManager;
        [SerializeField, Tooltip("Mirror room network manager associated with this authenticator. If empty, Awake resolves it from the same GameObject.")]
        protected RoomNetworkManager networkManager;

        #endregion

        protected Logging.Logger logger;
        protected float hostWaitTimeOut = 60f;

        #region UNITY

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            if (networkManager == null)
                networkManager = GetComponent<RoomNetworkManager>();
        }

        #endregion

        #region SERVER

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<ValidateRoomAccessRequestMessage>(OnValidateRoomAccessRequestMessage, false);
            roomManager.StartServer();
        }

        public override void OnStopServer()
        {
            NetworkClient.UnregisterHandler<ValidateRoomAccessRequestMessage>();
            roomManager.StopServer();
        }

        private void OnValidateRoomAccessRequestMessage(NetworkConnectionToClient client, ValidateRoomAccessRequestMessage message)
        {
            MstTimer.WaitUntil(() => roomManager.IsActive, (success) =>
            {
                if (!success)
                {
                    logger.Error("Room access validation is unavailable because the room server did not become active in time");
                    client.Send(CreateErrorResult(
                        ResponseStatus.NotConnected,
                        MstErrorCodes.ROOM_ACCESS_UNAVAILABLE));

                    ServerReject(client);
                    return;
                }

                roomManager.ValidateRoomAccess(client.connectionId, message.token, (isSuccess, error) =>
                {
                    try
                    {
                        if (!isSuccess)
                        {
                            logger.Warn("Room access validation rejected the client");
                            client.Send(CreateErrorResult(
                                ResponseStatus.Unauthorized,
                                MstErrorCodes.ROOM_ACCESS_DENIED));

                            MstTimer.WaitForSeconds(1f, () => client.Disconnect());
                            return;
                        }

                        client.Send(new ValidateRoomAccessResultMessage()
                        {
                            status = ResponseStatus.Success
                        });
                    }
                    // If we got another exception
                    catch (Exception e)
                    {
                        logger.Error(e);
                        client.Send(CreateErrorResult(
                            ResponseStatus.Error,
                            MstErrorCodes.INTERNAL_ERROR));

                        MstTimer.WaitForSeconds(1f, () => client.Disconnect());
                    }
                });
            }, 10);
        }

        private static ValidateRoomAccessResultMessage CreateErrorResult(
            ResponseStatus status,
            string code)
        {
            var properties = new MstProperties();
            properties.Set(MstErrorPropertyKeys.CODE, code);

            return new ValidateRoomAccessResultMessage
            {
                error = properties.ToBytes(),
                status = status
            };
        }

        #endregion

        #region CLIENT

        public override void OnClientAuthenticate()
        {
            //// Only authorized users can have access data to the room.
            //// Such users usually come from the main menu
            //if (Mst.Client.Rooms.HasAccess)
            //{
            //    NetworkClient.Send(new ValidateRoomAccessRequestMessage()
            //    {
            //        token = Mst.Client.Rooms.ReceivedAccess.Token
            //    });
            //}
            //// If the user does not have access data to the room,
            //// then most likely he did not receive it earlier or started the game in host mode.
            //else
            //{
            //    if (networkManager.mode == NetworkManagerMode.Host)
            //    {
            //        MstTimer.WaitUntil(() => roomManager.IsActive, (TimerActionCompleteHandler)((success) =>
            //        {
            //            this.SignIn();
            //        }), validateTimeOut);
            //    }
            //    else if (networkManager.mode == NetworkManagerMode.ClientOnly)
            //    {
            //        Mst.Connection.AddConnectionOpenListener(OnConnectedToMaster);
            //    }
            //}
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<ValidateRoomAccessResultMessage>(OnValidateRoomAccessResultMessage, false);
            NetworkClient.RegisterHandler<ServerShutDownMessage>(OnServerShutDownMessage, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<ValidateRoomAccessResultMessage>();
            NetworkClient.UnregisterHandler<ServerShutDownMessage>();
            Mst.Client.Rooms.Destroy();
        }

        private void OnServerShutDownMessage(ServerShutDownMessage message)
        {
            logger.Error("The room server has just been stopped");
            ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage("The room server has just been stopped", null));
        }

        private void OnValidateRoomAccessResultMessage(ValidateRoomAccessResultMessage message)
        {
            ViewsManager.Hide<LoadingInfoView>();

            if (message.status == ResponseStatus.Success)
            {
                ClientAccept();
            }
            else
            {
                string error = Mst.Errors.Parse(message.status, message.error);

                ClientReject();
                ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(error, null));
                logger.Error(error);
            }
        }

        #endregion
    }
}
