using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class RoomClientEvent : UnityEvent<RoomClient> { }

    public class RoomClient : BaseClientBehaviour
    {
        #region INSPECTOR

        [Header("Settings"), SerializeField]
        protected bool doNotDestroyOnLoad = true;

        [Header("Master Connection Settings"), SerializeField]
        protected string masterIP = "127.0.0.1";
        [SerializeField]
        protected int masterPort = 5000;

        [Header("Room Connection Settings"), SerializeField]
        protected string roomServerIp = "127.0.0.1";
        [SerializeField]
        protected int roomServerPort = 7777;

        [Header("Editor Settings"), SerializeField]
        protected HelpBox roomClientInfoEditor = new HelpBox()
        {
            Text = "Editor settings are used only while running in editor",
            Type = HelpBoxType.Warning
        };

        [SerializeField]
        protected bool autoStartInEditor = true;

        [SerializeField]
        protected bool signInAsGuest = true;

        [SerializeField]
        protected string username = "qwerty";

        [SerializeField]
        protected string password = "qwerty12345";

        #endregion

        /// <summary>
        /// This socket connects room client to master
        /// </summary>
        protected IClientSocket masterConnection;

        /// <summary>
        /// Room access that client gets from master server
        /// </summary>
        private RoomAccessPacket roomServerAccessInfo;

        /// <summary>
        /// Fires when room server has given an access to us
        /// </summary>
        public UnityEvent OnAccessGrantedEvent;

        /// <summary>
        /// Fires when room server has rejected an access to us
        /// </summary>
        public UnityEvent OnAccessDiniedEvent;

        /// <summary>
        /// Fires when room client connected to room server
        /// </summary>
        public RoomClientEvent OnConnectedToServerEvent;

        /// <summary>
        /// Fires when room client disconnected from room server
        /// </summary>
        public RoomClientEvent OnDisconnectedFromServerEvent;

        protected override void Awake()
        {
            if (doNotDestroyOnLoad)
            {
                // Find another instance of this behaviour
                var clientInstance = FindObjectOfType<RoomClient>();

                if (clientInstance != this)
                {
                    Destroy(gameObject);
                    return;
                }

                DontDestroyOnLoad(gameObject);
            }

            base.Awake();

            // Get connection to master
            masterConnection = Mst.Connection;

            // Create room client connection
            Connection = ConnectionFactory();

            // Listen to connection statuses
            Connection.AddConnectionListener(OnClientConnectedToRoomServer, false);
            Connection.AddDisconnectionListener(OnClientDisconnectedFromRoomServer, false);

            // If master IP is provided via cmd arguments
            masterIP = Mst.Args.AsString(Mst.Args.Names.MasterIp, masterIP);
            // If master port is provided via cmd arguments
            masterPort = Mst.Args.AsInt(Mst.Args.Names.MasterPort, masterPort);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            // Disconnect from room server
            Connection?.Disconnect();
        }

        /// <summary>
        /// Check if room client behaviour is in test mode
        /// </summary>
        /// <returns></returns>
        public virtual bool IsInTestMode()
        {
            return Mst.Runtime.IsEditor && autoStartInEditor && !Mst.Options.Has(MstDictKeys.AUTOSTART_ROOM_CLIENT);
        }

        /// <summary>
        /// Clears connection and all its handlers if <paramref name="clearHandlers"/> is true
        /// </summary>
        public override void ClearConnection(bool clearHandlers = true)
        {
            base.ClearConnection(clearHandlers);

            // If we are in test mode we need to be disconnected
            if (IsInTestMode() && Connection != null)
            {
                Connection.Disconnect();
            }
        }

        /// <summary>
        /// Creates new connection to room server
        /// </summary>
        /// <returns></returns>
        protected override IClientSocket ConnectionFactory()
        {
            return Mst.Create.ClientSocket();
        }

        protected override void OnInitialize()
        {
            // Listen to disconnection from master
            masterConnection.AddDisconnectionListener(OnDisconnectedFromMasterEvent, false);

            MstTimer.WaitForSeconds(1f, () =>
            {
                if (IsInTestMode())
                {
                    StartRoomClient(true);
                }

                if (Mst.Options.Has(MstDictKeys.AUTOSTART_ROOM_CLIENT) || Mst.Args.StartClientConnection)
                {
                    StartRoomClient();
                }
            });
        }

        /// <summary>
        /// Start room client
        /// </summary>
        /// <param name="ignoreForceClientMode"></param>
        public void StartRoomClient(bool ignoreForceClientMode = false)
        {
            if (!Mst.Client.Rooms.ForceClientMode && !ignoreForceClientMode) return;

            logger.Info($"Starting Room Client... {Mst.Version}");
            logger.Info($"Start parameters are: {Mst.Args}");

            // Start connecting room server to master server
            ConnectToMaster();
        }

        /// <summary>
        /// Starting connection to master server as client to be able to register room later after successful connection
        /// </summary>
        private void ConnectToMaster()
        {
            // Start client connection
            if (!masterConnection.IsConnected)
            {
                masterConnection.UseSsl = MstApplicationConfig.Instance.UseSecure || Mst.Args.UseSecure;
                masterConnection.Connect(masterIP, masterPort);
            }

            // Wait a result of client connection
            masterConnection.WaitForConnection((clientSocket) =>
            {
                if (!clientSocket.IsConnected)
                {
                    logger.Error("Failed to connect room client to master server");
                }
                else
                {
                    logger.Info($"Successfully connected to master {masterConnection.ConnectionIp}:{masterConnection.ConnectionPort}");

                    // For the test purpose only
                    if (IsInTestMode())
                    {
                        if (signInAsGuest)
                        {
                            // Sign in client as guest
                            Mst.Client.Auth.SignInAsGuest(SignInCallback);
                        }
                        else
                        {
                            // Sign in client using credentials
                            Mst.Client.Auth.SignInWithLoginAndPassword(username, password, SignInCallback);
                        }
                    }
                    else
                    {
                        // If we have option with room id
                        // this approach can be used when you have come to this scene from another one.
                        // Set this option before this room client controller is connected to master server
                        if (Mst.Options.Has(MstDictKeys.ROOM_ID))
                        {
                            // Let's try to get access data for room we want to connect to
                            GetRoomAccess(Mst.Options.AsInt(MstDictKeys.ROOM_ID));
                        }
                        else
                        {
                            logger.Error($"You have no room id in this options: {Mst.Options}");
                        }
                    }
                }
            }, 5f);
        }

        /// <summary>
        /// Test sign in callback
        /// </summary>
        /// <param name="accountInfo"></param>
        /// <param name="error"></param>
        private void SignInCallback(ClientAccountInfo accountInfo, string error)
        {
            if (accountInfo == null)
            {
                logger.Error(error);
                return;
            }

            logger.Debug($"Signed in successfully as {accountInfo.Username}");
            logger.Debug("Finding games...");

            Mst.Client.Matchmaker.FindGames((games) =>
            {
                if (games.Count == 0)
                {
                    logger.Error("No test game found");
                    return;
                }

                logger.Debug($"Found {games.Count} games");

                // Get first game fromlist
                GameInfoPacket firstGame = games.First();

                // Let's try to get access data for room we want to connect to
                GetRoomAccess(firstGame.Id);
            });
        }

        /// <summary>
        /// Tries to get access data for room we want to connect to
        /// </summary>
        /// <param name="roomId"></param>
        private void GetRoomAccess(int roomId)
        {
            logger.Debug($"Getting access to room {roomId}");

            Mst.Client.Rooms.GetAccess(roomId, (access, error) =>
            {
                if (access == null)
                {
                    logger.Error(error);
                    OnAccessDiniedEvent?.Invoke();
                    return;
                }

                // Save gotten room access
                roomServerAccessInfo = access;

                // Let's set the IP before we start connection
                roomServerIp = roomServerAccessInfo.RoomIp;

                // Let's set the port before we start connection
                roomServerPort = roomServerAccessInfo.RoomPort;

                logger.Debug($"Access to room {roomId} received");
                logger.Debug(access);
                logger.Debug("Connecting to room server...");

                // Start client connection
                //roomServerConnection.UseSsl = MstApplicationConfig.Instance.UseSecure || Mst.Args.UseSecure;
                //roomServerConnection.Connect(roomServerIp, roomServerPort);

                //// Wait a result of client connection
                //roomServerConnection.WaitForConnection((clientSocket) =>
                //{
                //    if (!clientSocket.IsConnected)
                //    {
                //        logger.Error("Connection attempts to room server timed out");
                //        return;
                //    }
                //}, 4f);
            });
        }

        /// <summary>
        /// Fires when client connected to room server
        /// </summary>
        protected virtual void OnClientConnectedToRoomServer()
        {
            logger.Info("We have successfully connected to the room server");

            OnConnectedToServerEvent?.Invoke(this);

            //roomServerConnection.RemoveConnectionListener(OnClientConnectedToRoomServer);
            //roomServerConnection.SendMessage((short)MstMessageCodes.ValidateRoomAccessRequest, roomServerAccessInfo.Token, (status, response) =>
            //{
            //    // If access denied
            //    if (status != ResponseStatus.Success)
            //    {
            //        logger.Error(response.AsString());
            //        OnAccessDiniedEvent?.Invoke();
            //        return;
            //    }

            //    // If access granted
            //    OnAccessGrantedEvent?.Invoke();
            //});
        }

        /// <summary>
        /// Fires when client disconnected from room server
        /// </summary>
        protected virtual void OnClientDisconnectedFromRoomServer()
        {
            OnDisconnectedFromServerEvent?.Invoke(this);

            //roomServerConnection.RemoveDisconnectionListener(OnClientDisconnectedFromRoomServer);
            logger.Error("We have lost the connection to room server");
        }


        protected virtual void OnDisconnectedFromMasterEvent()
        {
            Connection?.RemoveDisconnectionListener(OnDisconnectedFromMasterEvent);
            //roomServerConnection?.Disconnect();
        }
    }
}