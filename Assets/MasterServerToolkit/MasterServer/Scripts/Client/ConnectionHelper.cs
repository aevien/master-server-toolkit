using MasterServerToolkit.Extensions;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Helper base-class that manages a client socket lifecycle (connect / retry / disconnect) to a Master Server.
    /// Derive from this class to plug in your own connection factory or custom behavior.
    /// </summary>
    /// <typeparam name="T">Concrete MonoBehaviour type (used by SingletonBehaviour)</typeparam>
    [DisallowMultipleComponent]
    public abstract class ConnectionHelper<T> : SingletonBehaviour<T> where T : MonoBehaviour
    {
        #region INSPECTOR

        [SerializeField]
        /// <summary>
        /// Help box shown in the Inspector with a short description of this component.
        /// </summary>
        protected HelpBox header = new HelpBox()
        {
            Text = "This component connects a client to a Master Server. It handles retries, timeouts and events.",
            Type = HelpBoxType.Info
        };

        [Header("Connection Settings")]
        [SerializeField, Tooltip("Master server IP address or host name used for every connection attempt. Examples: 127.0.0.1, localhost or master.example.com.")]
        /// <summary>
        /// Target server IP address or hostname.
        /// </summary>
        protected string serverIp = "127.0.0.1";

        [SerializeField, Tooltip("Master server WebSocket TCP port. Valid values are 0 to 65535 and the value must match the listening server.")]
        /// <summary>
        /// Target server port.
        /// </summary>
        protected int serverPort = 5000;

        [Header("Security Settings")]
        [SerializeField, Tooltip("Uses a secure WebSocket connection (WSS). The -mstUseSecure argument overrides this value; the server endpoint and certificate must support TLS when enabled.")]
        /// <summary>
        /// Whether to use a secure connection.
        /// </summary>
        protected bool useSecure = false;

        [SerializeField, Tooltip("WebSocket service/path identifier requested from the server. It must not be empty and must match the server Service value, for example 'mst'.")]
        /// <summary>
        /// Service to which the client connects.
        /// </summary>
        protected string service = "mst";

        [SerializeField, Tooltip("Exact permission key whose connection-scoped credential is supplied by this helper. Use 'default' for the initial client-to-master handshake. This connection-scoped value has priority over runtime -mstPermissionCredentials for the matching key.")]
        protected string permissionKey = MstPermissionKeys.Default;

        [SerializeField, Tooltip("Credential for Permission Key. It is embedded in the built client and is not a protected secret store. A non-empty value overrides runtime -mstPermissionCredentials for this connection and matching key; an empty value falls back to runtime configuration or the built-in credential.")]
        protected string permissionCredential = MstPermissionSecrets.Default;

        [Header("Auto Connect")]
        [SerializeField, Tooltip("Starts the first connection cycle automatically after this component starts. The -mstStartClientConnection argument overrides this value.")]
        /// <summary>
        /// Connect automatically on Start().
        /// </summary>
        protected bool connectOnStart = true;

        [SerializeField, Tooltip("Delay in realtime seconds before the automatic connection cycle starts. 0 connects immediately; valid Inspector range is 0 to 60 seconds.")]
        [Min(0f)]
        /// <summary>
        /// Delay before automatic connection.
        /// </summary>
        protected float waitAndConnect = 0.2f;

        [Header("Advanced")]
        [SerializeField, Tooltip("Maximum duration in realtime seconds allowed for each connection attempt. Valid Inspector range is 2 to 60 seconds.")]
        [Range(2f, 60f)]
        /// <summary>
        /// Timeout per connection attempt, in seconds.
        /// </summary>
        protected float timeout = 5f;

        [SerializeField, Tooltip("Number of attempts in one connection cycle before On Failed Connect is invoked. Minimum value is 1; failed-cycle reconnect can start another cycle.")]
        [Min(1)]
        /// <summary>
        /// Maximum retry attempts.
        /// </summary>
        protected int maxAttemptsToConnect = 5;

        [Header("Reconnect")]
        [SerializeField, Tooltip("Starts a new connection cycle after an established connection closes unexpectedly. Manual closure suppresses this reconnect path.")]
        protected bool reconnectOnDisconnect = true;

        [SerializeField, Tooltip("Starts another full connection cycle after every attempt in the current cycle fails. Disable to stop after On Failed Connect is invoked.")]
        protected bool reconnectOnFailedConnect = true;

        [SerializeField, Tooltip("Delay in realtime seconds between attempts in one connection cycle and before reconnecting after a lost connection. 0 retries immediately.")]
        [Min(0f)]
        protected float reconnectDelay = 2f;

        [SerializeField, Tooltip("Delay in realtime seconds before a new cycle starts after all attempts fail. Used only when Reconnect On Failed Connect is enabled; 0 restarts immediately.")]
        [Min(0f)]
        protected float failedReconnectDelay = 5f;

        [Header("Events")]
        [Tooltip("Invoked once when a connection to the server is established.")]
        /// <summary>
        /// Invoked on successful connection.
        /// </summary>
        public UnityEvent OnConnectedEvent;

        [Tooltip("Invoked when all connection attempts fail.")]
        /// <summary>
        /// Invoked after the last failed attempt to connect.
        /// </summary>
        public UnityEvent OnFailedConnectEvent;

        [Tooltip("Invoked when the socket disconnects from the server (manually or due to a network issue).")]
        /// <summary>
        /// Invoked on disconnection.
        /// </summary>
        public UnityEvent OnDisconnectedEvent;

        #endregion

        /// <summary>
        /// Current attempt index (1..maxAttemptsToConnect).
        /// </summary>
        protected int currentAttemptToConnect = 0;

        /// <summary>
        /// Time when the connection sequence started (used to compute total connect time).
        /// </summary>
        private float startConnectionTime = 0f;

        /// <summary>
        /// True while a delayed retry/reconnect callback is pending.
        /// </summary>
        private bool reconnectScheduled = false;

        /// <summary>
        /// Incremented to invalidate old delayed reconnect callbacks.
        /// </summary>
        private int reconnectScheduleVersion = 0;

        /// <summary>
        /// Last endpoint used by the connection cycle. Delayed reconnect uses these values.
        /// </summary>
        private string activeServerIp = string.Empty;

        private int activeServerPort = 0;

        private int activeConnectionAttempts = 0;

        /// <summary>
        /// Prevents inspector event self-calls from starting a connection inside connection lifecycle callbacks.
        /// </summary>
        private bool isInvokingConnectionEvent = false;

        /// <summary>
        /// Active client socket instance used to communicate with the server.
        /// </summary>
        public IClientSocket Connection { get; protected set; }

        /// <summary>
        /// Returns true if there is a live connection to the server.
        /// </summary>
        public bool IsConnected => Connection != null && Connection.IsConnected;

        /// <summary>
        /// 
        /// </summary>
        public bool IsConnecting => Connection != null && Connection.IsConnecting;

        /// <summary>
        /// Unity lifecycle: ensures a single instance and prepares the connection according to CLI args and inspector.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // If current object is destroying just make a return
            if (isNowDestroying) return;

            // Create a connection instance if not provided
            Connection ??= ConnectionFactory();
            //Connection.LogLevel = logLevel;

            // Read secure mode from CLI args (overrides Inspector)
            useSecure = Mst.Args.AsBool(Mst.Args.Names.UseSecure, useSecure);

            // Apply base connection settings
            Connection.Service = service;
            Connection.UseSecure = useSecure;
            ApplyPermissionCredential();

            // Ensure object survives scene reloads by staying at root
            transform.SetParent(null, false);

            // Allow CLI arg to force auto-start
            connectOnStart = Mst.Args.AsBool(Mst.Args.Names.StartClientConnection, connectOnStart);
        }

        /// <summary>
        /// Unity lifecycle: optionally starts the connection flow with an optional delay.
        /// </summary>
        protected virtual void Start()
        {
            if (!connectOnStart) return;

            if (waitAndConnect <= 0f)
            {
                StartConnection();
            }
            else
            {
                MstTimer.WaitForSeconds(waitAndConnect, StartConnection);
            }
        }

        /// <summary>
        /// Unity editor callback: validates inspector values after edit-time changes.
        /// </summary>
        protected virtual void OnValidate()
        {
            permissionKey = string.IsNullOrWhiteSpace(permissionKey)
                ? MstPermissionKeys.Default
                : permissionKey.Trim();
            permissionCredential ??= string.Empty;
            maxAttemptsToConnect = Mathf.Clamp(maxAttemptsToConnect, 1, int.MaxValue);
            waitAndConnect = Mathf.Clamp(waitAndConnect, 0f, 60f);
            timeout = Mathf.Clamp(timeout, 2f, 60f);
            reconnectDelay = Mathf.Clamp(reconnectDelay, 0f, 3600f);
            failedReconnectDelay = Mathf.Clamp(failedReconnectDelay, 0f, 3600f);
        }

        /// <summary>
        /// Unity lifecycle: clean up listeners and close the socket on destroy.
        /// </summary>
        protected override void OnDestroy()
        {
            isNowDestroying = true;
            CancelScheduledReconnect();

            if (Connection != null)
            {
                CloseConnectionInternal(false);
            }

            base.OnDestroy();
        }

        /// <summary>
        /// Factory method for creating an <see cref="IClientSocket"/> instance.
        /// Override in derived classes to return a custom socket implementation.
        /// </summary>
        protected virtual IClientSocket ConnectionFactory()
        {
            return Mst.Connection;
        }

        private void ApplyPermissionCredential()
        {
            var credentialTarget = Connection as IConnectionPermissionCredentials;

            if (credentialTarget == null)
            {
                if (!string.IsNullOrEmpty(permissionCredential))
                {
                    logger.Warn(
                        $"{Connection?.GetType().Name ?? "Client socket"} does not support connection-scoped permission credentials");
                }

                return;
            }

            credentialTarget.PermissionKey = string.IsNullOrWhiteSpace(permissionKey)
                ? MstPermissionKeys.Default
                : permissionKey.Trim();
            credentialTarget.PermissionCredential = permissionCredential ?? string.Empty;
        }

        /// <summary>
        /// Updates the target server IP or hostname at runtime.
        /// </summary>
        /// <param name="address">IP or hostname</param>
        public void SetIpAddress(string address)
        {
            serverIp = address;
        }

        /// <summary>
        /// Updates the target server port at runtime.
        /// </summary>
        /// <param name="port">Server port</param>
        public void SetPort(int port)
        {
            serverPort = port;
        }

        /// <summary>
        /// Starts the connection sequence using the configured retry count.
        /// </summary>
        public void StartConnection()
        {
            StartConnection(maxAttemptsToConnect);
        }

        /// <summary>
        /// Starts the connection sequence with a specific number of attempts.
        /// </summary>
        /// <param name="numberOfAttempts">How many times to try before failing</param>
        public void StartConnection(int numberOfAttempts)
        {
            StartConnection(serverIp, serverPort, numberOfAttempts);
        }

        /// <summary>
        /// Starts the connection sequence toward a specific endpoint with optional attempt count.
        /// </summary>
        /// <param name="serverIp">IP/Hostname</param>
        /// <param name="serverPort">Port</param>
        /// <param name="numberOfAttempts">Attempts before failing (defaults to 5)</param>
        public void StartConnection(string serverIp, int serverPort, int numberOfAttempts = 5)
        {
            if (isNowDestroying)
            {
                return;
            }

            if (isInvokingConnectionEvent)
            {
                logger.Debug($"{GetType().Name.FromCamelcase()} start request ignored because it was invoked from a connection event");
                return;
            }

            if (reconnectScheduled)
            {
                logger.Debug($"{GetType().Name.FromCamelcase()} start request ignored because reconnect is already scheduled");
                return;
            }

            if (Connection == null)
            {
                logger.Error("Your connection is not defined");
                return;
            }

            if (IsConnected || IsConnecting)
            {
                logger.Debug($"{GetType().Name.FromCamelcase()} start request ignored because connection is already active");
                return;
            }

            startConnectionTime = Time.realtimeSinceStartup;
            currentAttemptToConnect = 0;
            activeServerIp = serverIp;
            activeServerPort = serverPort;
            activeConnectionAttempts = numberOfAttempts > 0 ? numberOfAttempts : maxAttemptsToConnect;
            maxAttemptsToConnect = activeConnectionAttempts;

            Connection.OnConnectionOpenEvent -= OnConnectedEventHandler;
            Connection.OnConnectionOpenEvent += OnConnectedEventHandler;

            StartConnectionProcess(activeServerIp, activeServerPort, activeConnectionAttempts);
        }

        /// <summary>
        /// 
        /// </summary>
        public void CloseConnection()
        {
            CloseConnection(true);
        }

        /// <summary>
        /// Closes current socket connection and optionally fires disconnect listeners.
        /// </summary>
        public void CloseConnection(bool fireEvent)
        {
            CancelScheduledReconnect();
            CloseConnectionInternal(fireEvent);
        }

        private void CloseConnectionInternal(bool fireEvent)
        {
            if (Connection == null)
            {
                return;
            }

            Connection.OnConnectionOpenEvent -= OnConnectedEventHandler;

            if (!fireEvent)
            {
                Connection.OnConnectionCloseEvent -= OnDisconnectedEventHandler;
            }

            Connection.Close(fireEvent);

            if (fireEvent)
            {
                Connection.OnConnectionCloseEvent -= OnDisconnectedEventHandler;
            }
        }

        /// <summary>
        /// Internal step that performs a single connection attempt and schedules retries if needed.
        /// </summary>
        /// <param name="serverIp">IP/Hostname</param>
        /// <param name="serverPort">Port</param>
        /// <param name="numberOfAttempts">Remaining attempts counter (informational)</param>
        protected virtual void StartConnectionProcess(string serverIp, int serverPort, int numberOfAttempts)
        {
            if (isNowDestroying)
            {
                return;
            }

            if (IsConnected || IsConnecting) return;

            currentAttemptToConnect++;

            if (!IsConnected && !IsConnecting)
            {
                logger.Info($"Starting {GetType().Name.FromCamelcase()}...");
                logger.Info($"{GetType().Name.FromCamelcase()} is connecting to server at: {serverIp}:{serverPort}");
            }
            else if (!IsConnected && IsConnecting)
            {
                logger.Info($"{GetType().Name.FromCamelcase()} is retrying to connect to server at: {serverIp}:{serverPort}. Attempt: {currentAttemptToConnect}");
            }

            Connection.OnConnectionOpenEvent -= OnConnectedEventHandler;
            Connection.OnConnectionOpenEvent += OnConnectedEventHandler;

            Connection.Connect(serverIp, serverPort, timeout);

            // Await the result of this attempt and decide: succeed, retry, or fail.
            Connection.WaitForConnection((client) =>
            {
                if (isNowDestroying)
                {
                    return;
                }

                if (!client.IsConnected)
                {
                    CloseConnectionInternal(false);

                    if (currentAttemptToConnect >= maxAttemptsToConnect)
                    {
                        logger.Info($"{GetType().Name.FromCamelcase()} cannot connect to server at: {serverIp}:{serverPort}");
                        ScheduleFailedReconnectCycle();
                        InvokeConnectionEvent(OnFailedConnectEvent);
                    }
                    else
                    {
                        ScheduleNextConnectionAttempt(serverIp, serverPort, numberOfAttempts);
                    }
                }
            });
        }

        /// <summary>
        /// Handler invoked when the connection opens.
        /// </summary>
        /// <param name="client">Client socket</param>
        protected virtual void OnConnectedEventHandler(IClientSocket client)
        {
            if (isNowDestroying)
            {
                return;
            }

            float totalConnectionTime = Time.realtimeSinceStartup - startConnectionTime;
            CancelScheduledReconnect();
            logger.Info($"{GetType().Name.FromCamelcase()} connected to server at: {activeServerIp}:{activeServerPort}. Connection time is: {totalConnectionTime}s.");
            InvokeConnectionEvent(OnConnectedEvent);
            client.OnConnectionCloseEvent -= OnDisconnectedEventHandler;
            client.OnConnectionCloseEvent += OnDisconnectedEventHandler;
        }

        /// <summary>
        /// Handler invoked when the connection closes.
        /// </summary>
        /// <param name="client">Client socket</param>
        protected virtual void OnDisconnectedEventHandler(IClientSocket client)
        {
            client.OnConnectionCloseEvent -= OnDisconnectedEventHandler;

            if (isNowDestroying)
            {
                return;
            }

            logger.Info($"{GetType().Name.FromCamelcase()} disconnected from server");
            ScheduleDisconnectReconnectCycle();
            InvokeConnectionEvent(OnDisconnectedEvent);
        }

        private void ScheduleNextConnectionAttempt(string serverIp, int serverPort, int numberOfAttempts)
        {
            ScheduleReconnect(reconnectDelay, "retry", () =>
            {
                StartConnectionProcess(serverIp, serverPort, numberOfAttempts);
            });
        }

        private void ScheduleDisconnectReconnectCycle()
        {
            if (!reconnectOnDisconnect)
            {
                return;
            }

            ScheduleReconnect(reconnectDelay, "disconnect", () =>
            {
                StartConnection(activeServerIp, activeServerPort, activeConnectionAttempts);
            });
        }

        private void ScheduleFailedReconnectCycle()
        {
            if (!reconnectOnFailedConnect)
            {
                return;
            }

            ScheduleReconnect(failedReconnectDelay, "failed connect", () =>
            {
                StartConnection(activeServerIp, activeServerPort, activeConnectionAttempts);
            });
        }

        private void ScheduleReconnect(float delay, string reason, System.Action callback)
        {
            if (isNowDestroying || IsConnected || reconnectScheduled)
            {
                return;
            }

            reconnectScheduled = true;
            int scheduleVersion = ++reconnectScheduleVersion;

            logger.Info($"{GetType().Name.FromCamelcase()} scheduled reconnect in {delay:0.##}s. Reason: {reason}");

            MstTimer.WaitForRealtimeSeconds(delay, () =>
            {
                if (scheduleVersion != reconnectScheduleVersion)
                {
                    return;
                }

                reconnectScheduled = false;

                if (isNowDestroying || IsConnected || IsConnecting)
                {
                    return;
                }

                callback?.Invoke();
            });
        }

        private void CancelScheduledReconnect()
        {
            reconnectScheduled = false;
            reconnectScheduleVersion++;
        }

        private void InvokeConnectionEvent(UnityEvent connectionEvent)
        {
            isInvokingConnectionEvent = true;

            try
            {
                connectionEvent?.Invoke();
            }
            finally
            {
                isInvokingConnectionEvent = false;
            }
        }
    }
}
