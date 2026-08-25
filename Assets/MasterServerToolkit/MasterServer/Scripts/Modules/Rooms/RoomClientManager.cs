using MasterServerToolkit.Bridges;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using MasterServerToolkit.Utils;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class RoomClientManager : RoomClient
    {
        #region INSPECTOR

        /// <summary>
        /// Name of the room that will be loaded after a match is successfully created
        /// </summary>
        [Header("Settings"), SerializeField, Tooltip("The name of the room that will be loaded after the client leaves the room")]
        private string offlineRoomScene = "Client";

        #endregion

        protected IClientSocket roomConnection;

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (roomConnection != null)
            {
                roomConnection.RemoveConnectionOpenListener(OnConnectedToRoomEventHandler);
                roomConnection.RemoveConnectionCloseListener(OnDisconnectedFromRoomEventHandler);
            }
        }

        protected override void Connect(RoomAccessPacket access)
        {
            if (roomConnection != null)
                roomConnection.Close(false);
            else
                roomConnection = Mst.Connection;

            roomConnection.AddConnectionOpenListener(OnConnectedToRoomEventHandler);
            roomConnection.AddConnectionCloseListener(OnDisconnectedFromRoomEventHandler, false);

            roomConnection.Connect(access.Ip, access.Port, roomConnectionTimeout);

            roomConnection.WaitForConnection((socket) =>
            {
                if (!socket.IsConnected)
                {
                    roomConnection.Close();
                    logger.Error("Room connection failed or timed out");
                }
            });
        }

        /// <summary>
        /// Disconnects client from room server
        /// </summary>
        protected override void Disconnect()
        {
            // Stop client
            roomConnection.Close();
        }

        private void OnConnectedToRoomEventHandler(IClientSocket client)
        {
            roomConnection.SendMessage(MstOpCodes.ValidateRoomAccessRequest, Mst.Client.Rooms.ReceivedAccess.Token, (status, response) =>
            {
                if (status == ResponseStatus.Success)
                {
                    logger.Info($"You have received access to room");
                    LoadOnlineScene();
                }
                else
                {
                    string error = Mst.Errors.Parse(status, response);
                    logger.Info(error);
                    ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(error, null));
                }
            });
        }

        private void OnDisconnectedFromRoomEventHandler(IClientSocket client)
        {
            LoadOfflineScene();
        }

        /// <summary>
        /// Starts match scene
        /// </summary>
        protected virtual void LoadOnlineScene()
        {
            ScenesLoader.LoadSceneByName(Mst.Client.Rooms.ReceivedAccess.SceneName, (progressValue) =>
            {
                ViewsManager.Show<LoadingInfoView>($"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% ... Please wait!");
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
                    ViewsManager.Show<LoadingInfoView>($"Loading scene {Mathf.RoundToInt(progressValue * 100f)}% ... Please wait!");
                }, null);
            }
        }
    }
}
