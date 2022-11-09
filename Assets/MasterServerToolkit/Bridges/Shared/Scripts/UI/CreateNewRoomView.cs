using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
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

            RoomName = $"Room#{Mst.Helper.CreateFriendlyId()}";

            // Listen to show/hide events
            Mst.Events.AddListener(MstEventKeys.showCreateNewRoomView, OnShowCreateNewRoomEventHandler);
            Mst.Events.AddListener(MstEventKeys.hideCreateNewRoomView, OnHideCreateNewRoomEventHandler);
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
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Starting room... Please wait!");

            Logs.Debug("Starting room... Please wait!");

            Regex roomNameRe = new Regex(@"\s+");

            // Spawn options for spawner controller
            var spawnOptions = new MstProperties();
            spawnOptions.Add(Mst.Args.Names.RoomMaxConnections, MaxConnections);
            spawnOptions.Add(Mst.Args.Names.RoomName, roomNameRe.Replace(RoomName, "_"));

            if (!string.IsNullOrEmpty(Password))
                spawnOptions.Add(Mst.Args.Names.RoomPassword, Password);

            // TODO
            // You can send scene name to load that one in online mode
            //spawnOptions.Add(Mst.Args.Names.RoomOnlineScene);

            MatchmakingBehaviour.Instance.CreateNewRoom(RegionName, spawnOptions, () =>
            {
                Show();
            });
        }
    }
}