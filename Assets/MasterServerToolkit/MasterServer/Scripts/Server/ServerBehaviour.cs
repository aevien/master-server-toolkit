using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System;
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
        protected string serverIP = "127.0.0.1";

        [SerializeField, Tooltip("Port, to which server will listen to")]
        protected int serverPort = 5000;

        [SerializeField, Tooltip("The max number of allowed connections. If 0 - means unlimeted")]
        protected ushort maxConnections = 0;

        [Header("Editor Settings"), SerializeField]
        private HelpBox hpEditor = new HelpBox()
        {
            Text = "Editor settings are used only while running in editor",
            Type = HelpBoxType.Warning
        };

        [SerializeField]
        protected bool autoStartInEditor = true;

        #endregion

        /// <summary>
        /// Server socket
        /// </summary>
        private IServerSocket socket;

        /// <summary>
        /// Server messages handlers list
        /// </summary>
        private Dictionary<short, IPacketHandler> handlers;

        /// <summary>
        /// Server modules handlers
        /// </summary>
        private Dictionary<Type, IBaseServerModule> modules;

        /// <summary>
        /// Initialized server modules list
        /// </summary>
        private HashSet<Type> initializedModules;

        /// <summary>
        /// List of connected clients to server
        /// </summary>
        private Dictionary<int, IPeer> connectedPeers;

        /// <summary>
        /// List of connected clients to server by guids
        /// </summary>
        private Dictionary<Guid, IPeer> peersByGuidLookup;

        /// <summary>
        /// Just message constant
        /// </summary>
        protected const string internalServerErrorMessage = "Internal Server Error";

        /// <summary>
        /// Current server behaviour <see cref="Logger"/>
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// Check if current server is running
        /// </summary>
        public bool IsRunning { get; protected set; } = false;

        /// <summary>
        /// Gets total peers connected to server
        /// </summary>
        public int TotalPeersCount => connectedPeers != null ? connectedPeers.Count : 0;

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
            if (string.IsNullOrEmpty(MstApplicationConfig.Singleton.ApplicationKey)) throw new Exception("ApplicationKey is not defined");

            if (!Mst.Runtime.IsEditor)
                Application.targetFrameRate = Mst.Args.AsInt(Mst.Args.Names.TargetFrameRate, targetFrameRate);

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            connectedPeers = new Dictionary<int, IPeer>();
            modules = new Dictionary<Type, IBaseServerModule>();
            initializedModules = new HashSet<Type>();
            handlers = new Dictionary<short, IPacketHandler>();
            peersByGuidLookup = new Dictionary<Guid, IPeer>();

            // Create the server 
            socket = Mst.Create.ServerSocket();
            socket.LogLevel = logLevel;

            // Setup secure connection
            socket.UseSecure = MstApplicationConfig.Singleton.UseSecure;
            socket.CertificatePath = MstApplicationConfig.Singleton.CertificatePath;
            socket.CertificatePassword = MstApplicationConfig.Singleton.CertificatePassword;
            socket.ApplicationKey = MstApplicationConfig.Singleton.ApplicationKey;

            socket.OnPeerConnectedEvent += OnPeerConnectedEventHandle;
            socket.OnPeerDisconnectedEvent += OnPeerDisconnectedEventHandler;

            // AesKey handler
            RegisterMessageHandler((short)MstMessageCodes.AesKeyRequest, GetAesKeyRequestHandler);
            RegisterMessageHandler((short)MstMessageCodes.PermissionLevelRequest, PermissionLevelRequestHandler);
            RegisterMessageHandler((short)MstMessageCodes.PeerGuidRequest, GetPeerGuidRequestHandler);
        }

        protected virtual void Start()
        {
            if (IsAllowedToBeStartedInEditor())
            {
                // Start the server on next frame
                MstTimer.WaitForEndOfFrame(() =>
                {
                    StartServer();
                });
            }
        }

        protected virtual void OnValidate()
        {
            maxConnections = (ushort)Mathf.Clamp(maxConnections, 0, ushort.MaxValue);
        }

        /// <summary>
        /// Check if server is allowed to be started in editor. This feature is for testing purpose only
        /// </summary>
        /// <returns></returns>
        protected virtual bool IsAllowedToBeStartedInEditor()
        {
            return Mst.Runtime.IsEditor && autoStartInEditor;
        }

        /// <summary>
        /// Sets the server IP
        /// </summary>
        /// <param name="listenToIp"></param>
        public void SetIpAddress(string listenToIp)
        {
            serverIP = listenToIp;
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
            StartServer(serverIP, serverPort);
        }

        /// <summary>
        /// Starts server with given port
        /// </summary>
        /// <param name="listenToPort"></param>
        public virtual void StartServer(int listenToPort)
        {
            StartServer(serverIP, listenToPort);
        }

        /// <summary>
        /// Starts server with given port and ip
        /// </summary>
        /// <param name="listenToIp">IP который слшаем</param>
        /// <param name="listenToPort"></param>
        public virtual void StartServer(string listenToIp, int listenToPort)
        {
            if (IsRunning)
            {
                return;
            }

            serverIP = listenToIp;
            serverPort = listenToPort;

            MstProperties startInfo = new MstProperties();
            startInfo.Add("\tFPS is", Application.targetFrameRate);
            startInfo.Add("\tApp key", socket.ApplicationKey);
            startInfo.Add("\tSecure", socket.UseSecure);
            startInfo.Add("\tCertificate Path", !socket.UseSecure ? "Undefined" : socket.CertificatePath);
            startInfo.Add("\tCertificate Pass", string.IsNullOrEmpty(socket.CertificatePath) || !socket.UseSecure ? "Undefined" : "********");

            logger.Info($"Starting Server...\n{startInfo.ToReadableString(";\n", " ")}");

            socket.Listen(listenToIp, listenToPort);
            LookForModules();
            IsRunning = true;
            OnStartedServer();
            OnServerStartedEvent?.Invoke();
        }

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
                    logger.Warn($"Some of the {GetType().Name} modules failed to initialize: \n{string.Join(" \n", uninitializedModules.Select(m => m.GetType().ToString()).ToArray())}");
                }
            }
        }

        /// <summary>
        /// Stops server
        /// </summary>
        public virtual void StopServer()
        {
            IsRunning = false;
            socket.Stop();
            OnServerStoppedEvent?.Invoke();
            OnStoppedServer();
        }

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

            // Save the peer
            connectedPeers[peer.Id] = peer;

            // Create the security extension
            var extension = peer.AddExtension(new SecurityInfoPeerExtension());

            // Set default permission level
            extension.PermissionLevel = 0;

            // Create a unique peer guid
            extension.UniqueGuid = Guid.NewGuid();
            peersByGuidLookup[extension.UniqueGuid] = peer;

            // Invoke the event
            OnPeerConnectedEvent?.Invoke(peer);
            OnPeerConnected(peer);

            logger.Debug($"Client {peer.Id} connected to server. Total clients are: {connectedPeers.Count}");
        }

        private void OnPeerDisconnectedEventHandler(IPeer peer)
        {
            // Remove listener to messages
            peer.OnMessageReceivedEvent -= OnMessageReceived;

            // Remove the peer
            connectedPeers.Remove(peer.Id);

            var extension = peer.GetExtension<SecurityInfoPeerExtension>();

            if (extension != null)
            {
                // Remove from guid lookup
                peersByGuidLookup.Remove(extension.UniqueGuid);
            }

            // Invoke the event
            OnPeerDisconnectedEvent?.Invoke(peer);
            OnPeerDisconnected(peer);

            logger.Debug($"Client {peer.Id} disconnected from server. Total clients are: {connectedPeers.Count}");
        }

        protected virtual void OnDestroy()
        {
            if (socket != null)
            {
                socket.OnPeerConnectedEvent -= OnPeerConnectedEventHandle;
                socket.OnPeerDisconnectedEvent -= OnPeerDisconnectedEventHandler;
            }
        }

        #region MESSAGE HANDLERS

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        protected virtual void GetPeerGuidRequestHandler(IIncomingMessage message)
        {
            var extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();
            message.Respond(extension.UniqueGuid.ToByteArray(), ResponseStatus.Success);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        protected virtual void PermissionLevelRequestHandler(IIncomingMessage message)
        {
            var key = message.AsString();

            var extension = message.Peer.GetExtension<SecurityInfoPeerExtension>();

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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        protected virtual async void GetAesKeyRequestHandler(IIncomingMessage message)
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

        #endregion

        #region VIRTUAL METHODS

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
                handlers.TryGetValue(message.OpCode, out IPacketHandler handler);

                if (handler == null)
                {
                    logger.Warn(string.Format($"Handler for OpCode {message.OpCode} does not exist"));

                    if (message.IsExpectingResponse)
                    {
                        message.Respond(internalServerErrorMessage, ResponseStatus.NotHandled);
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

                logger.Error($"Error while handling a message from Client. OpCode: {message.OpCode}, Error: {e}");

                if (!message.IsExpectingResponse)
                {
                    return;
                }

                try
                {
                    message.Respond(internalServerErrorMessage, ResponseStatus.Error);
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

        #endregion

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
            handlers[handler.OpCode] = handler;
        }

        /// <summary>
        /// Set message handler
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="handler"></param>
        public void RegisterMessageHandler(short opCode, IncommingMessageHandler handler)
        {
            RegisterMessageHandler(new PacketHandler(opCode, handler));
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
    }
}