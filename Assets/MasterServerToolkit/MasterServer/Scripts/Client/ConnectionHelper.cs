using MasterServerToolkit.Extensions;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ConnectionHelper<T> : SingletonBehaviour<T> where T : MonoBehaviour
    {
        #region INSPECTOR

        [SerializeField]
        protected HelpBox header = new HelpBox()
        {
            Text = "This script connects client to server. Is is just a helper",
            Type = HelpBoxType.Info
        };

        [Header("Connection Settings"), Tooltip("Address to the server"), SerializeField]
        protected string serverIp = "127.0.0.1";

        [Tooltip("Port of the server"), SerializeField]
        protected int serverPort = 5000;

        [Header("Advanced"), SerializeField]
        protected float timeout = 5f;
        [SerializeField]
        protected int maxAttemptsToConnect = 5;
        [SerializeField]
        protected float waitAndConnect = 0.2f;
        [Tooltip("If true, will try to connect on the Start()"), SerializeField]
        protected bool connectOnStart = true;

        [Header("Events")]
        /// <summary>
        /// Triggers when connected to server
        /// </summary>
        public UnityEvent OnConnectedEvent;
        /// <summary>
        /// Triggers when failed connect to server
        /// </summary>
        public UnityEvent OnFailedConnectEvent;
        /// <summary>
        /// triggers when disconnected from server
        /// </summary>
        public UnityEvent OnDisconnectedEvent;

        #endregion

        protected int currentAttemptToConnect = 0;
        private bool isConnecting = false;

        /// <summary>
        /// Main connection to server
        /// </summary>
        public IClientSocket Connection { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsConnected => Connection != null && Connection.IsConnected;

        protected override void Awake()
        {
            base.Awake();

            // If current object is destroying just make a return
            if (isNowDestroying) return;

            // Set connection if it is null
            if (Connection == null) Connection = ConnectionFactory();

            // In case this object is not at the root level of hierarchy
            // move it there, so that it won't be destroyed
            if (transform.parent != null)
                transform.SetParent(null, false);

            // check if autostart connection os defined in cmd args
            connectOnStart = Mst.Args.AsBool(Mst.Args.Names.StartClientConnection, connectOnStart);
        }

        protected virtual void Start()
        {
            if (connectOnStart)
            {
                MstTimer.WaitForSeconds(waitAndConnect, () =>
                {
                    StartConnection();
                });
            }
        }

        protected virtual void OnValidate()
        {
            maxAttemptsToConnect = Mathf.Clamp(maxAttemptsToConnect, 1, int.MaxValue);
            waitAndConnect = Mathf.Clamp(waitAndConnect, 0.2f, 60f);
            timeout = Mathf.Clamp(timeout, 2f, 60f);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (Connection != null)
            {
                Connection.RemoveConnectionOpenListener(OnConnectedEventHandler);
                Connection.RemoveConnectionCloseListener(OnDisconnectedEventHandler);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual IClientSocket ConnectionFactory()
        {
            return Mst.Client.Connection;
        }

        /// <summary>
        /// Sets the server IP
        /// </summary>
        /// <param name="address"></param>
        public void SetIpAddress(string address)
        {
            serverIp = address;
        }

        /// <summary>
        /// Sets the server port
        /// </summary>
        /// <param name="masterPort"></param>
        public void SetPort(int port)
        {
            serverPort = port;
        }

        /// <summary>
        /// Starts connection to server
        /// </summary>
        public void StartConnection()
        {
            StartConnection(maxAttemptsToConnect);
        }

        public void StartConnection(int numberOfAttempts)
        {
            StartConnection(serverIp, serverPort, numberOfAttempts);
        }

        public void StartConnection(string serverIp, int serverPort, int numberOfAttempts = 5)
        {
            if (Connection != null)
            {
                currentAttemptToConnect = 0;
                maxAttemptsToConnect = numberOfAttempts > 0 ? numberOfAttempts : maxAttemptsToConnect;

                Connection.AddConnectionOpenListener(OnConnectedEventHandler);
                Connection.AddConnectionCloseListener(OnDisconnectedEventHandler, false);

                StartConnectionProcess(serverIp, serverPort, numberOfAttempts);
            }
        }

        protected virtual void StartConnectionProcess(string serverIp, int serverPort, int numberOfAttempts)
        {
            if (Connection.IsConnected || isConnecting) return;

            currentAttemptToConnect++;
            isConnecting = true;

            if (!Connection.IsConnected && !Connection.IsConnecting)
            {
                logger.Info($"Starting {GetType().Name.SplitByUppercase()}...".ToGreen());
                logger.Info($"{GetType().Name.SplitByUppercase()} is connecting to server at: {serverIp}:{serverPort}".ToGreen());
            }
            else if (!Connection.IsConnected && Connection.IsConnecting)
            {
                logger.Info($"{GetType().Name.SplitByUppercase()} is retrying to connect to server at: {serverIp}:{serverPort}. Attempt: {currentAttemptToConnect}".ToGreen());
            }

            Connection.UseSecure = Mst.Settings.UseSecure;
            Connection.Connect(serverIp, serverPort, timeout);

            // 
            Connection.WaitForConnection((client) =>
            {
                isConnecting = false;

                if (!client.IsConnected)
                {
                    if (currentAttemptToConnect == maxAttemptsToConnect)
                    {
                        logger.Info($"{GetType().Name.SplitByUppercase()} cannot to connect to server at: {serverIp}:{serverPort}".ToRed());
                        Connection.Close();
                        OnFailedConnectEvent?.Invoke();
                    }
                    else
                    {
                        StartConnectionProcess(serverIp, serverPort, numberOfAttempts);
                    }
                }
            });
        }

        protected virtual void OnDisconnectedEventHandler(IClientSocket client)
        {
            logger.Info($"{GetType().Name.SplitByUppercase()} disconnected from server".ToRed());
            OnDisconnectedEvent?.Invoke();
        }

        protected virtual void OnConnectedEventHandler(IClientSocket client)
        {
            logger.Info($"{GetType().Name.SplitByUppercase()} connected to server at: {serverIp}:{serverPort}".ToGreen());
            OnConnectedEvent?.Invoke();
        }
    }
}
