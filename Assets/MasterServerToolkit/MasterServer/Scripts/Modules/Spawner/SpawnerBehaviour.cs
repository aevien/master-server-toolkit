using System;
using Aevien.Utilities;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    [AddComponentMenu("Master Server Toolkit/SpawnerBehaviour")]
    public class SpawnerBehaviour : Singleton<SpawnerBehaviour>
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

        [SerializeField, Tooltip("If true server will try to get you machine public IP. This feature is for quick way to get IP of the machine on which the room server is running. Do not use it on your local machine.")]
        protected bool usePublicIp = false;

        [SerializeField, Tooltip("Default path to executable file")]
        protected string executableFilePath = "";

        [SerializeField, Tooltip("Max number of rooms/server SpawnerController can run")]
        protected int maxProcesses = 5;

        [SerializeField, Tooltip("Use this to set whether or not to spawn room/server for browser games. This feature works only if game server uses websocket transport for connections")]
        protected bool spawnWebSocketServers = false;

        [SerializeField, Tooltip("Spawner region used when you are trying to start rooms by given region. Empty means International")]
        protected string region = "";

        [Header("Runtime Settings"), SerializeField, Tooltip("If true, kills all spawned processes when master server quits")]
        protected bool killProcessesWhenAppQuits = true;

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
        public Action OnSpawnerRegisteredEvent;

        protected override void Awake()
        {
            base.Awake();

            logger = Mst.Create.Logger(GetType().Name);
            logger.LogLevel = logLevel;

            Mst.Server.Spawners.DefaultPort = Mst.Args.RoomDefaultPort;
        }

        protected virtual void Start()
        {
            // Subscribe to connection event
            Mst.Connection.AddConnectionListener(OnConnectedToMasterEventHandler, true);
            // Subscribe to disconnection event
            Mst.Connection.AddDisconnectionListener(OnDisconnectedFromMasterEventHandler, true);
        }

        protected virtual void OnApplicationQuit()
        {
            if (killProcessesWhenAppQuits)
            {
                spawnerController?.KillProcesses();
            }
        }

        protected virtual void OnDestroy()
        {
            // Remove connection listener
            Mst.Connection.RemoveConnectionListener(OnConnectedToMasterEventHandler);
            // Remove disconnection listener
            Mst.Connection.RemoveDisconnectionListener(OnDisconnectedFromMasterEventHandler);
        }

        /// <summary>
        /// Fired when spawner connected to master
        /// </summary>
        protected virtual void OnConnectedToMasterEventHandler()
        {
            // If we want to start a spawner (cmd argument was found)
            if (Mst.Args.StartSpawner || (autoStartInEditor && Mst.Runtime.IsEditor))
            {
                // if you want to use your public IP address
                if (usePublicIp)
                {
                    Mst.Helper.GetPublicIp((ipInfo, error) =>
                    {
                        if (string.IsNullOrEmpty(ipInfo))
                        {
                            logger.Error(error);
                            logger.Error($"Our public IP is not defined. Let's use IP default IP address {machineIp}");
                        }
                        else
                        {
                            logger.Info($"Our public IP is {ipInfo}");
                            machineIp = ipInfo;
                            StartSpawner();
                        }
                    });
                }
                else
                {
                    StartSpawner();
                }
            }
        }

        /// <summary>
        /// Fired when spawner disconnected from master
        /// </summary>
        protected virtual void OnDisconnectedFromMasterEventHandler()
        {
            spawnerController?.KillProcesses();
        }

        /// <summary>
        /// Start spawner. But before start we are required to be connected
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

            // We do not want to use public IP
            if (!usePublicIp)
            {
                // If machine IP is defined in cmd
                machineIp = Mst.Args.AsString(Mst.Args.Names.RoomIp, machineIp);
            }

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

            logger.Info("Registering as a spawner with options: \n" + spawnerOptions);

            // 1. Register the spawner
            Mst.Server.Spawners.RegisterSpawner(spawnerOptions, (controller, error) =>
            {
                if (!string.IsNullOrEmpty(error))
                {
                    logger.Error($"Failed to create spawner: {error}");
                    return;
                }

                spawnerController = controller;
                spawnerController.Logger.LogLevel = spawnerLogLevel;

                spawnerController.SpawnSettings.UseWebSockets = Mst.Args.AsBool(Mst.Args.Names.UseWebSockets, spawnWebSocketServers);

                // 2. Set the default executable path
                spawnerController.SpawnSettings.ExecutablePath = Mst.Args.AsString(Mst.Args.Names.RoomExecutablePath, executableFilePath);

                // 3. Set the machine IP
                spawnerController.SpawnSettings.MachineIp = machineIp;

                // 4. Set region
                spawnerController.SpawnSettings.Region = spawnerOptions.Region;

                logger.Info("Spawner successfully created. Id: " + controller.SpawnerId);

                OnSpawnerRegistered();
            });
        }

        protected virtual void OnSpawnerRegistered() { }
    }
}