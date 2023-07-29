using MasterServerToolkit.Extensions;
using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public abstract class ServerBehaviour : MonoBehaviour, IServer
    {
        #region INSPECTOR

        [SerializeField]
        private HelpBox hpInfo = new HelpBox()
        {
            Text = "This component is responsible for starting a Server and initializing its modules",
            Type = HelpBoxType.Info
        };

        [Header("Server Settings")]
        [SerializeField, Tooltip("If true, will look for game objects with modules in scene, and initialize them")]
        private bool lookForModules = true;

        [SerializeField, Tooltip("If true, will go through children of this GameObject, and initialize modules that are found on the way")]
        private bool lookInChildrenOnly = true;

        [SerializeField]
        private List<PermissionEntry> permissions;

        [SerializeField, Range(10, 120), Tooltip("Frame rate the server will be running. You can limit it from 10 - 120")]
        private int targetFrameRate = 30;

        [SerializeField, Tooltip("Log level of this script")]
        protected LogLevel logLevel = LogLevel.Info;

        [SerializeField, Tooltip("IP address, to which server will listen to")]
        protected string serverIp = "127.0.0.1";

        [SerializeField, Tooltip("Port, to which server will listen to")]
        protected int serverPort = 5000;

        [SerializeField, Tooltip("The max number of allowed connections. If 0 - means unlimeted")]
        protected ushort maxConnections = 0;

        [SerializeField]
        protected float inactivityTimeout = 5f;

        [SerializeField]
        protected float validationTimeout = 5f;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        protected int currentInactivePeersCount => connectedPeers.Values.Where(x => !x.IsConnected).Count();

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

        /// <summary>
        /// Server socket
        /// </summary>
        private IServerSocket socket;

        /// <summary>
        /// Server messages handlers list
        /// </summary>
        private readonly ConcurrentDictionary<ushort, IPacketHandler> handlers = new ConcurrentDictionary<ushort, IPacketHandler>();

        /// <summary>
        /// Server modules
        /// </summary>
        private readonly Dictionary<Type, IBaseServerModule> modules = new Dictionary<Type, IBaseServerModule>();

        /// <summary>
        /// Initialized server modules list
        /// </summary>
        private readonly HashSet<Type> initializedModules = new HashSet<Type>();

        /// <summary>
        /// List of connected clients to server
        /// </summary>
        private readonly ConcurrentDictionary<int, IPeer> connectedPeers = new ConcurrentDictionary<int, IPeer>();

        /// <summary>
        /// 
        /// </summary>
        private readonly ConcurrentDictionary<int, IPeer> unauthenticatedPeers = new ConcurrentDictionary<int, IPeer>();

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
        public int CurrentPeersCount => connectedPeers.Count;

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
            // 
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;
        }

        protected virtual void Start()
        {
            if (!Mst.Settings.HasApplicationKey)
                throw new Exception("ApplicationKey is not defined");

            if (!Mst.Runtime.IsEditor)
                Application.targetFrameRate = Mst.Args.AsInt(Mst.Args.Names.TargetFrameRate, targetFrameRate);

            // Set timeout
            inactivityTimeout = Mst.Args.AsFloat(Mst.Args.Names.ClientInactivityTimeout, inactivityTimeout);
            validationTimeout = Mst.Args.AsFloat(Mst.Args.Names.ClientValidationTimeout, validationTimeout);

            // Create the server 
            socket = Mst.Create.ServerSocket();
            socket.LogLevel = logLevel;

            // Setup secure connection in Start method
            socket.UseSecure = Mst.Settings.UseSecure;
            socket.CertificatePath = Mst.Settings.CertificatePath;
            socket.CertificatePassword = Mst.Settings.CertificatePassword;
            socket.ApplicationKey = Mst.Settings.ApplicationKey;

            socket.OnPeerConnectedEvent += OnPeerConnectedEventHandle;
            socket.OnPeerDisconnectedEvent += OnPeerDisconnectedEventHandler;

            RegisterMessageHandler(MstOpCodes.AesKeyRequest, AesKeyRequestHandler);
            RegisterMessageHandler(MstOpCodes.PermissionLevelRequest, PermissionLevelRequestHandler);
            RegisterMessageHandler(MstOpCodes.PeerGuidRequest, PeerGuidRequestHandler);
            RegisterMessageHandler(MstOpCodes.ServerAccessRequest, ServerAccessRequestHandler);
        }

        protected virtual void OnValidate()
        {
            maxConnections = (ushort)Mathf.Clamp(maxConnections, 0, ushort.MaxValue);
        }

        protected virtual void OnDestroy()
        {
            StopServer();
        }

        #region DEBUG METHODS

        [ContextMenu("Disconnect clients")]
        private void DisconnectAllClients()
        {
            foreach (var peer in connectedPeers.Values)
                peer.Disconnect("Debug");
        }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual MstJson JsonInfo()
        {
            var info = new MstJson();

            try
            {
                info.AddField("initializedModules", GetInitializedModules().Count);
                info.AddField("unitializedModules", GetUninitializedModules().Count);
                info.AddField("activeClients", CurrentPeersCount);
                info.AddField("inactiveClients", currentInactivePeersCount);
                info.AddField("unauthenticatedPeers", unauthenticatedPeers.Count);
                info.AddField("totalClients", totalPeersCount);
                info.AddField("highestClients", highestPeersCount);
                info.AddField("peersAccepted", totalPeersCount);
                info.AddField("peersRejected", rejectedPeersCount);
                info.AddField("useSecure", Mst.Settings.UseSecure);
                info.AddField("certificatePath", Mst.Settings.CertificatePath);
                info.AddField("certificatePassword", Mst.Settings.CertificatePassword);
                info.AddField("applicationKey", Mst.Settings.ApplicationKey);
                info.AddField("localIp", Address);
                info.AddField("publicIp", Address);
                info.AddField("port", Port);
                info.AddField("incomingTraffic", Mst.TrafficStatistics.TotalReceived);
                info.AddField("outgoingTraffic", Mst.TrafficStatistics.TotalSent);
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
        /// <returns></returns>
        public virtual MstProperties Info()
        {
            MstProperties info = new MstProperties();
            info.Set("Initialized modules", GetInitializedModules().Count);
            info.Set("Unitialized modules", GetUninitializedModules().Count);
            info.Set("Active clients", CurrentPeersCount);
            info.Set("Inactive clients", currentInactivePeersCount);
            info.Add("Unauthenticated clients", unauthenticatedPeers.Count);
            info.Set("Total clients", totalPeersCount);
            info.Set("Highest clients", highestPeersCount);
            //info.Set("Updatebles", MstUpdateRunner.Instance.Count);
            info.Set("Peers accepted", totalPeersCount);
            info.Set("Peers rejected", rejectedPeersCount);
            info.Set("Use SSL", Mst.Settings.UseSecure);
            info.Set("Certificate Path", Mst.Settings.CertificatePath);
            info.Set("Certificate Password", Mst.Settings.CertificatePassword);
            info.Set("Application Key", Mst.Settings.ApplicationKey);
            info.Set("Local Ip", Address);
            info.Set("Public Ip", Address);
            info.Set("Port", Port);
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

            serverIp = listenToIp;
            serverPort = listenToPort;

            MstProperties startInfo = new MstProperties();
            startInfo.Add("\tFPS is", Application.targetFrameRate);
            startInfo.Add("\tApp key", socket.ApplicationKey);
            startInfo.Add("\tSecure", socket.UseSecure);
            startInfo.Add("\tCertificate Path", !socket.UseSecure ? "Undefined" : socket.CertificatePath);
            startInfo.Add("\tCertificate Pass", string.IsNullOrEmpty(socket.CertificatePath) || !socket.UseSecure ? "Undefined" : "********");

            logger.Info($"Starting {GetType().Name.ToSpaceByUppercase()}...\n{startInfo.ToReadableString(";\n", " ")}");

            socket.Listen(listenToIp, listenToPort);
            LookForModules();
            IsRunning = true;
            OnStartedServer();
            OnServerStartedEvent?.Invoke();

            MstTimer.OnTickEvent += Instance_OnTickEvent;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="currentTick"></param>
        private void Instance_OnTickEvent(long currentTick)
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
                    AddModule(module);
                }

                // Initialize modules
                InitializeModules();

                // Check and notify if some modules are not uninitialized
                var uninitializedModules = GetUninitializedModules();

                if (uninitializedModules.Count > 0)
                {
                    logger.Warn($"Some of the {GetType().Name.ToSpaceByUppercase()} modules failed to initialize: \n{string.Join(" \n", uninitializedModules.Select(m => m.GetType().ToString()).ToArray())}");
                }
            }
        }

        /// <summary>
        /// Stops server
        /// </summary>
        public virtual void StopServer()
        {
            MstTimer.OnTickEvent -= Instance_OnTickEvent;
            IsRunning = false;

            if (socket != null)
            {
                socket.OnPeerConnectedEvent -= OnPeerConnectedEventHandle;
                socket.OnPeerDisconnectedEvent -= OnPeerDisconnectedEventHandler;
                socket.Stop();
            }

            OnServerStoppedEvent?.Invoke();
            OnStoppedServer();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        private void OnPeerConnectedEventHandle(IPeer peer)
        {
            // Check if max number of connections has been reached
            if (maxConnections > 0 && connectedPeers.Count >= maxConnections)
            {
                peer.Disconnect("The max number of connections has been reached");
                return;
            }

            // Listen to messages
            peer.OnMessageReceivedEvent += OnMessageReceived;

            // Create the security extension
            var extension = peer.AddExtension(new SecurityInfoPeerExtension());

            // Set default permission level
            extension.PermissionLevel = 0;

            // Create a unique peer guid
            extension.UniqueGuid = Mst.Helper.CreateGuid();

            // Waiting for authentication
            unauthenticatedPeers[peer.Id] = peer;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        private void OnPeerDisconnectedEventHandler(IPeer peer)
        {
            // Remove listener to messages
            peer.OnMessageReceivedEvent -= OnMessageReceived;

            // Remove the peer
            connectedPeers.TryRemove(peer.Id, out var _);

            //
            OnPeerDisconnected(peer);

            // Invoke the event
            OnPeerDisconnectedEvent?.Invoke(peer);

            peer?.Dispose();

            logger.Debug($"Client {peer.Id} disconnected from server. Total clients are: {connectedPeers.Count}");
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
            try
            {
                message.Peer.LastActivity = DateTime.Now;
                handlers.TryGetValue(message.OpCode, out IPacketHandler handler);

                if (handler == null)
                {
                    logger.Error($"You are trying to handle message with OpCode [{Extensions.StringExtensions.FromHash(message.OpCode)}]. " +
                        $"But a handler for this message does not exist. " +
                        $"This may have happened because you did not initialize the server module that should handle this message or did not register the message handler properly.");

                    if (message.IsExpectingResponse)
                    {
                        message.Respond(ResponseStatus.NotHandled);
                        return;
                    }

                    return;
                }

                handler.Handle(message);
            }
            catch (Exception e)
            {
                if (Mst.Runtime.IsEditor)
                {
                    throw;
                }

                logger.Error($"An error occurred while handling a message from client. Message OpCode: [{Extensions.StringExtensions.FromHash(message.OpCode)}], Error: {e}");

                if (!message.IsExpectingResponse)
                {
                    return;
                }

                try
                {
                    message.Respond(ResponseStatus.Error);
                }
                catch (Exception exception)
                {
                    Logs.Error(exception);
                }
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
                    logger.Info($"Successfully initialized modules: \n{string.Join(" \n", initializedModules.Select(m => m.GetType().ToString()).ToArray())}");
                }
                else
                {
                    logger.Info("No modules found");
                }
            }
        }

        #region IServer

        /// <summary>
        /// Add new module to list
        /// </summary>
        /// <param name="module"></param>
        public void AddModule(IBaseServerModule module)
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
            AddModule(module);
            InitializeModules();
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
            var checkOptional = true;

            // Initialize modules
            while (true)
            {
                var changed = false;
                foreach (var entry in modules)
                {
                    // Module is already initialized
                    if (initializedModules.Contains(entry.Key))
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

                    // If we got here, we can initialize our module
                    entry.Value.Server = this;
                    entry.Value.Initialize(this);
                    initializedModules.Add(entry.Key);

                    // Keep checking optional if something new was initialized
                    checkOptional = true;
                    changed = true;
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

            if (module == null)
            {
                // Try to find an assignable module
                module = modules.Values.FirstOrDefault(m => m is T);
            }

            return module as T;
        }

        /// <summary>
        /// Get all modules that are already started
        /// </summary>
        /// <returns></returns>
        public List<IBaseServerModule> GetInitializedModules()
        {
            return modules
                .Where(m => initializedModules.Contains(m.Key))
                .Select(m => m.Value)
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
        /// Set message handler
        /// </summary>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(IPacketHandler handler)
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
        public void RegisterMessageHandler(ushort opCode, IncommingMessageHandler handler)
        {
            RegisterMessageHandler(new PacketHandler(opCode, handler));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(string opCode, IncommingMessageHandler handler)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        protected virtual async void PeerGuidRequestHandler(IIncomingMessage message)
        {
            var extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();

            if (extension != null)
            {
                message.Respond(extension.UniqueGuid.ToByteArray(), ResponseStatus.Success);
            }
            else
            {
                message.Respond(ResponseStatus.Unauthorized);
                await Task.Delay(200);
                message.Peer.Disconnect(ResponseStatus.Unauthorized.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        protected virtual async void PermissionLevelRequestHandler(IIncomingMessage message)
        {
            var extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();

            if (extension != null)
            {
                var key = message.AsString();
                var currentLevel = extension.PermissionLevel;
                var newLevel = currentLevel;
                var permissionClaimed = false;

                foreach (var entry in permissions)
                {
                    if (entry.key == key)
                    {
                        newLevel = entry.permissionLevel;
                        permissionClaimed = true;
                    }
                }

                extension.PermissionLevel = newLevel;

                if (!permissionClaimed && !string.IsNullOrEmpty(key))
                {
                    // If we didn't claim a permission
                    message.Respond("Invalid permission key", ResponseStatus.Unauthorized);
                    return;
                }

                message.Respond(newLevel, ResponseStatus.Success);
            }
            else
            {
                message.Respond(ResponseStatus.Unauthorized);
                await Task.Delay(200);
                message.Peer.Disconnect(ResponseStatus.Unauthorized.ToString());
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        protected virtual async void AesKeyRequestHandler(IIncomingMessage message)
        {
            var extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();
            var encryptedKey = extension.AesKeyEncrypted;

            if (encryptedKey != null)
            {
                logger.Debug("There's already a key generated");

                // There's already a key generated
                message.Respond(encryptedKey, ResponseStatus.Success);
                return;
            }

            // Generate a random key
            var aesKey = Mst.Helper.CreateRandomAlphanumericString(8);

            var clientsPublicKeyXml = message.AsString();

            // Deserialize public key
            var sr = new System.IO.StringReader(clientsPublicKeyXml);
            var xs = new System.Xml.Serialization.XmlSerializer(typeof(RSAParameters));
            var clientsPublicKey = (RSAParameters)xs.Deserialize(sr);

            byte[] encryptedAes = await Task.Run(() =>
            {
                using (var csp = new RSACryptoServiceProvider())
                {
                    csp.ImportParameters(clientsPublicKey);
                    encryptedAes = csp.Encrypt(Encoding.Unicode.GetBytes(aesKey), false);

                    // Save keys for later use
                    extension.AesKeyEncrypted = encryptedAes;
                    extension.AesKey = aesKey;

                    return encryptedAes;
                }
            });

            message.Respond(encryptedAes, ResponseStatus.Success);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void ServerAccessRequestHandler(IIncomingMessage message)
        {
            // Get access check options
            var accessCheckOptions = message.AsPacket(new ProvideServerAccessCheckPacket());

            // Request access code from client
            Mst.Security.ValidateConnection(accessCheckOptions, (isSuccess, error) =>
            {
                if (!isSuccess)
                {
                    logger.Error(error);

                    if (message.Peer != null && message.Peer.IsConnected)
                    {
                        message.Respond(error, ResponseStatus.Success);
                        message.Peer.Disconnect(error);
                    }

                    rejectedPeersCount++;
                    return;
                }

                // Remove from authentication list
                if (unauthenticatedPeers.TryRemove(message.Peer.Id, out IPeer peer) && peer != null)
                {
                    // Peer is authenticated
                    message.Respond(ResponseStatus.Success);

                    // Save the peer
                    connectedPeers[peer.Id] = peer;

                    // Add total clients
                    totalPeersCount++;

                    // Set highest peers count info
                    if (connectedPeers.Count > highestPeersCount)
                        highestPeersCount++;

                    // 
                    OnPeerConnected(peer);

                    // Invoke the event
                    OnPeerConnectedEvent?.Invoke(peer);

                    logger.Debug($"Client {peer.Id} connected to server. Total clients are: {connectedPeers.Count}");
                }
            });
        }

        #endregion
    }
}