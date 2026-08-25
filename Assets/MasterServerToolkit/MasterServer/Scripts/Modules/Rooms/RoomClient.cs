using MasterServerToolkit.Bridges;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.UI;
using MasterServerToolkit.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class RoomClient : MonoBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Time of waiting the connection to mirror server
        /// </summary>
        [Header("Base Settings"), SerializeField, Tooltip("Maximum duration in realtime seconds allowed for the room connection attempt. Use a positive value long enough for the selected transport and platform.")]
        protected float roomConnectionTimeout = 10;
        [SerializeField, Tooltip("Minimum severity written by this room client component.")]
        protected LogLevel logLevel = LogLevel.Info;

#if UNITY_EDITOR
        [Header("Editor Settings"), SerializeField]
        private HelpBox editorHelp = new HelpBox()
        {
            Text = "This settings works only in editor. They are for test purpose only",
            Type = HelpBoxType.Info
        };

        /// <summary>
        /// This will start client in editor automatically
        /// </summary>
        [SerializeField, Tooltip("Runs the Editor-only sign-in and room-client startup flow automatically. Ignored in standalone builds.")]
        protected bool autoStartInEditor = true;

        /// <summary>
        /// If true system will try to sign in as guest in test mode
        /// </summary>
        [SerializeField, Tooltip("Uses guest authentication during Editor auto-start. When disabled, Username and Password are used instead. Ignored outside the Editor.")]
        protected bool signInAsGuest = true;

        /// <summary>
        /// If <see cref="signInAsGuest"/> is not true system will try sign in as registereg user in test mode using this username
        /// </summary>
        [SerializeField, Tooltip("Account username used by Editor auto-start when Sign In As Guest is disabled. Ignored outside the Editor.")]
        protected string username = "qwerty";

        /// <summary>
        /// If <see cref="signInAsGuest"/> is not true system will try sign in as registereg user in test mode using this password
        /// </summary>
        [SerializeField, Tooltip("Account password used by Editor auto-start when Sign In As Guest is disabled. Ignored outside the Editor.")]
        protected string password = "qwerty12345";
#endif

        #endregion

        protected Logging.Logger logger;
        protected bool isChangingZone = false;

        private IDisposable leaveRoomListener;
        private IDisposable goToZoneListener;

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            leaveRoomListener?.Dispose();
            leaveRoomListener = Mst.Events.AddListener(MstEventKeys.leaveRoom, OnLeaveRoomEventHandler);

            goToZoneListener?.Dispose();
            goToZoneListener = Mst.Events.AddListener(MstEventKeys.goToZone, OnGoToZoneEventHandler);

            // Register access listener
            Mst.Client.Rooms.OnAccessReceivedEvent += OnAccessReceivedEvent;
        }

        protected virtual void Start()
        {
            if (Mst.Client.Rooms.HasAccess)
            {
                Connect(Mst.Client.Rooms.ReceivedAccess);
                return;
            }

#if UNITY_EDITOR
            if (autoStartInEditor)
            {
                AutostartInEditor();
            }
#endif
        }

        protected virtual void OnDestroy()
        {
            leaveRoomListener?.Dispose();
            leaveRoomListener = null;

            goToZoneListener?.Dispose();
            goToZoneListener = null;

            // Unregister access listener
            Mst.Client.Rooms.OnAccessReceivedEvent -= OnAccessReceivedEvent;
        }

        private void OnLeaveRoomEventHandler(EventPayload message)
        {
            Disconnect();
        }

        private void OnGoToZoneEventHandler(EventPayload message)
        {
            isChangingZone = message.AsBool();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 
        /// </summary>
        protected virtual void AutostartInEditor()
        {
            ViewsManager.Show<LoadingInfoView>("Starting room in editor...");

            MstTimer.WaitForSeconds(1f, () =>
            {
                ViewsManager.Show<LoadingInfoView>("Signing in...");

                if (signInAsGuest)
                {
                    SignInAsGuest();
                }
                else
                {
                    SignIn();
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void SignIn()
        {
            SignIn(username, password);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <param name="password"></param>
        public virtual void SignIn(string username, string password)
        {
            Mst.Client.Auth.SignInWithLoginAndPassword(username, password, (status, account, signInError) =>
            {
                if (status != ResponseStatus.Success || account == null)
                {
                    logger.Error(signInError);
                    ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(signInError, null));
                    ViewsManager.Hide<LoadingInfoView>();
                    return;
                }

                StartGame();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        public virtual void SignInAsGuest()
        {
            Mst.Client.Auth.SignInAsGuest((status, account, signInError) =>
            {
                if (status != ResponseStatus.Success || account == null)
                {
                    logger.Error(signInError);
                    ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(signInError, null));
                    ViewsManager.Hide<LoadingInfoView>();
                    return;
                }

                StartGame();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void StartGame()
        {
            ViewsManager.Show<LoadingInfoView>("Looking for available games...");

            Mst.Client.Matchmaker.FindGames((games) =>
            {
                if (!games.Any())
                {
                    logger.Error("No games found");

                    ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage("No games found", null));
                    ViewsManager.Hide<LoadingInfoView>();
                    return;
                }

                ViewsManager.Show<LoadingInfoView>("Getting access...");

                Mst.Client.Rooms.GetAccess(games[0].Id, (access, getAccessError) =>
                {
                    ViewsManager.Hide<LoadingInfoView>();

                    if (!string.IsNullOrEmpty(getAccessError))
                    {
                        logger.Error(getAccessError);
                        ViewsManager.Show<OkDialogBoxView>( new OkDialogBoxEventMessage(getAccessError, null));
                        Disconnect();
                    }
                });
            });
        }
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="access"></param>
        private void OnAccessReceivedEvent(RoomAccessPacket access)
        {
            Connect(access);
        }

        /// <summary>
        /// Starts connection process
        /// </summary>
        /// <param name="access"></param>
        protected abstract void Connect(RoomAccessPacket access);

        /// <summary>
        /// Closes coneection to server
        /// </summary>
        protected abstract void Disconnect();
    }
}
