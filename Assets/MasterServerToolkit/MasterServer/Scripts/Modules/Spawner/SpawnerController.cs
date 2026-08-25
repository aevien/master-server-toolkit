using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnerController : ISpawnerController
    {
        private const int ProcessExitWaitTimeoutMilliseconds = 5000;

        private sealed class SpawnOperation
        {
            private int callbackInvoked;

            public SpawnOperation(int spawnId, SuccessCallback callback)
            {
                SpawnId = spawnId;
                Callback = callback;
            }

            public int SpawnId { get; }
            public SuccessCallback Callback { get; }
            public int? RoomPort { get; set; }
            public int? RoomRedirectPort { get; set; }
            public Process Process { get; set; }
            public Thread WorkerThread { get; set; }
            public bool KillRequested { get; set; }
            public bool ProcessWasStarted { get; set; }
            public bool ProcessKilledNotificationEmitted { get; set; }

            public void CompleteCallback(bool isSuccessful, string error, Logger logger)
            {
                if (Interlocked.Exchange(ref callbackInvoked, 1) != 0)
                    return;

                try
                {
                    Callback?.Invoke(isSuccessful, error);
                }
                catch (Exception exception)
                {
                    logger.Error($"Spawn callback for operation [{SpawnId}] failed");
                    logger.Error(exception);
                }
            }
        }

        private readonly object processLock = new object();
        private readonly Dictionary<int, SpawnOperation> spawnOperations = new Dictionary<int, SpawnOperation>();
        private readonly SpawnersServer spawnersServer;
        private bool disposed;
        private bool killProcessesRequested;

        /// <summary>
        /// Current connection
        /// </summary>
        public IClientSocket Connection { get; protected set; }

        /// <summary>
        /// Id of this spawner controller that master server gives
        /// </summary>
        public int SpawnerId { get; protected set; }

        /// <summary>
        /// Settings, which are used by the default spawn handler
        /// </summary>
        public SpawnerConfig SpawnSettings { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        public Logger Logger { get; protected set; }

        /// <summary>
        /// Fired when process is started
        /// </summary>
        public event Action OnProcessStartedEvent;

        /// <summary>
        /// Fired when process is killed
        /// </summary>
        public event Action OnProcessKilledEvent;

        /// <summary>
        /// Create new instance of spawner controller
        /// </summary>
        /// <param name="spawnerId"></param>
        /// <param name="connection"></param>
        public SpawnerController(int spawnerId, IClientSocket connection, SpawnerOptions spawnerOptions)
            : this(spawnerId, connection, spawnerOptions, null)
        {
        }

        internal SpawnerController(int spawnerId, IClientSocket connection, SpawnerOptions spawnerOptions,
            SpawnersServer spawnersServer)
        {
            Logger = Mst.Create.Logger(typeof(SpawnerController).Name, LogLevel.All);
            this.spawnersServer = spawnersServer;

            Connection = connection;
            SpawnerId = spawnerId;

            SpawnSettings = new SpawnerConfig()
            {
                MasterIp = connection.Address,
                MasterPort = connection.Port,
                MachineIp = spawnerOptions.MachineIp,
                MachineRegion = string.IsNullOrEmpty(spawnerOptions.Region) ? "International" : spawnerOptions.Region
            };

        }

        /// <summary>
        /// Notifies all listeners that process is started
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="processId"></param>
        /// <param name="cmdArgs"></param>
        public void NotifyProcessStarted(int spawnId, int processId, string cmdArgs)
        {
            GetSpawnersServer().NotifyProcessStarted(spawnId, processId, cmdArgs, Connection);

            OnProcessStartedEvent?.Invoke();
        }

        /// <summary>
        /// Notifies all listeners that process is killed
        /// </summary>
        /// <param name="spawnId"></param>
        public void NotifyProcessKilled(int spawnId)
        {
            GetSpawnersServer().NotifyProcessKilled(spawnId, Connection);
            OnProcessKilledEvent?.Invoke();
        }

        /// <summary>
        /// Notifies master server, how many processes are running on a specified spawner
        /// </summary>
        /// <param name="count"></param>
        public void UpdateProcessesCount(int count)
        {
            GetSpawnersServer().UpdateProcessesCount(SpawnerId, count, Connection);
        }

        /// <summary>
        /// Requests removal of this spawner registration from the master server.
        /// </summary>
        /// <param name="callback">Receives the master-server result.</param>
        public void RequestUnregister(SuccessCallback callback = null)
        {
            GetSpawnersServer().UnregisterSpawner(this, callback);
        }

        /// <summary>
        /// Default spawn spawned process request handler that will be used by controller if <see cref="spawnRequestHandler"/> is not overriden
        /// </summary>
        /// <param name="data"></param>
        /// <param name="message"></param>
        public virtual void SpawnRequestHandler(SpawnRequestPacket data, SuccessCallback callback)
        {
            var operation = new SpawnOperation(data?.SpawnTaskId ?? -1, callback);
            bool operationReserved = false;

            try
            {
                if (data == null)
                    throw new ArgumentNullException(nameof(data));

                if (!TryReserveOperation(operation, out Exception reservationException))
                    throw reservationException;

                operationReserved = true;

                Logger.Info($"Spawn handler started handling a request to spawn process for spawn controller [{SpawnerId}]");

                // Create process args string
                var processArguments = data.Options.EscapeValues();

                // Check if we're overriding an IP to master server
                var masterIpArgument = string.IsNullOrEmpty(SpawnSettings.MasterIp) ?
                    Connection.Address : SpawnSettings.MasterIp;

                // Create master IP arg
                processArguments.Set(Mst.Args.Names.MasterIp, masterIpArgument);

                /// Check if we're overriding a port to master server
                var masterPortArgument = SpawnSettings.MasterPort < 0 ? Connection.Port : SpawnSettings.MasterPort;

                // Create master port arg
                processArguments.Set(Mst.Args.Names.MasterPort, masterPortArgument);

                // If rooms module start room with port arg itself
                if (!processArguments.Has(Mst.Args.Names.RoomIp))
                    processArguments.Set(Mst.Args.Names.RoomIp, SpawnSettings.MachineIp);

                if (!processArguments.Has(Mst.Args.Names.RoomPort))
                {
                    operation.RoomPort = GetSpawnersServer().GetAvailablePort();
                    processArguments.Set(Mst.Args.Names.RoomPort, operation.RoomPort.Value);
                }

                if (!processArguments.Has(Mst.Args.Names.RoomRedirectPort) && GetSpawnersServer().IsRedirectPortAllocationEnabled)
                {
                    operation.RoomRedirectPort = GetSpawnersServer().GetAvailableRedirectPort();
                    processArguments.Set(Mst.Args.Names.RoomRedirectPort, operation.RoomRedirectPort.Value);
                }

                // Create spawn id arg
                processArguments.Set(Mst.Args.Names.SpawnerTaskId, data.SpawnTaskId);

                // Create spawn code arg
                processArguments.Set(Mst.Args.Names.SpawnerTaskUniqueCode, data.SpawnTaskUniqueCode);

                if (!File.Exists(SpawnSettings.ExecutablePath))
                    throw new FileNotFoundException($"Room executable not found at {SpawnSettings.ExecutablePath}");

                string redactedProcessArguments = Mst.Args.ToRedactedString(processArguments);
                Logger.Info($"Starting process with args: {redactedProcessArguments}");

                // Create info about starting process
                var startProcessInfo = new ProcessStartInfo(SpawnSettings.ExecutablePath)
                {
                    CreateNoWindow = false,
                    UseShellExecute = true,
                    Arguments = processArguments.ToReadableString(" ", " ")
                };

                var workerThread = CreateWorkerThread(() => RunSpawnOperation(operation, startProcessInfo, redactedProcessArguments));

                if (!TryStartReservedOperation(operation, workerThread, out Exception startException))
                {
                    TryRemoveOperation(operation);
                    TryReleaseAllocatedRoomPorts(operation);
                    operation.CompleteCallback(false, MstErrorCodes.SPAWN_PROCESS_START_FAILED, Logger);
                    Logger.Error(startException);
                }
            }
            catch (Exception e)
            {
                if (operationReserved)
                    TryRemoveOperation(operation);

                TryReleaseAllocatedRoomPorts(operation);
                operation.CompleteCallback(false, MstErrorCodes.SPAWN_PROCESS_START_FAILED, Logger);
                Logger.Error(e);
            }
        }

        protected virtual Thread CreateWorkerThread(ThreadStart worker)
        {
            return new Thread(worker)
            {
                IsBackground = true,
                Name = $"MST Spawner {SpawnerId} Worker"
            };
        }

        protected virtual Process StartProcess(ProcessStartInfo startProcessInfo)
        {
            return Process.Start(startProcessInfo);
        }

        private bool TryReserveOperation(SpawnOperation operation, out Exception error)
        {
            lock (processLock)
            {
                if (disposed)
                {
                    error = new ObjectDisposedException(nameof(SpawnerController));
                    return false;
                }

                if (killProcessesRequested)
                {
                    error = new InvalidOperationException("Spawner controller is stopping");
                    return false;
                }

                if (spawnOperations.ContainsKey(operation.SpawnId))
                {
                    error = new InvalidOperationException($"Spawn operation [{operation.SpawnId}] is already registered");
                    return false;
                }

                spawnOperations.Add(operation.SpawnId, operation);
                error = null;
                return true;
            }
        }

        private bool TryStartReservedOperation(SpawnOperation operation, Thread workerThread, out Exception error)
        {
            lock (processLock)
            {
                if (!spawnOperations.TryGetValue(operation.SpawnId, out SpawnOperation reservedOperation) ||
                    !ReferenceEquals(reservedOperation, operation))
                {
                    error = new InvalidOperationException($"Spawn operation [{operation.SpawnId}] is no longer reserved");
                    return false;
                }

                if (disposed || killProcessesRequested || operation.KillRequested)
                {
                    error = new OperationCanceledException($"Spawn operation [{operation.SpawnId}] was canceled before launch");
                    return false;
                }

                try
                {
                    operation.WorkerThread = workerThread;
                    workerThread.Start();
                    error = null;
                    return true;
                }
                catch (Exception exception)
                {
                    operation.WorkerThread = null;
                    error = exception;
                    return false;
                }
            }
        }

        private void TryRemoveOperation(SpawnOperation operation)
        {
            lock (processLock)
            {
                if (spawnOperations.TryGetValue(operation.SpawnId, out SpawnOperation currentOperation) &&
                    ReferenceEquals(currentOperation, operation))
                {
                    spawnOperations.Remove(operation.SpawnId);
                }
            }
        }

        private void RunSpawnOperation(SpawnOperation operation, ProcessStartInfo startProcessInfo, string redactedProcessArguments)
        {
            Process process = null;

            try
            {
                bool cancelBeforeLaunch;

                lock (processLock)
                    cancelBeforeLaunch = operation.KillRequested;

                if (cancelBeforeLaunch)
                {
                    operation.CompleteCallback(false, MstErrorCodes.SPAWN_PROCESS_START_FAILED, Logger);
                    return;
                }

                Logger.Info($"Starting room process [{startProcessInfo.FileName}]");
                process = StartProcess(startProcessInfo)
                    ?? throw new InvalidOperationException($"Process.Start returned null for [{startProcessInfo.FileName}]");

                bool killImmediately;

                lock (processLock)
                {
                    operation.Process = process;
                    operation.ProcessWasStarted = true;
                    killImmediately = operation.KillRequested;
                }

                Logger.Info($"Process [{startProcessInfo.FileName}] started. Spawn Id: {operation.SpawnId}, pid: {process.Id}");

                if (killImmediately)
                {
                    operation.CompleteCallback(false, MstErrorCodes.SPAWN_PROCESS_START_FAILED, Logger);
                    TryKillProcess(process, operation.SpawnId, false);
                    process.WaitForExit();
                    return;
                }

                operation.CompleteCallback(true, string.Empty, Logger);
                TryNotifyProcessStarted(operation, process.Id, redactedProcessArguments);

                lock (processLock)
                    killImmediately = operation.KillRequested;

                if (killImmediately)
                    TryKillProcess(process, operation.SpawnId, false);

                process.WaitForExit();
            }
            catch (Exception exception)
            {
                operation.CompleteCallback(false, MstErrorCodes.SPAWN_PROCESS_START_FAILED, Logger);

                Logger.Error("An exception was thrown while starting or supervising a process. Make sure that you have set a correct build path. " +
                             $"We've tried to start a process at [{startProcessInfo.FileName}]. You can change it at 'SpawnerBehaviour' component or in application.cfg");
                Logger.Error(exception);

                if (process != null)
                    TryKillProcess(process, operation.SpawnId, true);
            }
            finally
            {
                bool notifyProcessKilled;

                lock (processLock)
                {
                    operation.Process = null;
                    operation.WorkerThread = null;
                    notifyProcessKilled = (operation.ProcessWasStarted || operation.KillRequested) &&
                                           !operation.ProcessKilledNotificationEmitted;

                    if (notifyProcessKilled)
                        operation.ProcessKilledNotificationEmitted = true;

                    if (spawnOperations.TryGetValue(operation.SpawnId, out SpawnOperation currentOperation) &&
                        ReferenceEquals(currentOperation, operation))
                    {
                        spawnOperations.Remove(operation.SpawnId);
                    }
                }

                TryReleaseAllocatedRoomPorts(operation);

                if (notifyProcessKilled)
                    TryNotifyProcessKilled(operation.SpawnId);

                try
                {
                    process?.Dispose();
                }
                catch (Exception exception)
                {
                    Logger.Error($"Failed to dispose process for spawn operation [{operation.SpawnId}]");
                    Logger.Error(exception);
                }
            }
        }

        private void TryNotifyProcessStarted(SpawnOperation operation, int processId, string redactedProcessArguments)
        {
            try
            {
                NotifyProcessStarted(operation.SpawnId, processId, redactedProcessArguments);
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to notify that process [{operation.SpawnId}] started");
                Logger.Error(exception);
            }
        }

        private void TryNotifyProcessKilled(int spawnId)
        {
            try
            {
                Logger.Info($"Notifying about killed process with spawn id [{spawnId}]");
                NotifyProcessKilled(spawnId);
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to notify that process [{spawnId}] was killed");
                Logger.Error(exception);
            }
        }

        private void TryReleaseAllocatedRoomPorts(SpawnOperation operation)
        {
            int? roomPort;
            int? roomRedirectPort;

            lock (processLock)
            {
                roomPort = operation.RoomPort;
                roomRedirectPort = operation.RoomRedirectPort;
                operation.RoomPort = null;
                operation.RoomRedirectPort = null;
            }

            try
            {
                ReleaseAllocatedRoomPorts(roomPort, roomRedirectPort);
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to release allocated ports for spawn operation [{operation.SpawnId}]");
                Logger.Error(exception);
            }
        }

        private void ReleaseAllocatedRoomPorts(int? roomPort, int? roomRedirectPort)
        {
            if (roomPort.HasValue)
                GetSpawnersServer().ReleasePort(roomPort.Value);

            if (roomRedirectPort.HasValue)
                GetSpawnersServer().ReleaseRedirectPort(roomRedirectPort.Value);
        }

        /// <summary>
        /// Default kill spawned process request handler that will be used by controller if <see cref="killRequestHandler"/> is not overriden
        /// </summary>
        /// <param name="spawnId"></param>
        public virtual ResponseStatus KillRequestHandler(int spawnId)
        {
            Logger.Info($"Kill request handler started handling a request to kill a process with id [{spawnId}] for spawn controller with id [{SpawnerId}]");

            try
            {
                Process process = null;
                bool operationFound;

                lock (processLock)
                {
                    operationFound = spawnOperations.TryGetValue(spawnId, out SpawnOperation operation);

                    if (operationFound)
                    {
                        operation.KillRequested = true;
                        process = operation.Process;
                    }
                }

                if (!operationFound)
                    return ResponseStatus.NotFound;

                return process == null || TryKillProcess(process, spawnId, false)
                    ? ResponseStatus.Success
                    : ResponseStatus.Error;
            }
            catch (Exception e)
            {
                Logger.Error($"Got error while killing a spawned process with id [{spawnId}]");
                Logger.Error(e);
                return ResponseStatus.Error;
            }
        }

        /// <summary>
        /// Kill all processes running in this controller
        /// </summary>
        public void KillProcesses()
        {
            var list = new List<Process>();

            lock (processLock)
            {
                killProcessesRequested = true;

                foreach (SpawnOperation operation in spawnOperations.Values)
                {
                    operation.KillRequested = true;

                    if (operation.Process != null)
                        list.Add(operation.Process);
                }
            }

            foreach (var process in list)
                TryKillProcess(process, null, false);
        }

        /// <summary>
        /// Get the number of processes
        /// </summary>
        /// <returns></returns>
        public int ProcessesCount()
        {
            lock (processLock)
            {
                int count = 0;

                foreach (SpawnOperation operation in spawnOperations.Values)
                {
                    if (operation.Process != null)
                        count++;
                }

                return count;
            }
        }

        public void Dispose()
        {
            List<SpawnOperation> operationsToDrain = null;

            lock (processLock)
            {
                if (disposed)
                    return;

                disposed = true;

                if (killProcessesRequested)
                {
                    operationsToDrain = new List<SpawnOperation>(spawnOperations.Values);

                    foreach (SpawnOperation operation in operationsToDrain)
                        operation.KillRequested = true;
                }
            }

            try
            {
                spawnersServer?.UnregisterSpawnerController(this);
            }
            catch (Exception exception)
            {
                Logger.Error($"Failed to unregister spawn controller [{SpawnerId}] from its owner");
                Logger.Error(exception);
            }

            if (operationsToDrain == null)
                return;

            foreach (SpawnOperation operation in operationsToDrain)
            {
                try
                {
                    Process process;

                    lock (processLock)
                        process = operation.Process;

                    if (process != null)
                        TryKillProcess(process, operation.SpawnId, false);
                }
                catch (Exception exception)
                {
                    Logger.Error($"Failed while stopping spawn operation [{operation.SpawnId}]");
                    Logger.Error(exception);
                }
            }

            var shutdownWait = Stopwatch.StartNew();

            foreach (SpawnOperation operation in operationsToDrain)
            {
                Thread workerThread;

                lock (processLock)
                    workerThread = operation.WorkerThread;

                if (workerThread == null || ReferenceEquals(workerThread, Thread.CurrentThread))
                    continue;

                try
                {
                    int remainingWaitMilliseconds = Math.Max(0,
                        ProcessExitWaitTimeoutMilliseconds - (int)shutdownWait.ElapsedMilliseconds);

                    if (remainingWaitMilliseconds == 0 || !workerThread.Join(remainingWaitMilliseconds))
                        Logger.Warn($"Timed out while waiting for spawn operation [{operation.SpawnId}] to stop");
                }
                catch (Exception exception)
                {
                    Logger.Error($"Failed while waiting for spawn operation [{operation.SpawnId}] to stop");
                    Logger.Error(exception);
                }
            }
        }

        private bool TryKillProcess(Process process, int? spawnId, bool waitForExit)
        {
            if (process == null)
                return false;

            try
            {
                if (!process.HasExited)
                    process.Kill();

                if (waitForExit && !process.WaitForExit(ProcessExitWaitTimeoutMilliseconds))
                {
                    string processDescription = spawnId.HasValue
                        ? $"spawn operation [{spawnId.Value}]"
                        : "spawned process";
                    Logger.Warn($"Timed out while waiting for {processDescription} to exit");
                    return false;
                }

                return true;
            }
            catch (InvalidOperationException)
            {
                // The process has already exited or was not associated with an OS process.
                return true;
            }
            catch (Exception exception)
            {
                string processDescription = spawnId.HasValue
                    ? $"spawn operation [{spawnId.Value}]"
                    : "spawned process";
                Logger.Error($"Failed to kill {processDescription}");
                Logger.Error(exception);
                return false;
            }
        }

        private SpawnersServer GetSpawnersServer()
        {
            return spawnersServer ?? Mst.Server.Spawners;
        }
    }
}
