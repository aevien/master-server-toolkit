using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Games
{
    public class MatchmakingBehaviour : BaseClientBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Time to wait before match creation process will be aborted
        /// </summary>
        [SerializeField, Tooltip("Time to wait before match creation process will be aborted")]
        protected uint matchCreationTimeout = 60;

        public UnityEvent OnRoomStartedEvent;
        public UnityEvent OnRoomStartAbortedEvent;

        #endregion

        private static MatchmakingBehaviour _instance;

        public static MatchmakingBehaviour Instance
        {
            get
            {
                if (!_instance) Logs.Error("Instance of MatchmakingBehaviour is not found");
                return _instance;
            }
        }

        protected override void Awake()
        {
            if (_instance)
            {
                Destroy(_instance.gameObject);
                return;
            }

            _instance = this;

            base.Awake();
        }

        protected override void OnInitialize()
        {
            // Set cliet mode
            Mst.Client.Rooms.IsClientMode = true;
        }

        /// <summary>
        /// Tries to get access to room
        /// </summary>
        /// <param name="gameInfo"></param>
        /// <param name="password"></param>
        protected virtual void GetAccess(GameInfoPacket gameInfo, string password = "")
        {
            Mst.Client.Rooms.GetAccess(gameInfo.Id, password, (access, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    logger.Error(error);
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error, null));
                }
            });
        }

        /// <summary>
        /// Sends request to master server to start new room process
        /// </summary>
        /// <param name="spawnOptions"></param>
        public virtual void CreateNewRoom(string regionName, MstProperties spawnOptions, UnityAction failCallback = null)
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Starting room... Please wait!");

            logger.Debug("Starting room... Please wait!");

            // Custom options that will be given to room directly
            var customSpawnOptions = new MstProperties();
            customSpawnOptions.Add(Mst.Args.Names.StartClientConnection, true);

            // Here is the example of using custom options. If your option name starts from "-room."
            // then this option will be added to custom room options on server automatically
            customSpawnOptions.Add("-room.CustomTextOption", "Here is room custom option");
            customSpawnOptions.Add("-room.CustomIdOption", Mst.Helper.CreateID_10());
            customSpawnOptions.Add("-room.CustomDateTimeOption", DateTime.Now.ToString());

            Mst.Client.Spawners.RequestSpawn(spawnOptions, customSpawnOptions, regionName, (controller, error) =>
            {
                if (controller == null)
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(error, () =>
                    {
                        failCallback?.Invoke();
                    }));

                    return;
                }

                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Room started. Finalizing... Please wait!");

                // Wait for spawning status until it is finished
                // This status must be send by room
                MstTimer.WaitWhile(() =>
                {
                    return controller.Status != SpawnStatus.Finalized;
                }, (isSuccess) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (!isSuccess)
                    {
                        Mst.Client.Spawners.AbortSpawn(controller.SpawnTaskId);

                        logger.Error("Failed spawn new room. Time is up!");
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("Failed spawn new room. Time is up!", () =>
                        {
                            failCallback?.Invoke();
                        }));

                        OnRoomStartAborted();
                        OnRoomStartAbortedEvent?.Invoke();

                        return;
                    }

                    OnRoomStarted();
                    OnRoomStartedEvent?.Invoke();

                    logger.Info("You have successfully spawned new room");

                }, matchCreationTimeout);
            });
        }

        protected virtual void OnRoomStarted() { }

        protected virtual void OnRoomStartAborted() { }

        /// <summary>
        /// Sends request to master server to start new room process
        /// </summary>
        /// <param name="spawnOptions"></param>
        public virtual void CreateNewRoom(MstProperties spawnOptions)
        {
            CreateNewRoom(string.Empty, spawnOptions);
        }

        /// <summary>
        /// Starts given match
        /// </summary>
        /// <param name="gameInfo"></param>
        public virtual void StartMatch(GameInfoPacket gameInfo)
        {
            // Save room Id in buffer, may be very helpful
            Mst.Options.Set(MstDictKeys.ROOM_ID, gameInfo.Id);
            // Save max players to buffer, may be very helpful
            Mst.Options.Set(Mst.Args.Names.RoomMaxConnections, gameInfo.MaxPlayers);

            if (gameInfo.IsPasswordProtected)
            {
                Mst.Events.Invoke(MstEventKeys.showPasswordDialogBox,
                    new PasswordInputDialoxBoxEventMessage("Room requires the password. Please enter room password below", () =>
                    {
                        // Get password if was set
                        string password = Mst.Options.AsString(Mst.Args.Names.RoomPassword);

                        // Get access with password
                        GetAccess(gameInfo, password);
                    }));
            }
            else
            {
                GetAccess(gameInfo);
            }
        }
    }
}