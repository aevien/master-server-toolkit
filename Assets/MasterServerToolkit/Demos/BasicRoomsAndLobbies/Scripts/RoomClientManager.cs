using MasterServerToolkit.Games;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class RoomClientManager : RoomClient<RoomClientManager>
    {
        #region INSPECTOR

        /// <summary>
        /// Name of the room that will be loaded after a match is successfully created
        /// </summary>
        [Header("Settings"), SerializeField, Tooltip("Name of the room that will be loaded after a match is successfully created")]
        protected string onlineRoomScene = "Room";
        [SerializeField]
        private string offlineRoomScene = "Client";

        #endregion

        protected IClientSocket roomConnection;
        protected RoomAccessPacket roomAccess;

        protected override void StartConnection(RoomAccessPacket access)
        {
            roomAccess = access; 
            roomConnection = Mst.Create.ClientSocket();
            roomConnection.AddConnectionListener(OnConnectedToRoomEventHandler);
            roomConnection.AddDisconnectionListener(OnDisconnectedFromRoomEventHandler);
            roomConnection.Connect(access.RoomIp, access.RoomPort);
        }

        private void OnConnectedToRoomEventHandler()
        {
            roomConnection.SendMessage((short)MstMessageCodes.ValidateRoomAccessRequest, roomAccess.Token, (status, response) =>
            {
                if (status == ResponseStatus.Success)
                {
                    logger.Info($"You have received access to room");
                    LoadOnlineScene();
                }
                else
                {
                    logger.Info(response.AsString("Error"));
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(response.AsString("Error"), null));
                }
            });
        }

        private void OnDisconnectedFromRoomEventHandler()
        {
            LoadOfflineScene();
        }

        /// <summary>
        /// Starts match scene
        /// </summary>
        protected virtual void LoadOnlineScene()
        {
            ScenesLoader.LoadSceneByName(onlineRoomScene, (progressValue) =>
            {
                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% ... Please wait!");
            }, null);
        }

        /// <summary>
        /// Stops match scene
        /// </summary>
        protected virtual void LoadOfflineScene()
        {
            ScenesLoader.LoadSceneByName(offlineRoomScene, (progressValue) =>
            {
                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% ... Please wait!");
            }, null);
        }

        /// <summary>
        /// Disconnects client from room server
        /// </summary>
        public virtual void Disconnect()
        {
            roomConnection?.Disconnect();
        }
    }
}
