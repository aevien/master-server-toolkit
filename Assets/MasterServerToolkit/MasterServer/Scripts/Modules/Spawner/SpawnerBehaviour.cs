using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnerBehaviour : SingletonBehaviour<SpawnerBehaviour>
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
            Text = "It starts only when '-mstSpawnerStart=true' is configured, Editor auto-start is enabled, or StartSpawner() is called from code.",
            Type = HelpBoxType.Warning
        };

        [SerializeField, Tooltip("Minimum severity written by the process controller that starts, supervises and stops child executables.")]
        protected LogLevel spawnerLogLevel = LogLevel.Warn;

        [Header("Spawner Default Options")]
        [SerializeField, Tooltip("IP address advertised to rooms started by this spawner. It must be reachable by clients; the room-IP command-line argument overrides it.")]
        protected string machineIp = "127.0.0.1";

        [SerializeField, Tooltip("Default room/server executable launched by this spawner. The room-executable command-line argument overrides it outside the Editor.")]
        protected string executableFilePath = "";

        [SerializeField, Tooltip("Maximum number of child processes this spawner may own at once. 0 prevents new process starts. The spawner-max-processes command-line argument overrides it.")]
        protected int maxProcesses = 5;

        [SerializeField, Tooltip("Region advertised to the master for region-based spawn selection. An empty value is normalized to International; the room-region command-line argument overrides it.")]
        protected string region = "";

        [Header("Runtime Settings"), SerializeField, Tooltip("Terminates supervised child processes when this spawner stops. When disabled, already started processes are left running and remain outside MST supervision after shutdown.")]
        protected bool killProcessesWhenStop = true;

        [Header("Editor Settings"), SerializeField]
        private HelpBox hpEditor = new HelpBox()
        {
            Text = "These settings are used only while running in the Unity Editor for local testing.",
            Type = HelpBoxType.Warning
        };

        [Header("Running in Editor"), SerializeField, Tooltip("Automatically starts and registers the spawner after the Editor client connects to the master. Ignored in standalone builds.")]
        protected bool autoStartInEditor = true;

        [SerializeField, Tooltip("Uses Exe Path From Editor instead of the normal executable path while running in the Editor. Ignored in standalone builds.")]
        protected bool overrideExePathInEditor = true;

        [SerializeField, Tooltip("Room/server executable launched during Editor testing when Override Exe Path In Editor is enabled. Use an absolute path to the built executable.")]
        protected string exePathFromEditor = "C:/Please set your own path";

        #endregion

        /// <summary>
        /// Current spawner controller assigned to this behaviour
        /// </summary>
        protected ISpawnerController spawnerController;
        private int registrationGeneration;
        private volatile bool isDestroyed;

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
        [Tooltip("Invoked after this spawner has successfully registered with the master and can accept spawn work.")]
        public UnityEvent OnSpawnerStartedEvent;

        /// <summary>
        /// Invokes when this spawner stopped
        /// </summary>
        [Tooltip("Invoked when this spawner stops or loses its registered controller. Child-process behavior depends on Kill Processes When Stop.")]
        public UnityEvent OnSpawnerStoppedEvent;

        protected override void Awake()
        {
            base.Awake();

            Mst.Server.Spawners.DefaultPort = Mst.Args.SpawnerRoomDefaultPort;
            Mst.Server.Spawners.DefaultRedirectPort = Mst.Args.SpawnerRoomDefaultRedirectPort;

            // Subscribe to connection event
            Mst.Connection.AddConnectionOpenListener(OnConnectedToMasterEventHandler);
            // Subscribe to disconnection event
            Mst.Connection.AddConnectionCloseListener(OnDisconnectedFromMasterEventHandler, false);
        }

        private void OnValidate()
        {
            region = !string.IsNullOrEmpty(region) ? region : "International";
        }

        protected override void OnDestroy()
        {
            isDestroyed = true;
            Interlocked.Increment(ref registrationGeneration);
            ISpawnerController controllerToStop = spawnerController;
            spawnerController = null;
            IsSpawnerStarted = false;

            StopController(controllerToStop);

            // Remove connection listener
            Mst.Connection.RemoveConnectionOpenListener(OnConnectedToMasterEventHandler);
            // Remove disconnection listener
            Mst.Connection.RemoveConnectionCloseListener(OnDisconnectedFromMasterEventHandler);

            base.OnDestroy();
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
            if (isDestroyed)
                return;

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
            int generation = Interlocked.Increment(ref registrationGeneration);

            // Create spawner options
            var spawnerOptions = new SpawnerOptions
            {
                // If MaxProcesses count defined in cmd args
                MaxProcesses = Mst.Args.AsInt(Mst.Args.Names.SpawnerMaxProcesses, maxProcesses),
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

            logger.Info($"Registering as a spawner with options: {spawnerOptions}");

            // 1. Register the spawner
            Mst.Server.Spawners.RegisterSpawner(spawnerOptions, (controller, error) =>
            {
                if (isDestroyed ||
                    generation != Volatile.Read(ref registrationGeneration) ||
                    !IsSpawnerStarted)
                {
                    StopController(controller);
                    return;
                }

                if (!string.IsNullOrEmpty(error))
                {
                    IsSpawnerStarted = false;
                    logger.Error("Failed to create spawner");
                    return;
                }

                // 2. Save spawner controller
                spawnerController = controller;

                // 3. Set its log level
                spawnerController.Logger.LogLevel = spawnerLogLevel;

                // 4. Set the executable path
                spawnerController.SpawnSettings.ExecutablePath = executableFilePath;

                // 5. Set the machine IP
                spawnerController.SpawnSettings.MachineIp = machineIp;

                // 6. Set region
                spawnerController.SpawnSettings.MachineRegion = spawnerOptions.Region;

                logger.Info($"Spawner successfully created. Id: {controller.SpawnerId}");

                // 7. Inform listeners
                OnSpawnerStartedEvent?.Invoke();
                OnSpawnerStarted();
            });
        }

        /// <summary>
        /// Stops spawner processes
        /// </summary>
        public virtual void StopSpawner()
        {
            Interlocked.Increment(ref registrationGeneration);
            ISpawnerController controllerToStop = spawnerController;
            spawnerController = null;
            IsSpawnerStarted = false;

            if (controllerToStop != null)
            {
                logger.Info($"Spawner stopped. Id: {controllerToStop.SpawnerId}");
                StopController(controllerToStop);
            }

            OnSpawnerStoppedEvent?.Invoke();
        }

        private void StopController(ISpawnerController controller)
        {
            if (controller == null)
                return;

            if (killProcessesWhenStop)
                controller.KillProcesses();

            if (controller.Connection == null || !controller.Connection.IsConnected)
            {
                controller.Dispose();
                return;
            }

            try
            {
                controller.RequestUnregister((isSuccessful, error) =>
                {
                    if (!isSuccessful)
                        logger.Warn($"Failed to unregister spawner [{controller.SpawnerId}]");

                    controller.Dispose();
                });
            }
            catch (System.Exception exception)
            {
                logger.Error($"Failed to request unregister for spawner [{controller.SpawnerId}]");
                logger.Error(exception);
                controller.Dispose();
            }
        }

        /// <summary>
        /// Invokes when spawner registered and started
        /// </summary>
        protected virtual void OnSpawnerStarted() { }
    }
}
