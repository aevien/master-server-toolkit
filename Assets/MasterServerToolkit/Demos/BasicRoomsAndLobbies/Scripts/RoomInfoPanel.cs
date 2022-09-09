using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MasterServerToolkit.Examples.BasicRoomsAndLobbies
{
    public class RoomInfoPanel : MonoBehaviour
    {
        #region INSPECTOR

        [Header("Components"), SerializeField]
        private TMP_Text roomIdText;
        [SerializeField]
        private TMP_Text roomSceneNameText;
        [SerializeField]
        private TMP_Text roomMaxPlayersText;

        #endregion

        private void Start()
        {
            var roomManager = FindObjectOfType<RoomServerManager>();

            if (roomManager)
            {
                MstTimer.WaitWhile(() => !Mst.Client.Rooms.HasAccess, (isSuccess) =>
                {
                    if (isSuccess)
                    {
                        var access = Mst.Client.Rooms.ReceivedAccess;

                        roomIdText.text = $"ID: {access.RoomId}";
                        roomSceneNameText.text = $"Scene: {access.SceneName}";
                        roomMaxPlayersText.text = $"Max Players: {access.RoomMaxConnections}";
                    }
                }, 10f);

                if (!roomManager.IsActive)
                    roomManager.OnRoomRegisteredEvent.AddListener(OnRoomRegisteredEventHandler);
                else
                    OnRoomRegisteredEventHandler(roomManager.RoomController);
            }
        }

        private void OnRoomRegisteredEventHandler(RoomController roomController)
        {
            string onlineScene = Mst.Args.AsString(Mst.Args.Names.RoomOnlineScene, SceneManager.GetActiveScene().name);

            roomIdText.text = $"ID: {roomController.RoomId}";
            roomSceneNameText.text = $"Scene: {onlineScene}";
            roomMaxPlayersText.text = $"Max Players: {roomController.Options.MaxConnections}";
        }
    }
}