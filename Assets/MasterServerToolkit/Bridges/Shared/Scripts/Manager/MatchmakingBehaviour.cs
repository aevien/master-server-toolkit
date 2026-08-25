using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using MasterServerToolkit.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.Bridges
{
    public class MatchmakingBehaviour : BaseClientBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Time to wait before match creation process will be aborted
        /// </summary>
        [SerializeField, Tooltip("Maximum time in seconds to wait for room creation before the client reports a failure. 0 disables the client-side timeout.")]
        protected uint matchCreationTimeout = 20;
        [SerializeField, Tooltip("Additional key/value room properties sent with every room spawn request alongside the request-specific spawn options.")]
        protected SerializedKeyValuePair[] customSpawnOptions;

        [Tooltip("Invoked on the client after a room starts and access data is received successfully.")]
        public UnityEvent OnRoomStartedEvent;
        [Tooltip("Invoked on the client when room creation, startup, or access acquisition fails.")]
        public UnityEvent OnRoomStartFailedEvent;

        #endregion

        private static MatchmakingBehaviour _instance;
        private bool roomStartingProcessCompleted = false;

        /// <summary>
        /// Properties that will be synced from room to all users
        /// </summary>
        public MstProperties CustomRoomProperties { get; private set; }

        public static MatchmakingBehaviour Instance
        {
            get
            {
                if (!_instance) Logs.Error("Instance of MatchmakingBehaviour is not found");
                return _instance;
            }
        }

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetEditorPlayModeState()
        {
            _instance = null;
        }
#endif

        protected override void Awake()
        {
            if (_instance)
            {
                Destroy(_instance.gameObject);
                return;
            }

            _instance = this;

            base.Awake();

            CustomRoomProperties = new MstProperties(customSpawnOptions);
        }

        protected override void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
                _instance = null;

            base.OnDestroy();
        }

        protected override void OnInitialize()
        {
            // Set cliet mode
            Mst.Client.Rooms.IsClient = true;
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
                    Logger.Error(error);
                    ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(error, null));
                }
            });
        }

        /// <summary>
        /// Sends request to master server to start new room process
        /// </summary>
        /// <param name="spawnOptions"></param>
        public virtual void CreateNewRoom(string regionName, MstProperties spawnOptions, UnityAction failCallback = null)
        {
            ViewsManager.Show<LoadingInfoView>("Starting room... Please wait!");

            Logger.Debug("Starting room... Please wait!");
            roomStartingProcessCompleted = false;

            Mst.Client.Spawners.RequestSpawn(spawnOptions, CustomRoomProperties, regionName, (controller, error) =>
            {
                if (controller == null)
                {
                    ViewsManager.Hide<LoadingInfoView>();
                    ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage(error, () =>
                    {
                        failCallback?.Invoke();
                    }));

                    return;
                }

                controller.OnStatusChangedEvent += Controller_OnStatusChangedEvent;

                // Wait for spawning status until it is finished
                // This status must be send by room
                MstTimer.WaitWhile(() =>
                {
                    return !roomStartingProcessCompleted;
                }, (isSuccess) =>
                {
                    controller.OnStatusChangedEvent -= Controller_OnStatusChangedEvent;

                    ViewsManager.Hide<LoadingInfoView>();

                    if (isSuccess)
                    {
                        if (controller.Status == SpawnStatus.Finalized)
                        {
                            OnRoomStarted();
                            OnRoomStartedEvent?.Invoke();

                            Logger.Info("You have successfully spawned new room");
                        }
                        else
                        {
                            OnRoomStartFailed();
                            OnRoomStartFailedEvent?.Invoke();

                            Logger.Error($"Failed spawn new room. Status: {controller.Status}");

                            ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage("Failed spawn new room. Please, try later", () =>
                            {
                                failCallback?.Invoke();
                            }));
                        }
                    }
                    else
                    {
                        Mst.Client.Spawners.AbortSpawn(controller.SpawnTaskId);

                        OnRoomStartFailed();
                        OnRoomStartFailedEvent?.Invoke();

                        Logger.Error("Failed spawn new room. Time out");

                        ViewsManager.Show<OkDialogBoxView>(new OkDialogBoxEventMessage("Failed spawn new room. Time out", () =>
                        {
                            failCallback?.Invoke();
                        }));
                    }

                }, matchCreationTimeout);
            });
        }

        private void Controller_OnStatusChangedEvent(SpawnStatus status)
        {
            ViewsManager.Show<LoadingInfoView>($"Starting room... Status: {status}");

            switch (status)
            {
                case SpawnStatus.Finalized:
                case SpawnStatus.Killed:
                case SpawnStatus.Aborted:
                    roomStartingProcessCompleted = true;
                    break;
            }
        }

        protected virtual void OnRoomStarted() { }

        protected virtual void OnRoomStartFailed() { }

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
            Mst.Options.Set(MstParamKeys.ROOM_ID, gameInfo.Id);
            // Save max players to buffer, may be very helpful
            Mst.Options.Set(Mst.Args.Names.RoomMaxConnections, gameInfo.MaxPlayers);

            if (gameInfo.IsPasswordProtected)
            {
                ViewsManager.Show<PasswordInputDialogBoxView>(new PasswordInputDialoxBoxEventMessage("Room requires the password. Please enter room password below", () =>
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
