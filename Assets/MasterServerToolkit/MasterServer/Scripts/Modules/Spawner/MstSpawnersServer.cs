using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public delegate void RegisterSpawnerCallback(ISpawnerController spawner, string error);
    public delegate void RegisterSpawnedProcessCallback(SpawnTaskController taskController, string error);

    public class MstSpawnersServer : MstBaseClient
    {
        /// <summary>
        /// Free ports
        /// </summary>
        private Queue<int> freePorts;

        /// <summary>
        /// Last taken port
        /// </summary>
        private int lastPortTaken = -1;

        /// <summary>
        /// Spawner controllers registered on one Spawner instance
        /// </summary>
        private Dictionary<int, ISpawnerController> createdSpawnerControllers;

        /// <summary>
        /// Default port that will be used to start
        /// </summary>
        public int DefaultPort { get; set; } = 1500;

        /// <summary>
        /// Started room can use this to check if it was spawned or started manually
        /// </summary>
        public bool IsSpawnedProccess { get; private set; }

        /// <summary>
        /// Invoked on "spawner server", when it successfully registers to master server
        /// </summary>
        public event Action<ISpawnerController> OnSpawnerRegisteredEvent;

        public MstSpawnersServer(IClientSocket connection) : base(connection)
        {
            createdSpawnerControllers = new Dictionary<int, ISpawnerController>();
            freePorts = new Queue<int>();

            IsSpawnedProccess = Mst.Args.IsProvided(Mst.Args.Names.SpawnTaskUniqueCode);
        }

        /// <summary>
        /// Sends a request to master server, to register an existing spawner with given options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="callback"></param>
        public void RegisterSpawner(SpawnerOptions options, RegisterSpawnerCallback callback)
        {
            RegisterSpawner(options, callback, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to register an existing spawner with given options
        /// </summary>
        public void RegisterSpawner(SpawnerOptions options, RegisterSpawnerCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback?.Invoke(null, "Not connected");
                return;
            }

            connection.SendMessage(MstOpCodes.RegisterSpawner, options, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, response.AsString("Unknown Error"));
                    return;
                }

                var spawnerId = response.AsInt();
                var controller = new SpawnerController(spawnerId, connection, options);

                // Save reference
                createdSpawnerControllers[spawnerId] = controller;

                callback?.Invoke(controller, null);

                // Invoke the event
                OnSpawnerRegisteredEvent?.Invoke(controller);
            });
        }

        /// <summary>
        /// This method should be called, when spawn process is finalized (finished spawning).
        /// For example, when spawned game server fully starts
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="callback"></param>
        public void FinalizeSpawnedProcess(int spawnId, SuccessCallback callback)
        {
            FinalizeSpawnedProcess(spawnId, new MstProperties(), callback, Connection);
        }

        /// <summary>
        /// This method should be called, when spawn process is finalized (finished spawning).
        /// For example, when spawned game server fully starts
        /// </summary>
        public void FinalizeSpawnedProcess(int spawnId, MstProperties finalizationData, SuccessCallback callback)
        {
            FinalizeSpawnedProcess(spawnId, finalizationData, callback, Connection);
        }

        /// <summary>
        /// This method should be called, when spawn process is finalized (finished spawning).
        /// For example, when spawned game server fully starts
        /// </summary>
        public void FinalizeSpawnedProcess(int spawnId, MstProperties finalizationData, SuccessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback?.Invoke(false, "Not connected");
                return;
            }

            var packet = new SpawnFinalizationPacket()
            {
                SpawnTaskId = spawnId,
                FinalizationData = finalizationData
            };

            connection.SendMessage(MstOpCodes.CompleteSpawnProcess, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(false, response.AsString("Unknown Error"));
                    return;
                }

                callback?.Invoke(true, null);
            });
        }

        /// <summary>
        /// This should be called from a process, which is spawned.
        /// For example, it can be called from a game server, which is started by the spawner
        /// On successfull registration, callback contains <see cref="SpawnTaskController"/>, which 
        /// has a dictionary of properties, that were given when requesting a process to be spawned
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="spawnCode"></param>
        /// <param name="callback"></param>
        public void RegisterSpawnedProcess(int spawnId, string spawnCode, RegisterSpawnedProcessCallback callback)
        {
            RegisterSpawnedProcess(spawnId, spawnCode, callback, Connection);
        }

        /// <summary>
        /// This should be called from a process which is spawned.
        /// For example, it can be called from a game server, which is started by the spawner
        /// On successfull registration, callback contains <see cref="SpawnTaskController"/>, which 
        /// has a dictionary of properties, that were given when requesting a process to be spawned
        /// </summary>
        public void RegisterSpawnedProcess(int spawnId, string spawnCode, RegisterSpawnedProcessCallback callback, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                callback?.Invoke(null, "Not connected");
                return;
            }

            var packet = new RegisterSpawnedProcessPacket()
            {
                SpawnCode = spawnCode,
                SpawnId = spawnId
            };

            connection.SendMessage(MstOpCodes.RegisterSpawnedProcess, packet, (status, response) =>
            {
                if (status != ResponseStatus.Success)
                {
                    callback?.Invoke(null, response.AsString("Unknown Error"));
                    return;
                }

                // Read spawn task options received from master server
                var options = MstProperties.FromBytes(response.AsBytes());

                // Create spawn task controller
                var process = new SpawnTaskController(spawnId, options, connection);

                callback?.Invoke(process, null);
            });
        }

        /// <summary>
        /// Notifies master server, how many processes are running on a specified spawner
        /// </summary>
        public void UpdateProcessesCount(int spawnerId, int count)
        {
            UpdateProcessesCount(spawnerId, count, Connection);
        }

        /// <summary>
        /// Notifies master server, how many processes are running on a specified spawner
        /// </summary>
        public void UpdateProcessesCount(int spawnerId, int count, IClientSocket connection)
        {
            var packet = new IntPairPacket()
            {
                A = spawnerId,
                B = count
            };

            connection.SendMessage(MstOpCodes.UpdateSpawnerProcessesCount, packet);
        }

        /// <summary>
        /// Should be called by a spawned process, as soon as it is started
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="processId"></param>
        /// <param name="cmdArgs"></param>
        public void NotifyProcessStarted(int spawnId, int processId, string cmdArgs)
        {
            NotifyProcessStarted(spawnId, processId, cmdArgs, Connection);
        }

        /// <summary>
        /// Should be called by a spawned process, as soon as it is started
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="processId"></param>
        /// <param name="cmdArgs"></param>
        /// <param name="connection"></param>
        public void NotifyProcessStarted(int spawnId, int processId, string cmdArgs, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                return;
            }

            connection.SendMessage(MstOpCodes.ProcessStarted, new SpawnedProcessStartedPacket()
            {
                CmdArgs = cmdArgs,
                ProcessId = processId,
                SpawnId = spawnId
            });
        }

        /// <summary>
        /// Should be called by a spawner controller when one of the processes killed
        /// </summary>
        /// <param name="spawnId"></param>
        public void NotifyProcessKilled(int spawnId)
        {
            NotifyProcessKilled(spawnId, Connection);
        }

        /// <summary>
        /// Should be called by a spawner controller when one of the processes killed
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="connection"></param>
        public void NotifyProcessKilled(int spawnId, IClientSocket connection)
        {
            if (!connection.IsConnected)
            {
                return;
            }

            connection.SendMessage(MstOpCodes.ProcessKilled, spawnId);
        }

        /// <summary>
        /// Get spawner controller by id
        /// </summary>
        /// <param name="spawnerId"></param>
        /// <returns></returns>
        public ISpawnerController GetSpawnerController(int spawnerId)
        {
            ISpawnerController controller;
            createdSpawnerControllers.TryGetValue(spawnerId, out controller);
            return controller;
        }

        /// <summary>
        /// Get list of spawner controllers
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ISpawnerController> GetCreatedSpawnerControllers()
        {
            return createdSpawnerControllers.Values;
        }

        /// <summary>
        /// Gets available port
        /// </summary>
        /// <returns></returns>
        public int GetAvailablePort()
        {
            // Return a port from a list of available ports
            if (freePorts.Count > 0)
            {
                return freePorts.Dequeue();
            }

            if (lastPortTaken < 0)
            {
                lastPortTaken = DefaultPort;
            }

            return lastPortTaken++;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="port"></param>
        public void ReleasePort(int port)
        {
            freePorts.Enqueue(port);
        }
    }
}