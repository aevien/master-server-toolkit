using Aevien.UI;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Games
{
    public class CreateNewRoomView : UIView
    {
        [Header("Components"), SerializeField]
        private TMP_InputField roomNameInputField;
        [SerializeField]
        private TMP_InputField roomMaxConnectionsInputField;
        [SerializeField]
        private TMP_InputField roomRegionNameInputField;
        [SerializeField]
        private TMP_InputField roomPasswordInputField;

        protected override void Awake()
        {
            base.Awake();

            // Listen to show/hide events
            Mst.Events.AddEventListener(MstEventKeys.showCreateNewRoomView, OnShowCreateNewRoomEventHandler);
            Mst.Events.AddEventListener(MstEventKeys.hideCreateNewRoomView, OnHideCreateNewRoomEventHandler);
        }

        private void OnShowCreateNewRoomEventHandler(EventMessage message)
        {
            Show();
        }

        private void OnHideCreateNewRoomEventHandler(EventMessage message)
        {
            Hide();
        }

        public string RoomName
        {
            get
            {
                return roomNameInputField != null ? roomNameInputField.text : string.Empty;
            }

            set
            {
                if (roomNameInputField)
                    roomNameInputField.text = value;
            }
        }

        public string MaxConnections
        {
            get
            {
                return roomMaxConnectionsInputField != null ? roomMaxConnectionsInputField.text : string.Empty;
            }

            set
            {
                if (roomMaxConnectionsInputField)
                    roomMaxConnectionsInputField.text = value;
            }
        }

        public string RegionName
        {
            get
            {
                return roomRegionNameInputField != null ? roomRegionNameInputField.text : string.Empty;
            }

            set
            {
                if (roomRegionNameInputField)
                    roomRegionNameInputField.text = value;
            }
        }

        public string Password
        {
            get
            {
                return roomPasswordInputField != null ? roomPasswordInputField.text : string.Empty;
            }

            set
            {
                if (roomPasswordInputField)
                    roomPasswordInputField.text = value;
            }
        }

        public void CreateNewMatch()
        {
            Hide();

            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Starting room... Please wait!");

            Logs.Debug("Starting room... Please wait!");

            // Spawn options for spawner controller
            var spawnOptions = new MstProperties();
            spawnOptions.Add(MstDictKeys.ROOM_MAX_PLAYERS, MaxConnections);
            spawnOptions.Add(MstDictKeys.ROOM_NAME, RoomName);
            spawnOptions.Add(MstDictKeys.ROOM_PASSWORD, Password);

            MatchmakingBehaviour.Instance.CreateNewRoom(RegionName, spawnOptions);
        }
    }
}