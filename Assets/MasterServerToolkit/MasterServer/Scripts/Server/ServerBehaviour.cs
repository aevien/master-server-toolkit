using MasterServerToolkit.Extensions;
using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ServerBehaviour : MonoBehaviour, IServer
    {
        #region INSPECTOR

        [SerializeField]
        private HelpBox hpInfo = new()
        {
            Text = "This component is responsible for starting a Server and initializing its modules",
            Type = HelpBoxType.Info
        };

        [Header("Server Settings")]
        [SerializeField, Tooltip("Discovers and initializes BaseServerModule components when the server component initializes. Disable this only when every module is registered from code.")]
        private bool lookForModules = true;

        [SerializeField, Tooltip("Limits automatic module discovery to this GameObject hierarchy. When disabled, discovery searches the loaded scene. Used only when Look For Modules is enabled.")]
        private bool lookInChildrenOnly = true;

        [SerializeField, Tooltip("Permission definitions accepted by this server. Each entry provides a public key, numeric level from 0 to 999, and its handshake secret. Built-in default, admin, room_server and spawner entries are restored if removed.")]
        private List<PermissionEntry> permissions;

        [SerializeField, Range(10, 120), Tooltip("Target server update rate in frames per second. The command-line target-frame-rate setting overrides this value. Valid Inspector range: 10 to 120.")]
        private int targetFrameRate = 30;

        [SerializeField, Tooltip("Minimum severity written by this server component. Module log levels are configured separately.")]
        protected LogLevel logLevel = LogLevel.Info;

        [SerializeField, Tooltip("IP address or host name used by the listening socket. Command-line master/server address arguments may override it before startup.")]
        protected string serverIp = "localhost";

        [SerializeField, Tooltip("TCP port used by the listening socket. Valid runtime range is 0 to 65535; command-line port arguments may override it.")]
        protected int serverPort = 5000;

        [SerializeField, Tooltip("WebSocket service/path identifier required by connecting clients. It must not be empty and must match the client's endpoint.")]
        protected string service = "mst";

        [SerializeField, Tooltip("Maximum simultaneous authenticated and validating connections. 0 removes the MST connection-count limit.")]
        protected ushort maxConnections = 0;

        [SerializeField, Tooltip("Seconds an authenticated peer may remain without activity before disconnection. Values at or below 0 make every inactive check immediately eligible; use a positive value in production. Overridable by the client-inactivity command-line argument.")]
        protected float inactivityTimeout = 5f;

        [SerializeField, Tooltip("Seconds allowed for a newly connected peer to complete connection validation. Values at or below 0 cause immediate expiry. Overridable by the client-validation command-line argument.")]
        protected float validationTimeout = 5f;

        [Header("Security Settings")]
        [SerializeField, Tooltip("Requires secure WebSocket connections. The -mstUseSecure argument overrides this value. A readable certificate and compatible protocol are required when enabled.")]
        protected bool useSecure = false;

        [SerializeField, Tooltip("TLS protocol accepted by the secure listening socket. Ignored when secure connections are disabled.")]
        protected SslProtocols sslProtocols = SslProtocols.Tls12;

        [SerializeField, Tooltip("Path to the PFX certificate used by the secure listening socket. Relative paths are resolved by the transport; the certificate-path command-line argument overrides this value. Ignored when secure connections are disabled.")]
        protected string certificatePath = "";

        [SerializeField, Tooltip("Password used to open the configured PFX certificate. The certificate-password command-line argument overrides this value. Ignored when secure connections are disabled.")]
        protected string certificatePassword = "";

        #endregion

        /// <summary>
        ///
        /// </summary>
        protected int CountInactivePeers()
        {
            int count = 0;

            foreach (IPeer peer in connectedPeers.Values)
            {
                if (!peer.IsConnected)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Gets the total number of clients connected to the server during the entire session
        /// </summary>
        protected int totalPeersCount = 0;

        /// <summary>
        /// Gets the highest number of clients connected to the server during the entire session
        /// </summary>
        protected int highestPeersCount = 0;

        /// <summary>
        ///
        /// </summary>
        protected int rejectedPeersCount = 0;
        private volatile bool isStopping;
        private readonly object lifecycleSync = new object();
        private readonly object socketLifecycleSync = new object();
        private ServerRunContext activeRun;
        private long nextRunGeneration;
        private SynchronizationContext ownerThreadContext;
        private int ownerThreadId = -1;

        /// <summary>
        /// 
        /// </summary>
        protected DateTime startTime = DateTime.MinValue;

        /// <summary>
        /// Server socket
        /// </summary>
        private IServerSocket socket;

        /// <summary>
        /// Server messages handlers list
        /// </summary>
        private readonly ConcurrentDictionary<ushort, IAsyncPacketHandler> handlers = new ConcurrentDictionary<ushort, IAsyncPacketHandler>();

        /// <summary>
        /// Server modules
        /// </summary>
        private readonly Dictionary<Type, IBaseServerModule> modules = new Dictionary<Type, IBaseServerModule>();

        /// <summary>
        /// Initialized server modules list
        /// </summary>
        private readonly HashSet<Type> initializedModules = new HashSet<Type>();
        private readonly List<Type> initializedModuleOrder = new List<Type>();

        /// <summary>
        /// Modules that threw during initialization. Failed modules are not retried within the same server lifecycle.
        /// </summary>
        private readonly HashSet<Type> failedModules = new HashSet<Type>();

        /// <summary>
        /// List of connected clients to server
        /// </summary>
        private readonly ConcurrentDictionary<int, IPeer> connectedPeers = new ConcurrentDictionary<int, IPeer>();

        /// <summary>
        /// 
        /// </summary>
        private readonly ConcurrentDictionary<int, IPeer> unauthenticatedPeers = new ConcurrentDictionary<int, IPeer>();

        private readonly Dictionary<string, PermissionEntry> permissionEntriesByKey = new(StringComparer.Ordinal);
        private bool isConfigurationValid = true;

        private const int AccessRejectionDisconnectDelayMs = 350;

        private enum ServerRunState
        {
            Starting,
            Running,
            Stopping,
            Stopped
        }

        private sealed class ServerRunContext : IDisposable
        {
            public ServerRunContext(long generation, int startupThreadId)
            {
                Generation = generation;
                StartupThreadId = startupThreadId;
            }

            public long Generation { get; }
            public int StartupThreadId { get; }
            public CancellationTokenSource Cancellation { get; } = new CancellationTokenSource();
            public Dictionary<long, Task> ActiveHandlers { get; } = new Dictionary<long, Task>();
            public List<IServerRunModule> StartedModules { get; } = new List<IServerRunModule>();
            public TaskCompletionSource<bool> CancellationCompleted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> StartupCompleted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> HandlersDrained { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> ModulesStopped { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public Exception ModuleStopFailure { get; set; }
            public TaskCompletionSource<bool> TransportClosed { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> StopCallbacksPublished { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> StopCompleted { get; } =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public long NextHandlerId { get; set; }
            public bool IsAcceptingMessages { get; set; }
            public bool IsStopRequested { get; set; }
            public bool IsCancellationStartRequested { get; set; }
            public bool IsTransportCloseScheduled { get; set; }
            public bool IsTransportCloseStarted { get; set; }
            public bool AreModulesStopStarted { get; set; }
            public bool AreStopCallbacksStarted { get; set; }
            public bool IsCompletionWatcherStarted { get; set; }
            public bool IsCompletionPublished { get; set; }
            public ServerRunState State { get; set; } = ServerRunState.Starting;

            public void Dispose()
            {
                Cancellation.Dispose();
            }
        }

        /// <summary>
        /// All conected to server peers
        /// </summary>
        public IEnumerable<IPeer> Peers => connectedPeers.Values;

        /// <summary>
        /// Current server behaviour <see cref="Logger"/>
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// Check if current server is running
        /// </summary>
        public bool IsRunning { get; protected set; } = false;

        /// <summary>
        /// Gets peers count currently connected to server
        /// </summary>
        public int PeersCount => connectedPeers.Count;

        /// <summary>
        /// Server local IP address
        /// </summary>
        public string Address => serverIp;

        /// <summary>
        /// Server port
        /// </summary>
        public int Port => serverPort;

        /// <summary>
        /// Fires when any client connected to server
        /// </summary>
        public event PeerActionHandler OnPeerConnectedEvent;

        /// <summary>
        /// Fires when any client disconnected from server
        /// </summary>
        public event PeerActionHandler OnPeerDisconnectedEvent;

        /// <summary>
        /// Fires when server started
        /// </summary>
        public event Action OnServerStartedEvent;

        /// <summary>
        /// Fires when server stopped
        /// </summary>
        public event Action OnServerStoppedEvent;

        protected virtual void Awake()
        {
            ownerThreadContext = SynchronizationContext.Current;
            ownerThreadId = Thread.CurrentThread.ManagedThreadId;

            NormalizePermissions();

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            startTime = DateTime.UtcNow;
        }

        protected virtual void Start()
        {
            NormalizePermissions();

            if (string.IsNullOrEmpty(service))
            {
                logger.Error("ApplicationKey is not defined");
                return;
            }

            if (!Mst.Runtime.IsEditor)
                Application.targetFrameRate = Mst.Args.AsInt(Mst.Args.Names.TargetFrameRate, targetFrameRate);

            // Set timeout
            inactivityTimeout = Mst.Args.AsFloat(Mst.Args.Names.ClientInactivityTimeout, inactivityTimeout);
            validationTimeout = Mst.Args.AsFloat(Mst.Args.Names.ClientValidationTimeout, validationTimeout);

            if (!LoadPermissionConfiguration())
            {
                isConfigurationValid = false;
                logger.Error("Master server permission configuration is invalid. Server startup aborted");
                return;
            }

            string keyRingPath = ResolveSecurityKeyRingPath(Mst.Args.SecurityKeyRingFile);

            if (!Mst.Security.InitializeServer(keyRingPath, out string securityError))
            {
                isConfigurationValid = false;
                logger.Error($"Master security initialization failed: {securityError}");
                return;
            }

            isConfigurationValid = true;

            // Create the server 
            socket = Mst.Create.ServerSocket();
            socket.LogLevel = logLevel;

            // Setup secure connection in Start method
            socket.UseSecure = Mst.Args.AsBool(Mst.Args.Names.UseSecure, useSecure);
            socket.CertificatePath = Mst.Args.AsString(Mst.Args.Names.CertificatePath, certificatePath);
            socket.CertificatePassword = Mst.Args.AsString(Mst.Args.Names.CertificatePassword, certificatePassword);
            socket.Service = service;
            socket.SslProtocols = sslProtocols;

            if (!Mst.Runtime.IsEditor && !socket.UseSecure)
            {
                logger.Warn(
                    "MST socket transport is running without TLS. Use WSS in production to authenticate " +
                    "the server and protect permission and sign-in handshakes from interception");
            }

            socket.OnPeerConnectedEvent += OnPeerConnectedEventHandle;
            socket.OnPeerDisconnectedEvent += OnPeerDisconnectedEventHandler;

            RegisterMessageHandler(MstOpCodes.ServerAccessChallengeRequest, ServerAccessChallengeRequestHandler);
            RegisterMessageHandler(MstOpCodes.SealChallengeRequest, SealChallengeRequestHandler);
            RegisterMessageHandler(MstOpCodes.PeerGuidRequest, PeerGuidRequestHandler);
            RegisterMessageHandler(MstOpCodes.ServerAccessRequest, ServerAccessRequestHandler);
        }

        protected virtual void OnValidate()
        {
            maxConnections = (ushort)Mathf.Clamp(maxConnections, 0, ushort.MaxValue);
            NormalizePermissions();
        }

        protected virtual void OnDestroy()
        {
            StopServer();
        }

        protected virtual void OnApplicationQuit()
        {
            StopServer();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual MstJson Info()
        {
            var info = MstJson.CreateObject();

            try
            {
                info.AddField("server", MstJson.CreateObject());

                var uptime = DateTime.UtcNow - startTime;

                info["server"].AddField("startTime", startTime);
                info["server"].AddField("upTime", $"{uptime.TotalDays:F0}d {uptime.Hours:F0}h {uptime.Minutes:F0}m {uptime.Seconds:F0}s");
                info["server"].AddField("initializedModules", GetInitializedModules().Count);
                info["server"].AddField("unitializedModules", GetUninitializedModules().Count);
                info["server"].AddField("useSecure", useSecure);
                info["server"].AddField("certificatePath", certificatePath);
                info["server"].AddField("certificatePassword", "************");
                info["server"].AddField("service", service);
                info["server"].AddField("localIp", Address);
                info["server"].AddField("publicIp", Address);
                info["server"].AddField("port", Port);

                info.AddField("clients", MstJson.CreateObject());
                info["clients"].AddField("activeClients", PeersCount);
                info["clients"].AddField("inactiveClients", CountInactivePeers());
                info["clients"].AddField("unauthenticatedPeers", unauthenticatedPeers.Count);
                info["clients"].AddField("totalClients", totalPeersCount);
                info["clients"].AddField("highestClients", highestPeersCount);
                info["clients"].AddField("peersAccepted", totalPeersCount);
                info["clients"].AddField("peersRejected", rejectedPeersCount);

                var modulesArray = MstJson.CreateArray();

                foreach (var module in modules.Values)
                {
                    modulesArray.Add(module.Info());
                }

                info.AddField("modules", modulesArray);
                info.AddField("traffic", Mst.Traffic.Info());
            }
            catch (Exception e)
            {
                info.AddField("error", e.ToString());
            }

            return info;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="isOn"></param>
        /// <param name="inChildrenOnly"></param>
        public void SetLookForModules(bool isOn, bool inChildrenOnly = true)
        {
            lookForModules = isOn;
            lookInChildrenOnly = inChildrenOnly;
        }

        /// <summary>
        /// Sets the server IP
        /// </summary>
        /// <param name="listenToIp"></param>
        public void SetIpAddress(string listenToIp)
        {
            serverIp = listenToIp;
        }

        /// <summary>
        /// Sets the server port
        /// </summary>
        /// <param name="listenToPort"></param>
        public void SetPort(int listenToPort)
        {
            serverPort = listenToPort;
        }

        /// <summary>
        /// Starts server
        /// </summary>
        public virtual void StartServer()
        {
            StartServer(serverIp, serverPort);
        }

        /// <summary>
        /// Starts server with given port
        /// </summary>
        /// <param name="listenToPort"></param>
        public virtual void StartServer(int listenToPort)
        {
            StartServer(serverIp, listenToPort);
        }

        /// <summary>
        /// Starts server with given port and ip
        /// </summary>
        /// <param name="listenToIp">IP который слшаем</param>
        /// <param name="listenToPort"></param>
        public virtual void StartServer(string listenToIp, int listenToPort)
        {
            if (IsRunning) return;

            if (!isConfigurationValid || socket == null)
            {
                logger.Error($"Cannot start {GetType().Name.FromCamelcase()} because its configuration is invalid");
                return;
            }

            if (!TryBeginServerRunLifecycle(out long runGeneration))
            {
                logger.Warn($"Cannot start {GetType().Name.FromCamelcase()} while the previous server run is still stopping");
                return;
            }

            serverIp = listenToIp;
            serverPort = listenToPort;

            MstProperties startInfo = new();
            startInfo.Add("\t-FPS is", Application.targetFrameRate);
            startInfo.Add("\t-App key", socket.Service);
            startInfo.Add("\t-Secure", socket.UseSecure);
            startInfo.Add("\t-Certificate Path", !socket.UseSecure ? "Undefined" : socket.CertificatePath);
            startInfo.Add("\t-Certificate Pass", string.IsNullOrEmpty(socket.CertificatePath) || !socket.UseSecure ? "Undefined" : "********");

            logger.Info($"Starting {GetType().Name.FromCamelcase()}...\n{startInfo.ToReadableString("\n", ": ")}");

            try
            {
                ValidateServerStartupConfiguration();

                lock (socketLifecycleSync)
                {
                    if (!CanContinueServerStartup(runGeneration))
                        return;

                    socket.OnPeerConnectedEvent -= OnPeerConnectedEventHandle;
                    socket.OnPeerDisconnectedEvent -= OnPeerDisconnectedEventHandler;
                    socket.OnPeerConnectedEvent += OnPeerConnectedEventHandle;
                    socket.OnPeerDisconnectedEvent += OnPeerDisconnectedEventHandler;

                    socket.Listen(listenToIp, listenToPort);
                }

                if (!CanContinueServerStartup(runGeneration))
                    return;

                LookForModules();

                if (!TryStartServerRunModules(runGeneration))
                    return;

                if (!TryMarkServerRunStarted(runGeneration))
                    return;

                if (!TryRunServerStartupAction(runGeneration, OnStartedServer))
                    return;

                if (!TryInvokeServerStartedEvent(runGeneration))
                    return;

                TrySubscribeServerTimer(runGeneration);
            }
            catch
            {
                ServerRunContext failedRun = BeginServerStop(out bool ownsStop);

                if (failedRun != null)
                {
                    if (ownsStop)
                        RequestServerRunCancellation(failedRun);

                    EnsureStopCompletionWatcher(failedRun);
                }

                throw;
            }
            finally
            {
                CompleteServerStartup(runGeneration);
            }
        }

        private void ValidateServerStartupConfiguration()
        {
            var validators = new List<IServerStartupValidator>();

            foreach (IServerStartupValidator validator in modules.Values.OfType<IServerStartupValidator>())
                validators.Add(validator);

            if (lookForModules)
            {
                BaseServerModule[] discoveredModules = lookInChildrenOnly
                    ? GetComponentsInChildren<BaseServerModule>()
                    : FindObjectsOfType<BaseServerModule>();

                foreach (IServerStartupValidator validator in discoveredModules.OfType<IServerStartupValidator>())
                {
                    if (!validators.Any(existing => ReferenceEquals(existing, validator)))
                        validators.Add(validator);
                }
            }

            foreach (IServerStartupValidator validator in validators)
                validator.ValidateServerStartup();
        }

        /// <summary>
        /// Creates the cancellation and task-ownership context for a new server run.
        /// </summary>
        /// <param name="runGeneration">Unique generation assigned to the new run.</param>
        /// <returns><c>true</c> when the run was created; otherwise, <c>false</c>.</returns>
        protected bool TryBeginServerRunLifecycle(out long runGeneration)
        {
            lock (lifecycleSync)
            {
                if (activeRun != null)
                {
                    runGeneration = 0;
                    return false;
                }

                runGeneration = ++nextRunGeneration;
                activeRun = new ServerRunContext(
                    runGeneration,
                    Thread.CurrentThread.ManagedThreadId);
                isStopping = false;
                return true;
            }
        }

        private bool CanContinueServerStartup(long runGeneration)
        {
            lock (lifecycleSync)
                return activeRun != null &&
                       activeRun.Generation == runGeneration &&
                       !activeRun.IsStopRequested;
        }

        private bool TryRunServerStartupAction(long runGeneration, Action action)
        {
            lock (lifecycleSync)
            {
                if (activeRun == null ||
                    activeRun.Generation != runGeneration ||
                    activeRun.IsStopRequested)
                {
                    return false;
                }
            }

            // Admission is serialized with stop, but user callbacks run outside the lifecycle lock.
            // StartupCompleted still prevents transport cleanup until the admitted callback returns.
            action?.Invoke();

            return CanContinueServerStartup(runGeneration);
        }

        private bool TrySubscribeServerTimer(long runGeneration)
        {
            lock (lifecycleSync)
            {
                if (activeRun == null ||
                    activeRun.Generation != runGeneration ||
                    activeRun.IsStopRequested)
                {
                    return false;
                }

                MstTimer.OnTickEvent += MstTimer_OnTickEvent;
                return true;
            }
        }

        private bool TryInvokeServerStartedEvent(long runGeneration)
        {
            Action startedHandlers = OnServerStartedEvent;

            if (startedHandlers == null)
                return CanContinueServerStartup(runGeneration);

            foreach (Action startedHandler in startedHandlers.GetInvocationList())
            {
                if (!TryRunServerStartupAction(runGeneration, startedHandler))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Marks a prepared server run as running after its socket and modules are ready.
        /// </summary>
        /// <param name="runGeneration">Generation returned by <see cref="TryBeginServerRunLifecycle"/>.</param>
        /// <returns><c>true</c> when the run is still current and was marked running.</returns>
        protected bool TryMarkServerRunStarted(long runGeneration)
        {
            lock (lifecycleSync)
            {
                if (activeRun == null ||
                    activeRun.Generation != runGeneration ||
                    activeRun.IsStopRequested)
                {
                    return false;
                }

                activeRun.State = ServerRunState.Running;
                activeRun.IsAcceptingMessages = true;
                IsRunning = true;
                return true;
            }
        }

        /// <summary>
        /// Completes startup ownership so an overlapping stop can safely finalize the run.
        /// </summary>
        /// <param name="runGeneration">Generation returned by <see cref="TryBeginServerRunLifecycle"/>.</param>
        protected void CompleteServerStartup(long runGeneration)
        {
            ServerRunContext runContext;

            lock (lifecycleSync)
            {
                runContext = activeRun != null && activeRun.Generation == runGeneration
                    ? activeRun
                    : null;
            }

            runContext?.StartupCompleted.TrySetResult(true);
        }

        private void MstTimer_OnTickEvent(long currentTick)
        {
            // Searching peers that are not connected and their inactivity time is out
            var inactivePeers = connectedPeers.Values
                .Where(x => !x.IsConnected && DateTime.Now.Subtract(x.LastActivity).TotalSeconds >= inactivityTimeout);

            foreach (var peer in inactivePeers)
                OnPeerDisconnectedEventHandler(peer);

            // Searching peers that are connected but their validation time is out
            var invalidPeers = unauthenticatedPeers.Values
                .Where(x => DateTime.Now.Subtract(x.StartActivity).TotalSeconds >= validationTimeout);

            foreach (var peer in invalidPeers)
                peer.Disconnect("Validation timeout");
        }

        /// <summary>
        /// 
        /// </summary>
        private void LookForModules()
        {
            if (lookForModules)
            {
                // Find modules
                var modules = lookInChildrenOnly ? GetComponentsInChildren<BaseServerModule>() : FindObjectsOfType<BaseServerModule>();

                // Add modules
                foreach (var module in modules)
                {
                    if (this.modules.TryGetValue(module.GetType(), out IBaseServerModule registeredModule))
                    {
                        if (!ReferenceEquals(registeredModule, module))
                            throw new Exception("A module already exists in the server: " + module.GetType());

                        // The same component remains registered across transport restarts.
                        continue;
                    }

                    AddModuleCore(module);
                }

                // Initialize modules
                InitializeModulesCore();

                // Check and notify if some modules are not uninitialized
                var uninitializedModules = GetUninitializedModules();

                if (uninitializedModules.Count > 0)
                {
                    MstProperties modulesInfo = new();

                    foreach (var module in uninitializedModules.Select(m => m.GetType().Name))
                    {
                        modulesInfo.Add($"\t-{module}", "Not Ready");
                    }

                    logger.Warn($"Some of the {GetType().Name.FromCamelcase()} modules failed to initialize: \n{modulesInfo.ToReadableString("\n", ": ")}");
                }
            }
        }

        protected bool TryStartServerRunModules(long runGeneration)
        {
            List<IServerRunModule> runModules = GetInitializedModules()
                .OfType<IServerRunModule>()
                .ToList();

            foreach (IServerRunModule module in runModules)
            {
                ServerRunContext runContext;

                lock (lifecycleSync)
                {
                    runContext = activeRun != null && activeRun.Generation == runGeneration
                        ? activeRun
                        : null;

                    if (runContext == null || runContext.IsStopRequested)
                        return false;

                    // Register ownership before invoking user code so a partial start is still stopped.
                    runContext.StartedModules.Add(module);
                }

                module.StartServerRun(runContext.Cancellation.Token);

                if (!CanContinueServerStartup(runGeneration))
                    return false;
            }

            return true;
        }

        private async Task EnsureServerRunModulesStoppedAsync(ServerRunContext runContext)
        {
            bool beginStop;

            lock (lifecycleSync)
                beginStop = !runContext.AreModulesStopStarted;

            if (beginStop)
                await RunOnOwnerThreadAsync(() => BeginServerRunModulesStop(runContext)).ConfigureAwait(false);

            await runContext.ModulesStopped.Task.ConfigureAwait(false);
        }

        private void BeginServerRunModulesStop(ServerRunContext runContext)
        {
            List<IServerRunModule> modulesToStop;

            lock (lifecycleSync)
            {
                if (runContext.AreModulesStopStarted)
                    return;

                runContext.AreModulesStopStarted = true;
                modulesToStop = runContext.StartedModules.AsEnumerable().Reverse().ToList();
            }

            _ = StopServerRunModulesInOrderAsync(runContext, modulesToStop);
        }

        private async Task StopServerRunModulesInOrderAsync(ServerRunContext runContext,
            List<IServerRunModule> modulesToStop)
        {
            var stopFailures = new List<Exception>();

            try
            {
                foreach (IServerRunModule module in modulesToStop)
                {
                    Task stopTask = Task.CompletedTask;

                    try
                    {
                        await RunOnOwnerThreadAsync(() =>
                        {
                            if (module is UnityEngine.Object unityModule && !unityModule)
                                return;

                            stopTask = module.StopServerRunAsync() ?? Task.CompletedTask;
                        }).ConfigureAwait(false);

                        await stopTask.ConfigureAwait(false);
                    }
                    catch (Exception exception)
                    {
                        logger.Error($"Server-run module {module.GetType().Name} failed while stopping: {exception}");
                        stopFailures.Add(new InvalidOperationException(
                            $"Server-run module {module.GetType().Name} failed while stopping",
                            exception));
                    }
                }
            }
            finally
            {
                if (stopFailures.Count > 0)
                {
                    runContext.ModuleStopFailure = new AggregateException(
                        "One or more server-run modules failed while stopping",
                        stopFailures);
                }

                runContext.ModulesStopped.TrySetResult(true);
            }
        }

        /// <summary>
        /// Requests server shutdown without waiting for handlers or modules to finish.
        /// </summary>
        public virtual void StopServer()
        {
            _ = RequestServerStop(closeTransportImmediately: true);
        }

        /// <summary>
        /// Stops the server after canceling and draining all work owned by the current run.
        /// </summary>
        /// <returns>A task that completes after handlers, run modules, socket and peers are fully stopped.</returns>
        public virtual Task StopServerAsync()
        {
            return RequestServerStop(closeTransportImmediately: false);
        }

        private Task RequestServerStop(bool closeTransportImmediately)
        {
            ServerRunContext runContext = BeginServerStop(out bool ownsStop);

            if (runContext == null)
                return Task.CompletedTask;

            if (ownsStop)
                RequestServerRunCancellation(runContext);

            EnsureStopCompletionWatcher(runContext);

            if (closeTransportImmediately)
                _ = EnsureTransportClosedAsync(runContext);

            return runContext.StopCompleted.Task;
        }

        private ServerRunContext BeginServerStop(out bool ownsStop)
        {
            ServerRunContext runContext;
            bool handlersAlreadyDrained = false;

            lock (lifecycleSync)
            {
                runContext = activeRun;
                ownsStop = runContext != null && !runContext.IsStopRequested;

                if (!ownsStop)
                    return runContext;

                runContext.IsStopRequested = true;
                runContext.IsAcceptingMessages = false;
                runContext.State = ServerRunState.Stopping;
                isStopping = true;
                IsRunning = false;
                handlersAlreadyDrained = runContext.ActiveHandlers.Count == 0;
            }

            MstTimer.OnTickEvent -= MstTimer_OnTickEvent;

            foreach (IPeer peer in connectedPeers.Values.Concat(unauthenticatedPeers.Values).Distinct())
                peer.OnMessageReceivedEvent -= OnMessageReceived;

            if (handlersAlreadyDrained)
                runContext.HandlersDrained.TrySetResult(true);

            return runContext;
        }

        private void RequestServerRunCancellation(ServerRunContext runContext)
        {
            bool startCancellation;

            lock (lifecycleSync)
            {
                startCancellation = !runContext.IsCancellationStartRequested;
                runContext.IsCancellationStartRequested = true;
            }

            if (startCancellation)
                _ = Task.Run(() => CancelServerRun(runContext));
        }

        private void CancelServerRun(ServerRunContext runContext)
        {
            try
            {
                runContext.Cancellation.Cancel();
            }
            catch (AggregateException exception)
            {
                logger.Error($"One or more server shutdown cancellation callbacks failed: {exception}");
            }
            catch (Exception exception)
            {
                logger.Error($"Server shutdown cancellation failed: {exception}");
            }
            finally
            {
                runContext.CancellationCompleted.TrySetResult(true);
            }
        }

        private void EnsureStopCompletionWatcher(ServerRunContext runContext)
        {
            bool startWatcher;

            lock (lifecycleSync)
            {
                startWatcher = !runContext.IsCompletionWatcherStarted;
                runContext.IsCompletionWatcherStarted = true;
            }

            if (startWatcher)
            {
                _ = DrainAndFinalizeServerRunAsync(runContext);
                _ = ObserveStopCompletionAsync(runContext.StopCompleted.Task);
            }
        }

        private static async Task ObserveStopCompletionAsync(Task stopTask)
        {
            try
            {
                await stopTask.ConfigureAwait(false);
            }
            catch
            {
                // Shutdown failures are logged by the lifecycle pipeline before completion faults.
            }
        }

        private async Task DrainAndFinalizeServerRunAsync(ServerRunContext runContext)
        {
            try
            {
                await runContext.CancellationCompleted.Task.ConfigureAwait(false);
                await runContext.StartupCompleted.Task.ConfigureAwait(false);
                await runContext.HandlersDrained.Task.ConfigureAwait(false);
                await EnsureServerRunModulesStoppedAsync(runContext).ConfigureAwait(false);
                await EnsureTransportClosedAsync(runContext).ConfigureAwait(false);
                await EnsureStopCallbacksPublishedOnOwnerThreadAsync(runContext).ConfigureAwait(false);

                if (runContext.ModuleStopFailure != null)
                {
                    FailServerRun(runContext, runContext.ModuleStopFailure);
                    return;
                }

                CompleteServerRun(runContext);
            }
            catch (Exception exception)
            {
                logger.Error($"Server shutdown failed before lifecycle completion: {exception}");
                FailServerRun(runContext, exception);
            }
        }

        private async Task EnsureTransportClosedAsync(ServerRunContext runContext)
        {
            bool scheduleTransportClose;

            lock (lifecycleSync)
            {
                scheduleTransportClose = !runContext.IsTransportCloseScheduled;
                runContext.IsTransportCloseScheduled = true;
            }

            if (scheduleTransportClose)
            {
                try
                {
                    await Task.Run(() => CloseServerTransportAndPeers(runContext)).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    logger.Error($"Unexpected server transport cleanup failure: {exception}");
                }
            }

            await runContext.TransportClosed.Task.ConfigureAwait(false);
        }

        private async Task EnsureStopCallbacksPublishedOnOwnerThreadAsync(ServerRunContext runContext)
        {
            if (!runContext.StopCallbacksPublished.Task.IsCompleted)
                await RunOnOwnerThreadAsync(() => PublishServerStoppedCallbacks(runContext)).ConfigureAwait(false);

            await runContext.StopCallbacksPublished.Task.ConfigureAwait(false);
        }

        private Task RunOnOwnerThreadAsync(Action action)
        {
            if (IsOwnerThread)
            {
                action();
                return Task.CompletedTask;
            }

            if (ownerThreadContext == null)
            {
                return Task.FromException(new InvalidOperationException(
                    "The server owner SynchronizationContext is unavailable during shutdown"));
            }

            TaskCompletionSource<bool> completion =
                new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                ownerThreadContext.Post(_ =>
                {
                    try
                    {
                        action();
                        completion.TrySetResult(true);
                    }
                    catch (Exception exception)
                    {
                        completion.TrySetException(exception);
                    }
                }, null);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }

            return completion.Task;
        }

        private bool IsOwnerThread => Thread.CurrentThread.ManagedThreadId == ownerThreadId;

        private void CloseServerTransportAndPeers(ServerRunContext runContext)
        {
            lock (lifecycleSync)
            {
                if (runContext.IsTransportCloseStarted)
                    return;

                runContext.IsTransportCloseStarted = true;
            }

            try
            {
                MstTimer.OnTickEvent -= MstTimer_OnTickEvent;

                lock (lifecycleSync)
                {
                    runContext.IsAcceptingMessages = false;
                }

                lock (socketLifecycleSync)
                {
                    try
                    {
                        socket?.Stop();
                    }
                    catch (Exception exception)
                    {
                        logger.Error($"Failed to stop server socket: {exception}");
                    }
                    finally
                    {
                        if (socket != null)
                        {
                            socket.OnPeerConnectedEvent -= OnPeerConnectedEventHandle;
                            socket.OnPeerDisconnectedEvent -= OnPeerDisconnectedEventHandler;
                        }
                    }
                }

                List<IPeer> peersToDisconnect = connectedPeers.Values
                    .Concat(unauthenticatedPeers.Values)
                    .Distinct()
                    .ToList();

                foreach (IPeer peer in peersToDisconnect)
                {
                    try
                    {
                        DisposePeerDuringServerStop(peer);
                    }
                    catch (Exception exception)
                    {
                        logger.Error($"Failed to dispose peer {peer.Id} during server shutdown: {exception}");
                    }
                }
            }
            finally
            {
                runContext.TransportClosed.TrySetResult(true);
            }
        }

        private void PublishServerStoppedCallbacks(ServerRunContext runContext)
        {
            lock (lifecycleSync)
            {
                if (runContext.AreStopCallbacksStarted)
                    return;

                runContext.AreStopCallbacksStarted = true;
            }

            if (!this)
            {
                runContext.StopCallbacksPublished.TrySetResult(true);
                return;
            }

            try
            {
                Action stoppedHandlers = OnServerStoppedEvent;

                if (stoppedHandlers != null)
                {
                    foreach (Action stoppedHandler in stoppedHandlers.GetInvocationList())
                    {
                        try
                        {
                            stoppedHandler.Invoke();
                        }
                        catch (Exception exception)
                        {
                            logger.Error($"A server stopped event subscriber failed: {exception}");
                        }
                    }
                }

                try
                {
                    OnStoppedServer();
                }
                catch (Exception exception)
                {
                    logger.Error($"The server stopped callback failed: {exception}");
                }
            }
            finally
            {
                runContext.StopCallbacksPublished.TrySetResult(true);
            }
        }

        private void CompleteServerRun(ServerRunContext runContext)
        {
            lock (lifecycleSync)
            {
                if (runContext.IsCompletionPublished ||
                    !runContext.TransportClosed.Task.IsCompleted ||
                    !runContext.StopCallbacksPublished.Task.IsCompleted ||
                    runContext.ActiveHandlers.Count > 0)
                {
                    return;
                }

                runContext.IsCompletionPublished = true;
                runContext.State = ServerRunState.Stopped;
                runContext.Dispose();
                runContext.StopCompleted.TrySetResult(true);

                if (ReferenceEquals(activeRun, runContext))
                    activeRun = null;

                isStopping = false;
            }
        }

        private void FailServerRun(ServerRunContext runContext, Exception exception)
        {
            lock (lifecycleSync)
            {
                if (runContext.IsCompletionPublished)
                    return;

                runContext.IsCompletionPublished = true;
                runContext.State = ServerRunState.Stopped;
                runContext.Dispose();
                runContext.StopCompleted.TrySetException(exception);

                if (ReferenceEquals(activeRun, runContext))
                    activeRun = null;

                isStopping = false;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="peer"></param>
        private void OnPeerConnectedEventHandle(IPeer peer)
        {
            bool disposeDuringStop = false;
            bool rejectForCapacity = false;

            lock (lifecycleSync)
            {
                if (activeRun == null || !activeRun.IsAcceptingMessages)
                {
                    disposeDuringStop = true;
                }
                else if (maxConnections > 0 && connectedPeers.Count + unauthenticatedPeers.Count >= maxConnections)
                {
                    rejectForCapacity = true;
                }
                else
                {
                    // Listen to messages
                    peer.OnMessageReceivedEvent += OnMessageReceived;

                    // Create the security extension
                    var extension = peer.AddExtension(new SecurityInfoPeerExtension(peer));

                    // Create a unique peer guid
                    extension.UniqueGuid = Mst.Helper.CreateGuid();

                    // Waiting for authentication
                    unauthenticatedPeers[peer.Id] = peer;
                }
            }

            if (disposeDuringStop)
            {
                DisposePeerDuringServerStop(peer);
                return;
            }

            if (rejectForCapacity)
                peer.Disconnect("The max number of connections has been reached");
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="peer"></param>
        private void OnPeerDisconnectedEventHandler(IPeer peer)
        {
            if (isStopping)
            {
                DisposePeerDuringServerStop(peer);
                return;
            }

            try
            {
                SecurityInfoPeerExtension extension = peer.GetExtension<SecurityInfoPeerExtension>();

                if (extension == null)
                {
                    CompletePeerDisconnection(peer);
                    return;
                }

                lock (extension.PermissionLifecycleSync)
                    CompletePeerDisconnection(peer);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void CompletePeerDisconnection(IPeer peer)
        {
            peer.OnMessageReceivedEvent -= OnMessageReceived;
            bool wasAuthenticated = connectedPeers.TryRemove(peer.Id, out _);
            unauthenticatedPeers.TryRemove(peer.Id, out _);

            if (wasAuthenticated)
            {
                OnPeerDisconnected(peer);
                OnPeerDisconnectedEvent?.Invoke(peer);
            }

            peer.Dispose();
            logger.Debug($"Client {peer.Id} disconnected from server. Total clients are: {connectedPeers.Count}");
        }

        private void DisposePeerDuringServerStop(IPeer peer)
        {
            if (peer == null)
                return;

            peer.OnMessageReceivedEvent -= OnMessageReceived;
            connectedPeers.TryRemove(peer.Id, out _);
            unauthenticatedPeers.TryRemove(peer.Id, out _);
            peer.Dispose();
        }

        /// <summary>
        /// Invokes when new <see cref="IPeer"/> connected
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnPeerConnected(IPeer peer) { }

        /// <summary>
        /// Invokes when existing <see cref="IPeer"/> disconnected
        /// </summary>
        /// <param name="peer"></param>
        protected virtual void OnPeerDisconnected(IPeer peer) { }

        /// <summary>
        /// Invokes when message received
        /// </summary>
        /// <param name="message"></param>
        protected virtual void OnMessageReceived(IIncomingMessage message)
        {
            ServerRunContext runContext;
            long handlerId;
            Task handlerTask;

            lock (lifecycleSync)
            {
                runContext = activeRun;

                if (runContext == null || !runContext.IsAcceptingMessages)
                    return;

                handlerId = ++runContext.NextHandlerId;
                handlerTask = Task.Run(() => ProcessIncomingMessageAsync(
                    message,
                    runContext.Cancellation.Token));
                runContext.ActiveHandlers[handlerId] = handlerTask;
            }

            _ = ObserveMessageHandlerAsync(runContext, handlerId, handlerTask);
        }

        private async Task ProcessIncomingMessageAsync(IIncomingMessage message, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                message.Peer.LastActivity = DateTime.Now;

                SecurityInfoPeerExtension security = message.Peer.GetExtension<SecurityInfoPeerExtension>();

                if ((security == null || !security.IsConnectionAuthenticated) && !IsAccessHandshakeOpCode(message.OpCode))
                {
                    logger.Warn($"Unauthenticated peer {message.Peer.Id} attempted opcode [{Extensions.StringExtensions.FromHash(message.OpCode)}]");
                    await RejectPermissionAccess(message, ResponseStatus.Unauthorized,
                        cancellationToken, MstErrorCodes.CONNECTION_UNAUTHENTICATED);
                    return;
                }

                if (handlers.TryGetValue(message.OpCode, out IAsyncPacketHandler handler))
                {
                    await handler.HandleAsync(message, cancellationToken);
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    logger.Error($"You are trying to handle message with OpCode [{Extensions.StringExtensions.FromHash(message.OpCode)}]. " +
                        $"But a handler for this message does not exist. " +
                        $"This may have happened because you did not initialize the server module that should handle this message or did not register the message handler properly.");

                    if (message.IsExpectingResponse)
                        message.RespondError(ResponseStatus.BadRequest,
                            MstErrorCodes.REQUEST_HANDLER_NOT_FOUND);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Expected server shutdown path.
            }
            catch (Exception e)
            {
                logger.Error($"An error occurred while handling a message from client. Message OpCode: [{Extensions.StringExtensions.FromHash(message.OpCode)}], Error: {e}");

                if (message.IsExpectingResponse && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        message.RespondError(ResponseStatus.Error,
                            MstErrorCodes.REQUEST_HANDLER_FAILED);
                    }
                    catch (Exception responseException)
                    {
                        logger.Error($"Failed to send an error response for message OpCode [{Extensions.StringExtensions.FromHash(message.OpCode)}]: {responseException}");
                    }
                }
            }
        }

        private async Task ObserveMessageHandlerAsync(ServerRunContext runContext, long handlerId, Task handlerTask)
        {
            try
            {
                await handlerTask;
            }
            catch (Exception exception)
            {
                logger.Error($"A tracked server message task failed outside its processing boundary: {exception}");
            }
            finally
            {
                bool handlersDrained;

                lock (lifecycleSync)
                {
                    runContext.ActiveHandlers.Remove(handlerId);
                    handlersDrained = runContext.IsStopRequested && runContext.ActiveHandlers.Count == 0;
                }

                if (handlersDrained)
                    runContext.HandlersDrained.TrySetResult(true);
            }
        }

        /// <summary>
        /// Invokes when server stopped
        /// </summary>
        protected virtual void OnStoppedServer() { }

        /// <summary>
        /// Invokes when server started
        /// </summary>
        protected virtual void OnStartedServer()
        {
            if (lookForModules)
            {
                var initializedModules = GetInitializedModules();

                if (initializedModules.Count > 0)
                {
                    MstProperties modulesInfo = new();

                    foreach (var module in initializedModules.Select(m => m.GetType().Name))
                    {
                        modulesInfo.Add($"\t-{module}", "Ready");
                    }

                    logger.Info($"Successfully initialized modules: \n{modulesInfo.ToReadableString("\n", ": ")}");
                }
                else
                {
                    logger.Info("No modules found");
                }
            }

            logger.Info($"{GetType().Name.FromCamelcase()} started and listening to: {serverIp}:{serverPort}");
        }

        #region IServer

        /// <summary>
        /// Add new module to list
        /// </summary>
        /// <param name="module"></param>
        public void AddModule(IBaseServerModule module)
        {
            lock (lifecycleSync)
            {
                EnsureModuleMutationAllowedLocked();
                AddModuleCore(module);
            }
        }

        private void AddModuleCore(IBaseServerModule module)
        {
            if (modules.ContainsKey(module.GetType()))
            {
                throw new Exception("A module already exists in the server: " + module.GetType());
            }

            modules[module.GetType()] = module;
        }

        /// <summary>
        /// Add new module to list and start it
        /// </summary>
        /// <param name="module"></param>
        public void AddModuleAndInitialize(IBaseServerModule module)
        {
            lock (lifecycleSync)
            {
                EnsureModuleMutationAllowedLocked();
                AddModuleCore(module);
                InitializeModulesCore();
            }
        }

        private void EnsureModuleMutationAllowedLocked()
        {
            if (activeRun != null)
            {
                throw new InvalidOperationException(
                    "Server modules cannot be added or initialized while a server run is active");
            }
        }

        /// <summary>
        /// Check is server contains module with given name
        /// </summary>
        /// <param name="module"></param>
        /// <returns></returns>
        public bool ContainsModule(IBaseServerModule module)
        {
            return modules.ContainsKey(module.GetType());
        }

        /// <summary>
        /// Start all asigned modules
        /// </summary>
        /// <returns></returns>
        public bool InitializeModules()
        {
            lock (lifecycleSync)
            {
                EnsureModuleMutationAllowedLocked();
                return InitializeModulesCore();
            }
        }

        private bool InitializeModulesCore()
        {
            var checkOptional = true;

            // Initialize modules
            while (true)
            {
                var changed = false;
                foreach (var entry in modules)
                {
                    // Module is already initialized or failed during this server lifecycle
                    if (initializedModules.Contains(entry.Key) || failedModules.Contains(entry.Key))
                    {
                        continue;
                    }

                    // Not all dependencies have been initialized
                    if (!entry.Value.Dependencies.All(d => initializedModules.Any(d.IsAssignableFrom)))
                    {
                        continue;
                    }

                    // Not all OPTIONAL dependencies have been initialized
                    if (checkOptional && !entry.Value.OptionalDependencies.All(d => initializedModules.Any(d.IsAssignableFrom)))
                    {
                        continue;
                    }

                    try
                    {
                        // If we got here, we can initialize our module
                        entry.Value.Server = this;
                        entry.Value.Initialize(this);
                        initializedModules.Add(entry.Key);
                        initializedModuleOrder.Add(entry.Key);

                        // Keep checking optional if something new was initialized
                        checkOptional = true;
                        changed = true;
                    }
                    catch (Exception exception)
                    {
                        entry.Value.Server = null;
                        failedModules.Add(entry.Key);
                        logger.Error($"Failed to initialize module {entry.Key.Name}: {exception}");
                    }
                }

                // If we didn't change anything, and initialized all that we could
                // with optional dependencies in mind
                if (!changed && checkOptional)
                {
                    // Initialize everything without checking optional dependencies
                    checkOptional = false;
                    continue;
                }

                // If we can no longer initialize anything
                if (!changed)
                {
                    return !GetUninitializedModules().Any();
                }
            }
        }

        /// <summary>
        /// Get <see cref="IBaseServerModule"/> module
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetModule<T>() where T : class, IBaseServerModule
        {
            modules.TryGetValue(typeof(T), out IBaseServerModule module);

            // Try to find an assignable module
            module ??= modules.Values.FirstOrDefault(m => m is T);
            return module as T;
        }

        /// <summary>
        /// Gets module by Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public T GetModuleById<T>(string id) where T : class, IBaseServerModule
        {
            var module = modules.Values.FirstOrDefault(m => m.Id == id);
            return module as T;
        }

        /// <summary>
        /// Get all modules that are already started
        /// </summary>
        /// <returns></returns>
        public List<IBaseServerModule> GetInitializedModules()
        {
            return initializedModuleOrder
                .Where(modules.ContainsKey)
                .Select(type => modules[type])
                .ToList();
        }

        /// <summary>
        /// Get all modules that are not started yet
        /// </summary>
        /// <returns></returns>
        public List<IBaseServerModule> GetUninitializedModules()
        {
            return modules
                .Where(m => !initializedModules.Contains(m.Key))
                .Select(m => m.Value)
                .ToList();
        }

        /// <summary>
        /// Resolves a configured permission key to its numeric permission level.
        /// </summary>
        /// <param name="key">Permission key configured on the server.</param>
        /// <param name="permissionLevel">Resolved permission level when the key exists.</param>
        /// <returns><c>true</c> when the key exists; otherwise, <c>false</c>.</returns>
        public bool TryGetPermissionLevel(string key, out int permissionLevel)
        {
            permissionLevel = MstPermissionLevels.Default;

            if (!TryGetPermissionEntry(key, out PermissionEntry entry))
                return false;

            permissionLevel = MstPermissionLevels.Clamp(entry.permissionLevel);
            return true;
        }

        /// <summary>
        /// Set message handler
        /// </summary>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(IAsyncPacketHandler handler)
        {
            if (!handlers.ContainsKey(handler.OpCode))
                handlers[handler.OpCode] = handler;
            else
                logger.Error($"Handler with opcode {handler.OpCode} is already registered");
        }

        /// <summary>
        /// Set message handler
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(ushort opCode, AsyncIncommingMessageHandler handler)
        {
            RegisterMessageHandler(new AsyncPacketHandler(opCode, handler));
        }

        /// <summary>
        /// Sets a cancellation-aware message handler.
        /// </summary>
        /// <param name="opCode">Operation code handled by the callback.</param>
        /// <param name="handler">Cancellation-aware handler.</param>
        public void RegisterMessageHandler(ushort opCode, CancellableAsyncIncomingMessageHandler handler)
        {
            RegisterMessageHandler(new AsyncPacketHandler(opCode, handler));
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(string opCode, AsyncIncommingMessageHandler handler)
        {
            ushort code = opCode.ToUint16Hash();
            RegisterMessageHandler(code, handler);
        }

        /// <summary>
        /// Sets a cancellation-aware message handler using a string operation code.
        /// </summary>
        /// <param name="opCode">String operation code.</param>
        /// <param name="handler">Cancellation-aware handler.</param>
        public void RegisterMessageHandler(string opCode, CancellableAsyncIncomingMessageHandler handler)
        {
            ushort code = opCode.ToUint16Hash();
            RegisterMessageHandler(code, handler);
        }

        /// <summary>
        /// Get connected <see cref="IPeer"/>
        /// </summary>
        /// <param name="peerId"></param>
        /// <returns></returns>
        public IPeer GetPeer(int peerId)
        {
            connectedPeers.TryGetValue(peerId, out IPeer peer);
            return peer;
        }

        #endregion

        #region MESSAGE HANDLERS

        protected virtual async Task ServerAccessChallengeRequestHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecurityInfoPeerExtension extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();

            if (extension == null)
            {
                await RejectPermissionAccess(message, ResponseStatus.Unauthorized, cancellationToken,
                    MstErrorCodes.SECURITY_CONTEXT_MISSING);
                return;
            }

            string permissionKey;

            try
            {
                permissionKey = message.AsPacket<ServerAccessChallengeRequestPacket>().PermissionKey;
            }
            catch (Exception exception)
            {
                logger.Warn($"Invalid permission challenge request from peer {message.Peer.Id}: {exception.Message}");
                await RejectPermissionAccess(message, ResponseStatus.BadRequest, cancellationToken,
                    MstErrorCodes.PERMISSION_REQUEST_INVALID);
                return;
            }

            if (string.IsNullOrWhiteSpace(permissionKey))
            {
                await RejectPermissionAccess(message, ResponseStatus.BadRequest, cancellationToken,
                    MstErrorCodes.PERMISSION_REQUEST_INVALID);
                return;
            }

            if ((!extension.IsConnectionAuthenticated && permissionKey != MstPermissionKeys.Default) ||
                permissionKey == MstPermissionKeys.Admin ||
                !TryGetPermissionEntry(permissionKey, out _))
            {
                await RejectPermissionAccess(message, ResponseStatus.Unauthorized, cancellationToken);
                return;
            }

            byte[] challengeId = CreateRandomBytes(ServerAccessChallengePacket.ChallengeIdSize);
            byte[] nonce = CreateRandomBytes(ServerAccessChallengePacket.NonceSize);
            long expiresAtUtcTicks = DateTime.UtcNow.AddSeconds(validationTimeout).Ticks;
            cancellationToken.ThrowIfCancellationRequested();

            if (!extension.TryIssuePermissionChallenge(permissionKey, challengeId, nonce, expiresAtUtcTicks))
            {
                await RejectPermissionAccess(message, ResponseStatus.BadRequest, cancellationToken,
                    MstErrorCodes.PERMISSION_REQUEST_INVALID);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            message.Respond(new ServerAccessChallengePacket
            {
                ChallengeId = challengeId,
                Nonce = nonce,
                ExpiresAtUtcTicks = expiresAtUtcTicks
            }, ResponseStatus.Success);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        protected virtual async Task PeerGuidRequestHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();

            if (extension != null)
            {
                message.Respond(extension.UniqueGuid.ToByteArray(), ResponseStatus.Success);
            }
            else
            {
                message.RespondError(ResponseStatus.Unauthorized,
                    MstErrorCodes.SECURITY_CONTEXT_MISSING);
                await Task.Delay(200, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                message.Peer.Disconnect(ResponseStatus.Unauthorized.ToString());
            }
        }

        private void NormalizePermissions()
        {
            permissions ??= new List<PermissionEntry>();

            var customPermissions = new List<PermissionEntry>();
            var builtInSecrets = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (PermissionEntry entry in permissions)
            {
                if (string.IsNullOrWhiteSpace(entry.key))
                    continue;

                if (IsSystemPermissionKey(entry.key))
                {
                    if (!builtInSecrets.ContainsKey(entry.key) && !string.IsNullOrWhiteSpace(entry.secret))
                        builtInSecrets.Add(entry.key, entry.secret);

                    continue;
                }

                customPermissions.Add(new PermissionEntry(entry.key, entry.permissionLevel, entry.secret));
            }

            permissions.Clear();
            permissions.Add(new PermissionEntry(MstPermissionKeys.Default, MstPermissionLevels.Default,
                GetBuiltInSecret(builtInSecrets, MstPermissionKeys.Default)));
            permissions.Add(new PermissionEntry(MstPermissionKeys.Admin, MstPermissionLevels.Admin,
                string.Empty));
            permissions.Add(new PermissionEntry(MstPermissionKeys.RoomServer, MstPermissionLevels.RoomServer,
                GetBuiltInSecret(builtInSecrets, MstPermissionKeys.RoomServer)));
            permissions.Add(new PermissionEntry(MstPermissionKeys.Spawner, MstPermissionLevels.Spawner,
                GetBuiltInSecret(builtInSecrets, MstPermissionKeys.Spawner)));
            permissions.AddRange(customPermissions);
        }

        private static string GetBuiltInSecret(IReadOnlyDictionary<string, string> configuredSecrets,
            string permissionKey)
        {
            return configuredSecrets.TryGetValue(permissionKey, out string secret)
                ? secret
                : MstPermissionSecrets.GetDefault(permissionKey);
        }

        private static bool IsSystemPermissionKey(string key)
        {
            return key == MstPermissionKeys.Default ||
                   key == MstPermissionKeys.Admin ||
                   key == MstPermissionKeys.RoomServer ||
                   key == MstPermissionKeys.Spawner;
        }

        private static bool IsAccessHandshakeOpCode(ushort opCode)
        {
            return opCode == MstOpCodes.ServerAccessChallengeRequest ||
                   opCode == MstOpCodes.ServerAccessRequest;
        }

        private bool LoadPermissionConfiguration()
        {
            permissionEntriesByKey.Clear();

            foreach (PermissionEntry entry in permissions)
            {
                if (string.IsNullOrWhiteSpace(entry.key))
                {
                    logger.Error("Permission list contains an empty key");
                    return false;
                }

                if (permissionEntriesByKey.ContainsKey(entry.key))
                {
                    logger.Error($"Permission list contains duplicate key '{entry.key}'");
                    return false;
                }

                permissionEntriesByKey.Add(entry.key,
                    new PermissionEntry(entry.key, entry.permissionLevel, entry.secret));
            }

            var configuredKeys = new HashSet<string>(StringComparer.Ordinal);

            if (Mst.Args.IsProvided(Mst.Args.Names.PermissionCredentials) &&
                !TryApplyPermissionCredentials(configuredKeys))
            {
                return false;
            }

            if (configuredKeys.Count > 0)
            {
                logger.Info($"Loaded permission credentials for keys: {string.Join(", ", configuredKeys.OrderBy(key => key))}");
            }

            return true;
        }

        private bool TryApplyPermissionCredentials(ISet<string> configuredKeys)
        {
            if (!Mst.Args.TryGetJson(Mst.Args.Names.PermissionCredentials, out MstJson credentialsJson))
            {
                logger.Error($"Argument {Mst.Args.Names.PermissionCredentials} must contain valid JSON");
                return false;
            }

            if (!credentialsJson.IsObject || credentialsJson.Keys == null || credentialsJson.Values == null)
            {
                logger.Error($"Argument {Mst.Args.Names.PermissionCredentials} must be a JSON object");
                return false;
            }

            for (int i = 0; i < credentialsJson.Count; i++)
            {
                string permissionKey = credentialsJson.Keys[i];
                MstJson secretValue = credentialsJson.Values[i];

                if (string.IsNullOrWhiteSpace(permissionKey))
                {
                    logger.Error($"Argument {Mst.Args.Names.PermissionCredentials} contains an empty permission key");
                    return false;
                }

                if (!configuredKeys.Add(permissionKey))
                {
                    logger.Error($"Argument {Mst.Args.Names.PermissionCredentials} contains duplicate key '{permissionKey}'");
                    return false;
                }

                if (secretValue == null || !secretValue.IsString)
                {
                    logger.Error($"Permission credential '{permissionKey}' must be a JSON string");
                    return false;
                }

                if (!permissionEntriesByKey.TryGetValue(permissionKey, out PermissionEntry entry))
                {
                    logger.Error($"Permission credential '{permissionKey}' is not declared in the server Inspector");
                    return false;
                }

                string secret = secretValue.StringValue ?? string.Empty;

                if (permissionKey != MstPermissionKeys.Admin && string.IsNullOrWhiteSpace(secret))
                {
                    logger.Error($"Permission credential '{permissionKey}' cannot be empty");
                    return false;
                }

                entry.secret = secret;
                permissionEntriesByKey[permissionKey] = entry;
            }

            return true;
        }

        private bool TryGetPermissionEntry(string permissionKey, out PermissionEntry permissionEntry)
        {
            permissionEntry = default;

            if (string.IsNullOrWhiteSpace(permissionKey))
                return false;

            if (permissionEntriesByKey.TryGetValue(permissionKey, out permissionEntry))
                return true;

            if (permissions == null)
                return false;

            foreach (PermissionEntry entry in permissions)
            {
                if (string.Equals(entry.key, permissionKey, StringComparison.Ordinal))
                {
                    permissionEntry = new PermissionEntry(entry.key, entry.permissionLevel, entry.secret);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="message"></param>
        protected virtual Task SealChallengeRequestHandler(
            IIncomingMessage message,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SecurityInfoPeerExtension extension =
                message.Peer.GetExtension<SecurityInfoPeerExtension>();
            MstSealChallengeRequestPacket request;

            try
            {
                request = message.AsPacket<MstSealChallengeRequestPacket>();
            }
            catch (Exception exception)
            {
                logger.Warn(
                    $"Invalid encryption challenge request from peer {message.Peer.Id}: " +
                    exception.Message);
                message.RespondError(ResponseStatus.Invalid,
                    MstErrorCodes.ENCRYPTION_CHALLENGE_REQUEST_INVALID);
                return Task.CompletedTask;
            }

            if (!Mst.Security.TryCreateSealChallenge(
                    extension,
                    request,
                    out MstSealChallengePacket challenge,
                    out string error))
            {
                logger.Warn(
                    $"Encryption challenge rejected for peer {message.Peer.Id}: {error}");
                message.RespondError(ResponseStatus.Error,
                    MstErrorCodes.ENCRYPTION_CHALLENGE_UNAVAILABLE);
                return Task.CompletedTask;
            }

            message.Respond(challenge, ResponseStatus.Success);
            return Task.CompletedTask;
        }

        private static string ResolveSecurityKeyRingPath(string configuredPath)
        {
            if (string.IsNullOrWhiteSpace(configuredPath))
                return string.Empty;

            if (Path.IsPathRooted(configuredPath))
                return Path.GetFullPath(configuredPath);

            string root = Path.GetDirectoryName(Application.dataPath);

            if (string.IsNullOrEmpty(root))
                root = Directory.GetCurrentDirectory();

            return Path.GetFullPath(Path.Combine(root, configuredPath));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private async Task ServerAccessRequestHandler(IIncomingMessage message, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProvideServerAccessCheckPacket accessProof;

            try
            {
                accessProof = message.AsPacket<ProvideServerAccessCheckPacket>();
            }
            catch (Exception exception)
            {
                logger.Warn($"Invalid permission proof packet from peer {message.Peer.Id}: {exception.Message}");
                await RejectPermissionAccess(message, ResponseStatus.BadRequest, cancellationToken,
                    MstErrorCodes.PERMISSION_REQUEST_INVALID);
                return;
            }

            SecurityInfoPeerExtension extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();

            if (extension == null || accessProof.Version != ProvideServerAccessCheckPacket.CurrentVersion)
            {
                await RejectPermissionAccess(message, ResponseStatus.Unauthorized, cancellationToken);
                return;
            }

            if (!extension.TryConsumePermissionChallenge(accessProof.ChallengeId,
                    out string permissionKey, out byte[] nonce, out long expiresAtUtcTicks))
            {
                await RejectPermissionAccess(message, ResponseStatus.Unauthorized, cancellationToken);
                return;
            }

            if (permissionKey == MstPermissionKeys.Admin ||
                !TryGetPermissionEntry(permissionKey, out PermissionEntry permissionEntry))
            {
                await RejectPermissionAccess(message, ResponseStatus.Unauthorized, cancellationToken);
                return;
            }

            byte[] expectedProof = MstSecurity.CreateAccessProof(
                permissionEntry.secret,
                service,
                permissionKey,
                accessProof.ChallengeId,
                nonce,
                expiresAtUtcTicks);

            if (!MstSecurity.FixedTimeEquals(expectedProof, accessProof.Proof))
            {
                await RejectPermissionAccess(message, ResponseStatus.Unauthorized, cancellationToken);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            lock (extension.PermissionLifecycleSync)
            {
                bool wasAuthenticated = extension.IsConnectionAuthenticated;

                if (!wasAuthenticated && permissionKey != MstPermissionKeys.Default)
                {
                    message.RespondError(ResponseStatus.Unauthorized,
                        MstErrorCodes.DEFAULT_PERMISSION_REQUIRED);
                    return;
                }

                if (wasAuthenticated)
                {
                    if (!connectedPeers.TryGetValue(message.Peer.Id, out IPeer connectedPeer) ||
                        connectedPeer == null ||
                        !connectedPeer.IsConnected)
                    {
                        return;
                    }

                    extension.GrantPermission(permissionKey, permissionEntry.permissionLevel);
                    message.Respond(permissionEntry.permissionLevel, ResponseStatus.Success);
                    logger.Debug($"Client {message.Peer.Id} received permission {permissionKey}");
                }
                else
                {
                    if (!unauthenticatedPeers.TryGetValue(message.Peer.Id, out IPeer peer) ||
                        peer == null ||
                        !peer.IsConnected)
                    {
                        return;
                    }

                    extension.GrantPermission(permissionKey, permissionEntry.permissionLevel);
                    connectedPeers[peer.Id] = peer;
                    unauthenticatedPeers.TryRemove(peer.Id, out _);
                    totalPeersCount++;

                    if (connectedPeers.Count > highestPeersCount)
                        highestPeersCount = connectedPeers.Count;

                    message.Respond(permissionEntry.permissionLevel, ResponseStatus.Success);
                    OnPeerConnected(peer);
                    OnPeerConnectedEvent?.Invoke(peer);

                    logger.Debug($"Client {peer.Id} authenticated with default permission. Total clients are: {connectedPeers.Count}");
                }
            }
        }

        private async Task RejectPermissionAccess(IIncomingMessage message, ResponseStatus status,
            CancellationToken cancellationToken, string errorCode = MstErrorCodes.PERMISSION_DENIED)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rejectedPeersCount++;

            if (message.Peer == null || !message.Peer.IsConnected)
                return;

            SecurityInfoPeerExtension extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();
            bool disconnectPeer = extension == null || !extension.IsConnectionAuthenticated;

            message.RespondError(status, errorCode);

            if (!disconnectPeer)
                return;

            await Task.Delay(AccessRejectionDisconnectDelayMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();

            if (extension != null && extension.IsConnectionAuthenticated)
                return;

            if (message.Peer.IsConnected)
                message.Peer.Disconnect(status.ToString());
        }

        private static byte[] CreateRandomBytes(int count)
        {
            var bytes = new byte[count];

            using (RandomNumberGenerator random = RandomNumberGenerator.Create())
            {
                random.GetBytes(bytes);
            }

            return bytes;
        }

        #endregion
    }
}
