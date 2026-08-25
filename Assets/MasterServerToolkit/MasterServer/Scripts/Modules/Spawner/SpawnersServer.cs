using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace MasterServerToolkit.MasterServer
{
    public delegate void RegisterSpawnerCallback(ISpawnerController spawner, string error);
    public delegate void RegisterSpawnedProcessCallback(SpawnTaskController taskController, string error);

    public class SpawnersServer : MstBaseClient
    {
        private readonly struct SpawnerControllerKey : IEquatable<SpawnerControllerKey>
        {
            public SpawnerControllerKey(IClientSocket connection, int spawnerId)
            {
                Connection = connection;
                SpawnerId = spawnerId;
            }

            public IClientSocket Connection { get; }
            public int SpawnerId { get; }

            public bool Equals(SpawnerControllerKey other)
            {
                return ReferenceEquals(Connection, other.Connection) && SpawnerId == other.SpawnerId;
            }

            public override bool Equals(object obj)
            {
                return obj is SpawnerControllerKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (RuntimeHelpers.GetHashCode(Connection) * 397) ^ SpawnerId;
                }
            }
        }

        private sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
        {
            public static readonly ReferenceComparer<T> Instance = new ReferenceComparer<T>();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }

        private sealed class ConnectionHandlerRegistration
        {
            public IClientSocket Connection { get; set; }
            public IPacketHandler SpawnHandler { get; set; }
            public IPacketHandler KillHandler { get; set; }
            public int OwnerCount { get; set; }
        }

        /// <summary>
        /// Free ports
        /// </summary>
        private readonly Queue<int> freePorts = new Queue<int>();

        /// <summary>
        /// Free redirected ports
        /// </summary>
        private readonly Queue<int> freeRedirectPorts = new Queue<int>();

        private readonly object portAllocationSync = new object();
        private readonly object redirectPortAllocationSync = new object();

        /// <summary>
        /// Last taken port
        /// </summary>
        private int lastPortTaken = -1;

        /// <summary>
        /// Last taken redirected port
        /// </summary>
        private int lastRedirectPortTaken = -1;

        /// <summary>
        /// Spawner controllers registered on one Spawner instance
        /// </summary>
        private readonly object controllerRegistrySync = new object();
        private readonly Dictionary<SpawnerControllerKey, ISpawnerController> createdSpawnerControllers;
        private readonly Dictionary<IClientSocket, ConnectionHandlerRegistration> customConnectionHandlers;

        /// <summary>
        /// Default port that will be used to start
        /// </summary>
        public int DefaultPort { get; set; } = 1500;

        /// <summary>
        /// Default redirected port that will be used when spawned rooms listen behind a reverse proxy. Negative value disables redirect port allocation.
        /// </summary>
        public int DefaultRedirectPort { get; set; } = -1;

        /// <summary>
        /// Returns true if the spawner should assign redirected room ports.
        /// </summary>
        public bool IsRedirectPortAllocationEnabled => DefaultRedirectPort > 0;

        /// <summary>
        /// Started room can use this to check if it was spawned or started manually
        /// </summary>
        public bool IsSpawnedProccess { get; private set; }

        /// <summary>
        /// Invoked on "spawner server", when it successfully registers to master server
        /// </summary>
        public event Action<ISpawnerController> OnSpawnerRegisteredEvent;

        public SpawnersServer(IClientSocket connection) : base(connection)
        {
            SpawnersClient.RegisterErrorParsers();
            createdSpawnerControllers = new Dictionary<SpawnerControllerKey, ISpawnerController>();
            customConnectionHandlers = new Dictionary<IClientSocket, ConnectionHandlerRegistration>(
                ReferenceComparer<IClientSocket>.Instance);

            IsSpawnedProccess = Mst.Args.IsProvided(Mst.Args.Names.SpawnerTaskUniqueCode);

            RegisterMessageHandler(MstOpCodes.SpawnProcessRequest, SpawnProcessRequestHandler);
            RegisterMessageHandler(MstOpCodes.KillProcessRequest, KillProcessRequestHandler);
        }

        private void SpawnProcessRequestHandler(IIncomingMessage message)
        {
            SpawnProcessRequestHandler(message, Connection);
        }

        private void SpawnProcessRequestHandler(IIncomingMessage message, IClientSocket sourceConnection)
        {
            try
            {
                var data = message.AsPacket<SpawnRequestPacket>();
                ISpawnerController controller = GetSpawnerController(data.SpawnerId, sourceConnection);

                if (controller == null)
                {
                    message.RespondError(ResponseStatus.NotFound,
                        MstErrorCodes.SPAWNER_CONTROLLER_NOT_FOUND);
                    return;
                }

                controller.Logger.Debug($"Spawn process requested for spawn controller [{controller.SpawnerId}]");
                controller.SpawnRequestHandler(data, (isSuccessful, error) =>
                {
                    if (isSuccessful)
                    {
                        message.Respond(ResponseStatus.Success);
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(error) &&
                            !string.Equals(error, MstErrorCodes.SPAWN_PROCESS_START_FAILED,
                                StringComparison.Ordinal))
                        {
                            controller.Logger.Error($"Spawn process request failed: {error}");
                        }

                        message.RespondError(ResponseStatus.Error, MstErrorCodes.SPAWN_PROCESS_START_FAILED);
                    }
                });
            }
            catch (Exception exception)
            {
                Logging.Logs.Error($"Spawn process request handler failed: {exception}",
                    Logging.LogChannels.System);
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        private void KillProcessRequestHandler(IIncomingMessage message)
        {
            KillProcessRequestHandler(message, Connection);
        }

        private void KillProcessRequestHandler(IIncomingMessage message, IClientSocket sourceConnection)
        {
            try
            {
                var data = message.AsPacket<KillSpawnedProcessRequestPacket>();
                ISpawnerController controller = GetSpawnerController(data.SpawnerId, sourceConnection);

                if (controller == null)
                {
                    message.RespondError(ResponseStatus.NotFound,
                        MstErrorCodes.SPAWNER_CONTROLLER_NOT_FOUND);
                    return;
                }

                controller.Logger.Debug($"Kill process requested for spawn controller [{controller.SpawnerId}]");
                ResponseStatus result = controller.KillRequestHandler(data.SpawnId);

                if (result == ResponseStatus.Success)
                    message.Respond(ResponseStatus.Success);
                else if (result == ResponseStatus.NotFound)
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.SPAWN_PROCESS_NOT_FOUND);
                else
                    message.RespondError(result, MstErrorCodes.SPAWN_PROCESS_KILL_FAILED);
            }
            catch (Exception exception)
            {
                Logging.Logs.Error($"Kill process request handler failed: {exception}",
                    Logging.LogChannels.System);
                message.RespondError(ResponseStatus.Error, MstErrorCodes.INTERNAL_ERROR);
            }
        }

        /// <summary>
        /// Sends a request to master server, to register an existing spawner with given options
        /// </summary>
        /// <param name="options"></param>
        /// <param name="callback"></param>
        public void RegisterSpawner(SpawnerOptions options, RegisterSpawnerCallback callback = null)
        {
            RegisterSpawner(options, callback, Connection);
        }

        /// <summary>
        /// Sends a request to master server, to register an existing spawner with given options
        /// </summary>
        public void RegisterSpawner(SpawnerOptions options, RegisterSpawnerCallback callback, IClientSocket connection)
        {
            int completionState = 0;

            void Complete(ISpawnerController controller, string error)
            {
                if (Interlocked.Exchange(ref completionState, 1) != 0)
                    return;

                try
                {
                    callback?.Invoke(controller, error);
                }
                catch (Exception exception)
                {
                    Logging.Logs.Error($"Spawner registration callback failed: {exception}",
                        Logging.LogChannels.System);
                }
            }

            if (connection == null)
            {
                Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (!connection.IsConnected)
            {
                Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            Mst.Security.RequestPermission(MstPermissionKeys.Spawner, (isSuccessful, error) =>
            {
                if (!isSuccessful)
                {
                    Logging.Logs.Error(
                        $"Failed to obtain '{MstPermissionKeys.Spawner}' permission",
                        Logging.LogChannels.System);
                    Complete(null, Mst.Errors.Parse(ResponseStatus.Unauthorized));
                    return;
                }

                if (!connection.IsConnected)
                {
                    Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                    return;
                }

                try
                {
                    connection.SendMessage(MstOpCodes.RegisterSpawner, options, (status, response) =>
                    {
                        try
                        {
                            if (status != ResponseStatus.Success)
                            {
                                Complete(null, Mst.Errors.Parse(status, response));
                                return;
                            }

                            if (response == null)
                            {
                                Complete(null, Mst.Errors.Parse(ResponseStatus.Invalid));
                                return;
                            }

                            var spawnerId = response.AsInt();
                            var controller = new SpawnerController(spawnerId, connection, options, this);

                            try
                            {
                                RegisterSpawnerController(controller);
                            }
                            catch (Exception exception)
                            {
                                controller.Dispose();
                                Logging.Logs.Error($"Failed to register local spawner controller: {exception}",
                                    Logging.LogChannels.System);
                                Complete(null, Mst.Errors.Parse(ResponseStatus.Error));
                                return;
                            }

                            Complete(controller, null);

                            // Invoke the event
                            OnSpawnerRegisteredEvent?.Invoke(controller);
                        }
                        catch (Exception exception)
                        {
                            if (Volatile.Read(ref completionState) != 0)
                            {
                                Logging.Logs.Error($"Spawner registered event subscriber failed: {exception}",
                                    Logging.LogChannels.System);
                            }
                            else
                            {
                                Logging.Logs.Error($"Spawner registration response processing failed: {exception}",
                                    Logging.LogChannels.System);
                                Complete(null, Mst.Errors.Parse(ResponseStatus.Error));
                            }
                        }
                    });
                }
                catch (Exception exception)
                {
                    Logging.Logs.Error($"Spawner registration request failed: {exception}",
                        Logging.LogChannels.System);
                    Complete(null, Mst.Errors.Parse(ResponseStatus.Error));
                }
            }, connection);
        }

        /// <summary>
        /// Requests removal of a registered spawner from the master server.
        /// </summary>
        /// <param name="controller">Controller whose registration should be removed.</param>
        /// <param name="callback">Receives whether the master server accepted the request.</param>
        public void UnregisterSpawner(ISpawnerController controller, SuccessCallback callback = null)
        {
            if (controller == null)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.Invalid));
                return;
            }

            IClientSocket connection = controller.Connection;

            if (connection == null || !connection.IsConnected)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            bool isRegistered;

            lock (controllerRegistrySync)
            {
                var key = new SpawnerControllerKey(connection, controller.SpawnerId);
                isRegistered = createdSpawnerControllers.TryGetValue(key, out ISpawnerController current) &&
                    ReferenceEquals(current, controller);
            }

            if (!isRegistered)
            {
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotFound));
                return;
            }

            try
            {
                connection.SendMessage(MstOpCodes.UnregisterSpawner, controller.SpawnerId, (status, response) =>
                {
                    if (status == ResponseStatus.Success)
                    {
                        callback?.Invoke(true, null);
                        return;
                    }

                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
                });
            }
            catch (Exception exception)
            {
                Logging.Logs.Error($"Spawner unregister request failed: {exception}",
                    Logging.LogChannels.System);
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.Error));
            }
        }

        /// <summary>
        /// This method should be called, when spawn process is finalized (finished spawning).
        /// For example, when spawned game server fully starts
        /// </summary>
        /// <param name="spawnId"></param>
        /// <param name="callback"></param>
        public void FinalizeSpawnedProcess(int spawnId, SuccessCallback callback = null)
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
                callback?.Invoke(false, Mst.Errors.Parse(ResponseStatus.NotConnected));
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
                    callback?.Invoke(false, Mst.Errors.Parse(status, response));
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
            int completionState = 0;

            void Complete(SpawnTaskController controller, string error)
            {
                if (Interlocked.Exchange(ref completionState, 1) != 0)
                    return;

                try
                {
                    callback?.Invoke(controller, error);
                }
                catch (Exception exception)
                {
                    Logging.Logs.Error($"Spawned process registration callback failed: {exception}",
                        Logging.LogChannels.System);
                }
            }

            if (connection == null)
            {
                Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            if (!connection.IsConnected)
            {
                Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                return;
            }

            Mst.Security.RequestPermission(MstPermissionKeys.RoomServer, (isSuccessful, error) =>
            {
                if (!isSuccessful)
                {
                    Logging.Logs.Error(
                        $"Failed to obtain '{MstPermissionKeys.RoomServer}' permission",
                        Logging.LogChannels.System);
                    Complete(null, Mst.Errors.Parse(ResponseStatus.Unauthorized));
                    return;
                }

                if (!connection.IsConnected)
                {
                    Complete(null, Mst.Errors.Parse(ResponseStatus.NotConnected));
                    return;
                }

                var packet = new RegisterSpawnedProcessPacket()
                {
                    SpawnCode = spawnCode,
                    SpawnId = spawnId
                };

                try
                {
                    connection.SendMessage(MstOpCodes.RegisterSpawnedProcess, packet, (status, response) =>
                    {
                        try
                        {
                            if (status != ResponseStatus.Success)
                            {
                                Complete(null, Mst.Errors.Parse(status, response));
                                return;
                            }

                            if (response == null)
                            {
                                Complete(null, Mst.Errors.Parse(ResponseStatus.Invalid));
                                return;
                            }

                            // Read spawn task options received from master server
                            var options = MstProperties.FromBytes(response.AsBytes());

                            // Create spawn task controller
                            var process = new SpawnTaskController(spawnId, options, connection);
                            Complete(process, null);
                        }
                        catch (Exception exception)
                        {
                            Logging.Logs.Error($"Spawned process registration response processing failed: {exception}",
                                Logging.LogChannels.System);
                            Complete(null, Mst.Errors.Parse(ResponseStatus.Error));
                        }
                    });
                }
                catch (Exception exception)
                {
                    Logging.Logs.Error($"Spawned process registration request failed: {exception}",
                        Logging.LogChannels.System);
                    Complete(null, Mst.Errors.Parse(ResponseStatus.Error));
                }
            }, connection);
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
        /// Gets a spawner controller by id when the id is unique across all registered connections.
        /// </summary>
        /// <param name="spawnerId"></param>
        /// <returns>The matching controller, or null when no controller exists or the id is ambiguous.</returns>
        public ISpawnerController GetSpawnerController(int spawnerId)
        {
            lock (controllerRegistrySync)
            {
                ISpawnerController match = null;

                foreach (KeyValuePair<SpawnerControllerKey, ISpawnerController> entry in createdSpawnerControllers)
                {
                    if (entry.Key.SpawnerId != spawnerId)
                        continue;

                    if (match != null)
                        return null;

                    match = entry.Value;
                }

                return match;
            }
        }

        /// <summary>
        /// Gets a spawner controller registered through the specified connection.
        /// </summary>
        public ISpawnerController GetSpawnerController(int spawnerId, IClientSocket connection)
        {
            if (connection == null)
                return null;

            lock (controllerRegistrySync)
            {
                createdSpawnerControllers.TryGetValue(new SpawnerControllerKey(connection, spawnerId),
                    out ISpawnerController controller);
                return controller;
            }
        }

        /// <summary>
        /// Get list of spawner controllers
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ISpawnerController> GetCreatedSpawnerControllers()
        {
            lock (controllerRegistrySync)
                return new List<ISpawnerController>(createdSpawnerControllers.Values);
        }

        private void RegisterSpawnerController(ISpawnerController controller)
        {
            lock (controllerRegistrySync)
            {
                var key = new SpawnerControllerKey(controller.Connection, controller.SpawnerId);

                if (createdSpawnerControllers.ContainsKey(key))
                {
                    throw new InvalidOperationException(
                        $"Spawner controller [{controller.SpawnerId}] is already registered for this connection");
                }

                if (!ReferenceEquals(controller.Connection, Connection))
                {
                    if (!customConnectionHandlers.TryGetValue(controller.Connection,
                        out ConnectionHandlerRegistration registration))
                    {
                        registration = CreateCustomConnectionHandlers(controller.Connection);
                        customConnectionHandlers.Add(controller.Connection, registration);
                    }

                    registration.OwnerCount++;
                }

                createdSpawnerControllers.Add(key, controller);
            }
        }

        private ConnectionHandlerRegistration CreateCustomConnectionHandlers(IClientSocket connection)
        {
            var spawnHandler = new PacketHandler(MstOpCodes.SpawnProcessRequest,
                message => SpawnProcessRequestHandler(message, connection));
            var killHandler = new PacketHandler(MstOpCodes.KillProcessRequest,
                message => KillProcessRequestHandler(message, connection));
            IPacketHandler registeredSpawnHandler = null;

            try
            {
                registeredSpawnHandler = connection.RegisterMessageHandler(spawnHandler);
                IPacketHandler registeredKillHandler = connection.RegisterMessageHandler(killHandler);

                return new ConnectionHandlerRegistration
                {
                    Connection = connection,
                    SpawnHandler = registeredSpawnHandler,
                    KillHandler = registeredKillHandler
                };
            }
            catch
            {
                if (registeredSpawnHandler != null)
                    connection.UnregisterMessageHandler(registeredSpawnHandler);

                throw;
            }
        }

        internal void UnregisterSpawnerController(ISpawnerController controller)
        {
            if (controller == null)
                return;

            ConnectionHandlerRegistration handlersToRemove = null;
            bool controllerRemoved = false;

            lock (controllerRegistrySync)
            {
                var key = new SpawnerControllerKey(controller.Connection, controller.SpawnerId);

                if (createdSpawnerControllers.TryGetValue(key, out ISpawnerController current) &&
                    ReferenceEquals(current, controller))
                {
                    createdSpawnerControllers.Remove(key);
                    controllerRemoved = true;
                }

                if (controllerRemoved && customConnectionHandlers.TryGetValue(controller.Connection,
                    out ConnectionHandlerRegistration registration))
                {
                    registration.OwnerCount = Math.Max(0, registration.OwnerCount - 1);

                    if (registration.OwnerCount == 0)
                    {
                        customConnectionHandlers.Remove(controller.Connection);
                        handlersToRemove = registration;
                    }
                }
            }

            if (handlersToRemove == null)
                return;

            handlersToRemove.Connection.UnregisterMessageHandler(handlersToRemove.SpawnHandler);
            handlersToRemove.Connection.UnregisterMessageHandler(handlersToRemove.KillHandler);
        }

        /// <summary>
        /// Gets available port
        /// </summary>
        /// <returns></returns>
        public int GetAvailablePort()
        {
            lock (portAllocationSync)
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
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="port"></param>
        public void ReleasePort(int port)
        {
            lock (portAllocationSync)
            {
                freePorts.Enqueue(port);
            }
        }

        /// <summary>
        /// Gets available redirected port.
        /// </summary>
        /// <returns></returns>
        public int GetAvailableRedirectPort()
        {
            lock (redirectPortAllocationSync)
            {
                if (freeRedirectPorts.Count > 0)
                {
                    return freeRedirectPorts.Dequeue();
                }

                if (lastRedirectPortTaken < 0)
                {
                    lastRedirectPortTaken = DefaultRedirectPort;
                }

                return lastRedirectPortTaken++;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="port"></param>
        public void ReleaseRedirectPort(int port)
        {
            lock (redirectPortAllocationSync)
            {
                freeRedirectPorts.Enqueue(port);
            }
        }
    }
}
