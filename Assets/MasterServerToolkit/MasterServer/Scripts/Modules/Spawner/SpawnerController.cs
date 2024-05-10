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
        /// <summary>
        /// Just <see cref="Process"/> lock
        /// </summary>
        protected static object processLock = new object();

        /// <summary>
        /// List of spawned processes
        /// </summary>
        protected Dictionary<int, Process> processes = new Dictionary<int, Process>();

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
        public MasterServerToolkit.Logging.Logger Logger { get; protected set; }

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
        /// <param name="options"></param>
        public SpawnerController(int spawnerId, IClientSocket connection, SpawnerOptions spawnerOptions)
        {
            Logger = Mst.Create.Logger(typeof(SpawnerController).Name, LogLevel.All);

            Connection = connection;
            SpawnerId = spawnerId;

            SpawnSettings = new SpawnerConfig()
            {
                MasterIp = connection.Address,
                MasterPort = connection.Port,
                MachineIp = spawnerOptions.MachineIp,
                Region = string.IsNullOrEmpty(spawnerOptions.Region) ? "International" : spawnerOptions.Region
            };

            // Add static handlers to listen one message for all controllers
            connection.RegisterMessageHandler(MstOpCodes.SpawnProcessRequest, SpawnProcessRequestHandler);
            connection.RegisterMessageHandler(MstOpCodes.KillProcessRequest, KillProcessRequestHandler);
        }

        /// <summary>
        /// Handles spawn request for all controllers filtered by ID
        /// </summary>
        /// <param name="message"></param>
        private static void SpawnProcessRequestHandler(IIncomingMessage message)
        {
            try
            {
                var data = message.AsPacket<SpawnRequestPacket>();
                ISpawnerController controller = Mst.Server.Spawners.GetSpawnerController(data.SpawnerId);

                if (controller == null)
                {
                    if (message.IsExpectingResponse)
                    {
                        message.Respond("Couldn't find a spawn controller", ResponseStatus.NotHandled);
                    }

                    return;
                }

                controller.Logger.Debug($"Spawn process requested for spawn controller [{controller.SpawnerId}]");
                controller.SpawnRequestHandler(data, message);
            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        /// <summary>
        /// Handles kill request for all controllers filtered by ID
        /// </summary>
        /// <param name="message"></param>
        private static void KillProcessRequestHandler(IIncomingMessage message)
        {
            var data = message.AsPacket<KillSpawnedProcessRequestPacket>();
            var controller = Mst.Server.Spawners.GetSpawnerController(data.SpawnerId) as SpawnerController;

            if (controller == null)
            {
                if (message.IsExpectingResponse)
                {
                    message.Respond("Couldn't find a spawn controller", ResponseStatus.NotHandled);
                }

                return;
            }

            controller.Logger.Debug($"Kill process requested for spawn controller [{controller.SpawnerId}]");
            controller.KillRequestHandler(data.SpawnId);
            message.Respond(ResponseStatus.Success);
        }

        /// <summary>
        /// Notifies all listeners that process is started
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="processId"></param>
        /// <param name="cmdArgs"></param>
        public void NotifyProcessStarted(int spawnId, int processId, string cmdArgs)
        {
            Mst.Server.Spawners.NotifyProcessStarted(spawnId, processId, cmdArgs, Connection);
            OnProcessStartedEvent?.Invoke();
        }

        /// <summary>
        /// Notifies all listeners that process is killed
        /// </summary>
        /// <param name="spawnId"></param>
        public void NotifyProcessKilled(int spawnId)
        {
            Mst.Server.Spawners.NotifyProcessKilled(spawnId);
            OnProcessKilledEvent?.Invoke();
        }

        /// <summary>
        /// Notifies master server, how many processes are running on a specified spawner
        /// </summary>
        /// <param name="count"></param>
        public void UpdateProcessesCount(int count)
        {
            Mst.Server.Spawners.UpdateProcessesCount(SpawnerId, count, Connection);
        }

        /// <summary>
        /// Default spawn spawned process request handler that will be used by controller if <see cref="spawnRequestHandler"/> is not overriden
        /// </summary>
        /// <param name="data"></param>
        /// <param name="message"></param>
        public virtual void SpawnRequestHandler(SpawnRequestPacket data, IIncomingMessage message)
        {
            Logger.Info($"Spawn handler started handling a request to spawn process for spawn controller [{SpawnerId}]");

            /************************************************************************/
            // Create process args string
            var processArguments = data.Options.EscapeValues();

            /************************************************************************/
            // Check if we're overriding an IP to master server
            var masterIpArgument = string.IsNullOrEmpty(SpawnSettings.MasterIp) ?
                Connection.Address : SpawnSettings.MasterIp;

            // Create master IP arg
            processArguments.Set(Mst.Args.Names.MasterIp, masterIpArgument);

            /************************************************************************/
            /// Check if we're overriding a port to master server
            var masterPortArgument = SpawnSettings.MasterPort < 0 ? Connection.Port : SpawnSettings.MasterPort;

            // Create master port arg
            processArguments.Set(Mst.Args.Names.MasterPort, masterPortArgument);

            /************************************************************************/
            // Machine Ip
            processArguments.Set(Mst.Args.Names.RoomIp, SpawnSettings.MachineIp);

            /************************************************************************/
            // Create port for room arg
            int machinePortArgument = Mst.Server.Spawners.GetAvailablePort();
            processArguments.Set(Mst.Args.Names.RoomPort, machinePortArgument);

            /************************************************************************/
            // Create spawn id arg
            processArguments.Set(Mst.Args.Names.SpawnTaskId, data.SpawnTaskId);

            /************************************************************************/
            // Create spawn code arg
            processArguments.Set(Mst.Args.Names.SpawnTaskUniqueCode, data.SpawnTaskUniqueCode);

            /************************************************************************/
            // Path to executable
            var executablePath = data.UseOverrideExePath ? data.OverrideExePath : SpawnSettings.ExecutablePath;

            if (!File.Exists(executablePath))
                throw new FileNotFoundException($"Room executable not found at {executablePath}");

            /// Create info about starting process
            var startProcessInfo = new ProcessStartInfo(executablePath)
            {
                CreateNoWindow = false,
                UseShellExecute = true,
                Arguments = processArguments.ToReadableString(" ", " ")
            };

            Logger.Info($"Starting process with args: {processArguments}");

            var processStarted = false;

            try
            {
                new Thread(() =>
                {
                    try
                    {
                        Logs.Info($"Starting room process [{startProcessInfo.FileName}]");

                        using (var process = Process.Start(startProcessInfo))
                        {
                            Logger.Info($"Process [{startProcessInfo.FileName}] started. Spawn Id: {data.SpawnTaskId}, pid: {process.Id}");
                            processStarted = true;

                            lock (processLock)
                            {
                                // Save the process
                                processes[data.SpawnTaskId] = process;
                            }

                            var processId = process.Id;

                            // Notify server that we've successfully handled the request
                            message.Respond(ResponseStatus.Success);
                            NotifyProcessStarted(data.SpawnTaskId, processId, startProcessInfo.Arguments);

                            process.WaitForExit();
                        }
                    }
                    catch (Exception e)
                    {
                        if (!processStarted)
                        {
                            message.Respond(ResponseStatus.Failed);
                        }

                        Logger.Error("An exception was thrown while starting a process. Make sure that you have set a correct build path. " +
                                     $"We've tried to start a process at [{startProcessInfo.FileName}]. You can change it at 'SpawnerBehaviour' component or in application.cfg");
                        Logger.Error(e);
                    }
                    finally
                    {
                        // Remove the process
                        lock (processLock)
                            processes.Remove(data.SpawnTaskId);

                        // Release the port number
                        Mst.Server.Spawners.ReleasePort(machinePortArgument);
                        Logger.Info($"Notifying about killed process with spawn id [{data.SpawnTaskId}]");
                        NotifyProcessKilled(data.SpawnTaskId);
                    }

                }).Start();
            }
            catch (Exception e)
            {
                message.Respond(e.Message, ResponseStatus.Error);
                Logger.Error(e);
            }
        }

        /// <summary>
        /// Default kill spawned process request handler that will be used by controller if <see cref="killRequestHandler"/> is not overriden
        /// </summary>
        /// <param name="spawnId"></param>
        public virtual void KillRequestHandler(int spawnId)
        {
            Logger.Info($"Kill request handler started handling a request to kill a process with id [{spawnId}] for spawn controller with id [{SpawnerId}]");

            try
            {
                Process process;

                lock (processLock)
                {
                    processes.TryGetValue(spawnId, out process);
                    processes.Remove(spawnId);
                }

                if (process != null)
                {
                    process.Kill();
                }
            }
            catch (Exception e)
            {
                Logger.Error($"Got error while killing a spawned process with id [{spawnId}]");
                Logger.Error(e);
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
                foreach (var process in processes.Values)
                {
                    list.Add(process);
                }
            }

            foreach (var process in list)
            {
                process.Kill();
            }
        }

        /// <summary>
        /// Get the number of processes
        /// </summary>
        /// <returns></returns>
        public int ProcessesCount()
        {
            return processes != null ? processes.Count : 0;
        }
    }
}