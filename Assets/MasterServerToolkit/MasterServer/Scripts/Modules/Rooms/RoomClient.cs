using MasterServerToolkit.Games;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
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
        [Header("Base Settings"), SerializeField, Tooltip("Time of waiting the connection to mirror server")]
        protected int roomConnectionTimeout = 10;

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

        /// <summary>
        /// Latest access data. When switching scenes, if this is set,
        /// connector should most likely try to use this data to connect to game server
        /// (if the scene is right)
        /// </summary>
        protected static RoomAccessPacket AccessData;

        protected virtual void OnValidate()
        {
            roomConnectionTimeout = Mathf.Clamp(roomConnectionTimeout, 4, 60);
        }

        protected override void Awake()
        {
            base.Awake();

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

        protected virtual void OnDestroy()
        {
            Mst.Client.Rooms.OnAccessReceivedEvent -= OnAccessReceivedEvent;
        }

        /// <summary>
        /// Invoked when room access received
        /// </summary>
        /// <param name="access"></param>
        private void OnAccessReceivedEvent(RoomAccessPacket access)
        {
            StartConnection(access);
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
                    if (!string.IsNullOrEmpty(getAccessError))
                    {
                        logger.Error(getAccessError);
                        Mst.Events.Invoke(MstEventKeys.showOkDialogBox, new OkDialogBoxEventMessage(getAccessError, null));
                    }

                    Mst.Events.Invoke(MstEventKeys.hideLoadingInfo);
                });
            });
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
            if (Singleton == null)
            {
                Logs.Error("Failed to connect to game server. No Game Connector was found in the scene");
                return;
            }

            // Save the access data
            AccessData = access;

            // Start connection
            (Singleton as RoomClient<T>).StartConnection(access);
        }

        /// <summary>
        /// Start disconnection process
        /// </summary>
        public static void Disconnect()
        {
            (Singleton as RoomClient<T>).StartDisconnection();
        }
    }
}