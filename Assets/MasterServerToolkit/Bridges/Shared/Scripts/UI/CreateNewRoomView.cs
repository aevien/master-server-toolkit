using Aevien.UI;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using System.Collections.Generic;
using System.Linq;
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
        private TMP_Dropdown roomRegionNameInputDropdown;
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

        protected override void OnShow()
        {
            base.OnShow();

            Mst.Client.Matchmaker.GetRegions(regions =>
            {
                roomRegionNameInputDropdown.ClearOptions();
                roomRegionNameInputDropdown.interactable = regions.Count > 0;

                if (regions.Count > 0)
                {
                    roomRegionNameInputDropdown.AddOptions(regions.Select(i =>
                    {
                        return $"<b>{i.Name}</b>, <color=#FF0000FF>Ping: {i.PingTime} ms.</color>";
                    }).ToList());
                }
            });
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
                return roomRegionNameInputDropdown != null && roomRegionNameInputDropdown.options.Count > 0 ? Mst.Client.Matchmaker.Regions[roomRegionNameInputDropdown.value].Name : string.Empty;
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
            spawnOptions.Add(MstDictKeys.ROOM_MAX_CONNECTIONS, MaxConnections);
            spawnOptions.Add(MstDictKeys.ROOM_NAME, RoomName);
            spawnOptions.Add(MstDictKeys.ROOM_PASSWORD, Password);

            // You can send scene name to load that one in online mode
            spawnOptions.Add(MstDictKeys.ROOM_ONLINE_SCENE_NAME, "");

            MatchmakingBehaviour.Instance.CreateNewRoom(RegionName, spawnOptions);
        }
    }
}