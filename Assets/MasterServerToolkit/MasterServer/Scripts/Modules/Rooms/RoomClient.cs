using MasterServerToolkit.Bridges;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class RoomClient<T> : SingletonBehaviour<T> where T : MonoBehaviour
    {
        #region INSPECTOR

        /// <summary>
        /// Time of waiting the connection to mirror server
        /// </summary>
        [Header("Base Settings"), SerializeField, Tooltip("Time of waiting the connection to room server in seconds")]
        protected float roomConnectionTimeout = 10;

        [Header("Editor Settings"), SerializeField]
        private HelpBox editorHelp = new HelpBox()
        {
            Text = "This settings works only in editor. They are for test purpose only",
            Type = HelpBoxType.Info
        };

        /// <summary>
        /// This will start client in editor automatically
        /// </summary>
        [SerializeField, Tooltip("This will start client in editor automatically")]
        protected bool autoStartInEditor = true;

        /// <summary>
        /// This time will be taken to wait and start client in editor
        /// </summary>
        [SerializeField, Tooltip("This time will be taken to wait and start client in editor")]
        protected float autoStartDelay = 1f;

        /// <summary>
        /// If true system will try to sign in as guest in test mode
        /// </summary>
        [SerializeField, Tooltip("If true system will try to sign in as guest in test mode")]
        protected bool signInAsGuest = true;

        /// <summary>
        /// If <see cref="signInAsGuest"/> is not true system will try sign in as registereg user in test mode using this username
        /// </summary>
        [SerializeField, Tooltip("If signInAsGuest is not true system will try sign in as registereg user in test mode using this username")]
        protected string username = "qwerty";

        /// <summary>
        /// If <see cref="signInAsGuest"/> is not true system will try sign in as registereg user in test mode using this password
        /// </summary>
        [SerializeField, Tooltip("If signInAsGuest is not true system will try sign in as registereg user in test mode using this password")]
        protected string password = "qwerty12345";

        #endregion

        protected bool isChangingZone = false;

        protected override void Awake()
        {
            base.Awake();

            if (isNowDestroying) return;

            Mst.Events.AddListener(MstEventKeys.leaveRoom, (message) =>
            {
                Disconnect();
            });

            Mst.Events.AddListener(MstEventKeys.goToZone, (message) =>
            {
                isChangingZone = message.AsBool();
            });

            // Register access listener
            Mst.Client.Rooms.OnAccessReceivedEvent += OnAccessReceivedEvent;
        }

        protected virtual void Start()
        {
            if (Mst.Runtime.IsEditor && autoStartInEditor)
            {
                MstTimer.WaitForSeconds(autoStartDelay, () =>
                {
                    AutostartInEditor();
                });
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Register access listener
            Mst.Client.Rooms.OnAccessReceivedEvent -= OnAccessReceivedEvent;
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void AutostartInEditor()
        {
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Starting room in editor...");

            MstTimer.WaitForSeconds(1f, () =>
            {
                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Signing in...");

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
        protected virtual void SignIn()
        {
            Mst.Client.Auth.SignInWithLoginAndPassword(username, password, (account, signInError) =>
            {
                if (account == null)
                {
                    logger.Error(signInError);
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(signInError, null));
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
                    return;
                }

                StartGame();
            });
        }

        /// <summary>
        /// 
        /// </summary>
        protected virtual void SignInAsGuest()
        {
            Mst.Client.Auth.SignInAsGuest((account, signInError) =>
            {
                if (account == null)
                {
                    logger.Error(signInError);
                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(signInError, null));
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
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
            Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Looking for available games...");

            Mst.Client.Matchmaker.FindGames((games) =>
            {
                if (games.Count == 0)
                {
                    logger.Error("No games found");

                    Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage("No games found", null));
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
                    return;
                }

                Mst.Events.Invoke(MstEventKeys.showLoadingInfo, "Getting access...");

                Mst.Client.Rooms.GetAccess(games.First().Id, (access, getAccessError) =>
                {
                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);

                    if (!string.IsNullOrEmpty(getAccessError))
                    {
                        logger.Error(getAccessError);
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(getAccessError, null));
                        Disconnect();
                    }
                });
            });
        }

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
        protected abstract void StartConnection(RoomAccessPacket access);

        /// <summary>
        /// Closes coneection to server
        /// </summary>
        protected abstract void StartDisconnection();

        /// <summary>
        /// Starts connection process
        /// </summary>
        /// <param name="access"></param>
        public static void Connect(RoomAccessPacket access)
        {
            if (Instance == null)
            {
                Logs.Error("Failed to connect to game server. No Game Connector was found in the scene");
                return;
            }

            var client = Instance as RoomClient<T>;

            // Start connection
            if (client)
                client.StartConnection(access);
        }

        /// <summary>
        /// Start disconnection process
        /// </summary>
        public static void Disconnect()
        {
            if (Instance == null)
            {
                Logs.Error("Failed to disconnect from game server. No Game Connector was found in the scene");
                return;
            }

            var client = Instance as RoomClient<T>;

            // Start disconnection
            if (client)
                client.StartDisconnection();
        }
    }
}