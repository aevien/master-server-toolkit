using MasterServerToolkit.Extensions;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.UI;
using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace MasterServerToolkit.Bridges
{
    public class CreateNewRoomView : UIView
    {
        [Header("Components"), SerializeField]
        [Tooltip("Input field used as the new room title. The view generates a friendly default title when initialized.")]
        private TMP_InputField roomNameInputField;
        [SerializeField]
        [Tooltip("Input field whose text is sent as the room maximum-connections argument. Configure numeric input validation in the TMP_InputField.")]
        private TMP_InputField roomMaxConnectionsInputField;
        [SerializeField]
        [Tooltip("Required dropdown populated with regions returned by the matchmaker when the view opens. Its selected region is used for room creation.")]
        private TMP_Dropdown roomRegionNameInputDropdown;
        [SerializeField]
        [Tooltip("Optional password input. An empty value creates a room without the room-password spawn option.")]
        private TMP_InputField roomPasswordInputField;

        private IDisposable showCreateNewRoomListener;
        private IDisposable hideCreateNewRoomListener;

        protected override void Awake()
        {
            base.Awake();

            RoomName = $"Room-{Mst.Helper.CreateFriendlyId()}";

            // Listen to show/hide events
            showCreateNewRoomListener?.Dispose();
            showCreateNewRoomListener = Mst.Events.AddListener(MstEventKeys.showCreateNewRoomView, OnShowCreateNewRoomEventHandler);

            hideCreateNewRoomListener?.Dispose();
            hideCreateNewRoomListener = Mst.Events.AddListener(MstEventKeys.hideCreateNewRoomView, OnHideCreateNewRoomEventHandler);
        }

        protected override void OnDestroy()
        {
            showCreateNewRoomListener?.Dispose();
            showCreateNewRoomListener = null;

            hideCreateNewRoomListener?.Dispose();
            hideCreateNewRoomListener = null;

            base.OnDestroy();
        }

        private void OnShowCreateNewRoomEventHandler(EventPayload message)
        {
            Show();
        }

        private void OnHideCreateNewRoomEventHandler(EventPayload message)
        {
            Hide();
        }

        protected override void OnEndShow()
        {
            base.OnEndShow();

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
            ViewsManager.Show<LoadingInfoView>("Starting room... Please wait!");

            Logs.Debug("Starting room... Please wait!");

            // Spawn options for spawner controller
            var spawnOptions = new MstProperties();
            spawnOptions.Add(Mst.Args.Names.RoomMaxConnections, MaxConnections);
            spawnOptions.Add(Mst.Args.Names.RoomTitle, RoomName.Escape());

            if (!string.IsNullOrEmpty(Password))
                spawnOptions.Add(Mst.Args.Names.RoomPassword, Password);

            MatchmakingBehaviour.Instance.CreateNewRoom(RegionName, spawnOptions, () =>
            {
                Show();
            });
        }
    }
}
