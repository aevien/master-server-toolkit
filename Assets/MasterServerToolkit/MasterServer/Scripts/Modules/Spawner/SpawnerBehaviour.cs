using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnerBehaviour : MonoBehaviour
    {
        #region INSPECTOR

        [SerializeField]
        private HelpBox headerEditor = new HelpBox()
        {
            Text = "This creates and registers a spawner, which can spawn " +
                   "game servers and other processes",
            Type = HelpBoxType.Info
        };

        [SerializeField]
        private HelpBox headerWarn = new HelpBox()
        {
            Text = $"It will start ONLY if '-msfStartSpawner' argument is found, or if StartSpawner() is called manually from your scripts",
            Type = HelpBoxType.Warning
        };

        [Header("General"), SerializeField, Tooltip("Log level of this script's logger")]
        protected LogLevel logLevel = LogLevel.Info;

        [SerializeField, Tooltip("Log level of internal SpawnerController logger")]
        protected LogLevel spawnerLogLevel = LogLevel.Warn;

        [Header("Spawner Default Options")]
        [SerializeField, Tooltip("Default IP address")]
        protected string machineIp = "127.0.0.1";

        [SerializeField, Tooltip("Default path to executable file")]
        protected string executableFilePath = "";

        [SerializeField, Tooltip("Max number of rooms/server SpawnerController can run")]
        protected int maxProcesses = 5;

        [SerializeField, Tooltip("Use this to set whether or not to spawn room/server for browser games. This feature works only if game server uses websocket transport for connections")]
        protected bool spawnWebSocketServers = false;

        [SerializeField, Tooltip("Spawner region used when you are trying to start rooms by given region. Empty means International")]
        protected string region = "";

        [Header("Runtime Settings"), SerializeField, Tooltip("If true, kills all spawned processes when spawners stopped")]
        protected bool killProcessesWhenStop = true;

        [Header("Editor Settings"), SerializeField]
        private HelpBox hpEditor = new HelpBox()
        {
            Text = "Editor settings are used only while running in editor and for test purpose only",
            Type = HelpBoxType.Warning
        };

        [Header("Running in Editor"), SerializeField, Tooltip("If true, when running in editor, spawner server will start automatically (after connecting to master)")]
        protected bool autoStartInEditor = true;

        [SerializeField, Tooltip("If true, and if running in editor, path to executable will be overriden, and a value from 'exePathFromEditor' will be used.")]
        protected bool overrideExePathInEditor = true;

        [SerializeField, Tooltip("Path to the executable to be spawned as server")]
        protected string exePathFromEditor = "C:/Please set your own path";

        #endregion

        /// <summary>
        /// Current spawner controller assigned to this behaviour
        /// </summary>
        protected ISpawnerController spawnerController;

        /// <summary>
        /// Just logger :)
        /// </summary>
        protected Logging.Logger logger;

        /// <summary>
        /// Check if spawner is ready to create rooms/servers
        /// </summary>
        public bool IsSpawnerStarted { get; protected set; } = false;

        /// <summary>
        /// Check if spawner successfully registered
        /// </summary>
        public bool IsSpawnerRegistered => spawnerController != null;

        /// <summary>
        /// Invokes when this spawner is registered in Master server
        /// </summary>
        public UnityEvent OnSpawnerStartedEvent;

        /// <summary>
        /// Invokes when this spawner stopped
        /// </summary>
        public UnityEvent OnSpawnerStoppedEvent;

        protected virtual void Awake()
        {
            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            Mst.Server.Spawners.DefaultPort = Mst.Args.RoomDefaultPort;

            // Subscribe to connection event
            Mst.Connection.AddConnectionOpenListener(OnConnectedToMasterEventHandler);
            // Subscribe to disconnection event
            Mst.Connection.AddConnectionCloseListener(OnDisconnectedFromMasterEventHandler, false);

            DontDestroyOnLoad(gameObject);
        }

        private void OnValidate()
        {
            region = !string.IsNullOrEmpty(region) ? region : "International";
        }

        protected virtual void OnApplicationQuit()
        {
            // Kill all the processes of spawner controller
            if (killProcessesWhenStop)
                spawnerController?.KillProcesses();
        }

        protected virtual void OnDestroy()
        {
            // Remove connection listener
            Mst.Connection.RemoveConnectionOpenListener(OnConnectedToMasterEventHandler);
            // Remove disconnection listener
            Mst.Connection.RemoveConnectionCloseListener(OnDisconnectedFromMasterEventHandler);
        }

        /// <summary>
        /// Fired when spawner connected to master
        /// </summary>
        protected virtual void OnConnectedToMasterEventHandler(IClientSocket client)
        {
            // If we want to start a spawner (cmd argument was found)
            if (Mst.Args.StartSpawner || (autoStartInEditor && Mst.Runtime.IsEditor))
            {
                StartSpawner();
            }
        }

        /// <summary>
        /// Fired when spawner disconnected from master
        /// </summary>
        protected virtual void OnDisconnectedFromMasterEventHandler(IClientSocket client)
        {
            logger.Info("Spawner disconnected from server. Stopping it...");
            StopSpawner();
        }

        /// <summary>
        /// Starts spawner. But before start we are required to be connected
        /// </summary>
        public virtual void StartSpawner()
        {
            // Stop if no connection
            if (!Mst.Connection.IsConnected)
            {
                logger.Error("Spawner cannot be started because of the lack of connection to the master.");
                return;
            }

            // In case we went from one scene to another, but we've already started the spawner
            if (IsSpawnerStarted)
            {
                return;
            }

            // If machine IP is defined in cmd
            machineIp = Mst.Args.AsString(Mst.Args.Names.RoomIp, machineIp);

            // If room region is defined in cmd
            region = Mst.Args.AsString(Mst.Args.Names.RoomRegion, region);

            IsSpawnerStarted = true;

            // Create spawner options
            var spawnerOptions = new SpawnerOptions
            {
                // If MaxProcesses count defined in cmd args
                MaxProcesses = Mst.Args.AsInt(Mst.Args.Names.MaxProcesses, maxProcesses),
                MachineIp = machineIp,
                Region = region
            };

            // If we're running in editor, and we want to override the executable path
            if (Mst.Runtime.IsEditor && overrideExePathInEditor)
            {
                executableFilePath = exePathFromEditor;
            }
            else
            {
                executableFilePath = Mst.Args.AsString(Mst.Args.Names.RoomExecutablePath, executableFilePath);
            }

            logger.Info($"Registering as a spawner with options: \n{spawnerOptions}");

            // 1. Register the spawner
            Mst.Server.Spawners.RegisterSpawner(spawnerOptions, (controller, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    logger.Error($"Failed to create spawner: {error}");
                    return;
                }

                // 2. Save spawner controller
                spawnerController = controller;

                // 3. Set its log level
                spawnerController.Logger.LogLevel = spawnerLogLevel;

                // 4. Set use web sockets if required
                spawnerController.SpawnSettings.UseWebSockets = Mst.Args.AsBool(Mst.Args.Names.UseWebSockets, spawnWebSocketServers);

                // 5. Set the executable path
                spawnerController.SpawnSettings.ExecutablePath = executableFilePath;

                // 6. Set the machine IP
                spawnerController.SpawnSettings.MachineIp = machineIp;

                // 7. Set region
                spawnerController.SpawnSettings.Region = spawnerOptions.Region;

                logger.Info($"Spawner successfully created. Id: {controller.SpawnerId}");

                // 8. Inform listeners
                OnSpawnerStartedEvent?.Invoke();
                OnSpawnerStarted();
            });
        }

        /// <summary>
        /// Stops spawner processes
        /// </summary>
        public virtual void StopSpawner()
        {
            // Kill all the processes of spawner controller
            if (killProcessesWhenStop)
                spawnerController?.KillProcesses();

            // Set spawn behaviour as not started
            IsSpawnerStarted = false;

            if (spawnerController != null)
                logger.Info($"Spawner stopped. Id: {spawnerController.SpawnerId}");

            // Destroy spawner
            spawnerController = null;

            OnSpawnerStoppedEvent?.Invoke();
        }

        /// <summary>
        /// Invokes when spawner registered and started
        /// </summary>
        protected virtual void OnSpawnerStarted() { }
    }
}