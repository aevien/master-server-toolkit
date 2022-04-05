using MasterServerToolkit.Games;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.MasterServer
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

        protected IClientSocket roomConnection;

        protected override void StartConnection(RoomAccessPacket access)
        {
            AccessData = access;
            roomConnection = Mst.Create.ClientSocket();
            roomConnection.AddConnectionOpenListener(OnConnectedToRoomEventHandler);
            roomConnection.AddConnectionCloseListener(OnDisconnectedFromRoomEventHandler, false);
            roomConnection.Connect(access.RoomIp, access.RoomPort, roomConnectionTimeout);

            roomConnection.WaitForConnection((socket) =>
            {
                if(socket == null)
                {
                    roomConnection.Close();
                    logger.Error($"Connection timeout has expired");
                }
            });
        }

        /// <summary>
        /// Disconnects client from room server
        /// </summary>
        protected override void StartDisconnection()
        {
            // Stop client
            roomConnection.Close();
        }

        private void OnConnectedToRoomEventHandler()
        {
            roomConnection.SendMessage((ushort)MstOpCodes.ValidateRoomAccessRequest, AccessData.Token, (status, response) =>
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
            ScenesLoader.LoadSceneByName(AccessData.SceneName, (progressValue) =>
            {
                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, $"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% ... Please wait!");
            }, null);
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
