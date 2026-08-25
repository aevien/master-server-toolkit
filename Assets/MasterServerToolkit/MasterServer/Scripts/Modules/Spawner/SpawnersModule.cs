using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnersModule : BaseServerModule
    {
        private sealed class OwnerSpawnersState
        {
            public OwnerSpawnersState(IPeer peer)
            {
                Peer = peer;
            }

            public object Gate { get; } = new object();
            public IPeer Peer { get; }
            public ConcurrentDictionary<int, RegisteredSpawner> Spawners { get; } =
                new ConcurrentDictionary<int, RegisteredSpawner>();
            public bool IsActive { get; set; } = true;
        }

        private sealed class ClientSpawnRequestState
        {
            public ClientSpawnRequestState(IPeer peer)
            {
                Peer = peer;
            }

            public object Gate { get; } = new object();
            public IPeer Peer { get; }
        }

        public delegate void SpawnedProcessRegistrationHandler(SpawnTask task, IPeer peer);

        #region INSPECTOR

        [Tooltip("Realtime interval in seconds between master-side spawner queue dispatch passes. Values below 0.01 seconds are treated as 0.01 and increase master update work."), SerializeField]
        protected float queueUpdateFrequency = 0.1f;

        [Tooltip("Maximum process-start requests concurrently awaiting responses from each registered spawner. Values below 1 are treated as 1."), SerializeField]
        protected int maxConcurrentSpawnRequests = 8;

        [Tooltip("Minimum delay in milliseconds between process-start requests sent to one registered spawner. 0 disables throttling; negative values are treated as 0."), SerializeField]
        protected int spawnRequestThrottleIntervalMs = 100;

        [Tooltip("Allows ordinary client spawn requests. Lobby/server-owned spawn flows remain available when this setting is disabled."), SerializeField]
        protected bool enableClientSpawnRequests = true;

        [Tooltip("Maximum time in milliseconds to wait for process-killed confirmation while supervising spawner closure. Minimum Inspector and runtime value is 1 millisecond."),
         SerializeField, Min(1)]
        protected int shutdownConfirmationTimeoutMs = 5000;

        #endregion

        private int lastSpawnerId = -1;
        private int lastSpawnTaskId = -1;

        protected readonly ConcurrentDictionary<int, RegisteredSpawner> spawnersList = new ConcurrentDictionary<int, RegisteredSpawner>();
        protected readonly ConcurrentDictionary<int, SpawnTask> spawnTasksList = new ConcurrentDictionary<int, SpawnTask>();
        private readonly ConcurrentDictionary<int, RegisteredSpawner> closingSpawnersList =
            new ConcurrentDictionary<int, RegisteredSpawner>();
        private readonly ConcurrentDictionary<int, Lazy<Task>> closeSupervisors =
            new ConcurrentDictionary<int, Lazy<Task>>();
        private readonly ConcurrentDictionary<int, OwnerSpawnersState> ownerSpawnersByPeerId =
            new ConcurrentDictionary<int, OwnerSpawnersState>();
        private readonly ConcurrentDictionary<int, ClientSpawnRequestState> clientSpawnRequestStates =
            new ConcurrentDictionary<int, ClientSpawnRequestState>();
        private readonly object runGate = new object();
        private Coroutine queueUpdaterCoroutine;
        private bool acceptsSpawnerWork = true;

        public IEnumerable<RegisteredSpawner> Spawners => spawnersList.Values;
        public IEnumerable<SpawnTask> Tasks => spawnTasksList.Values;
        private int MaxConcurrentSpawnRequests => Mathf.Max(1, maxConcurrentSpawnRequests);
        private int SpawnRequestThrottleIntervalMs => Mathf.Max(0, spawnRequestThrottleIntervalMs);
        protected virtual int ShutdownConfirmationTimeoutMs => Mathf.Max(1, shutdownConfirmationTimeoutMs);
        protected int PendingCloseSupervisionCount => closeSupervisors.Count;

        public event Action<RegisteredSpawner> OnSpawnerRegisteredEvent;
        public event Action<RegisteredSpawner> OnSpawnerDestroyedEvent;
        public event SpawnedProcessRegistrationHandler OnSpawnedProcessRegisteredEvent;

        public override void Initialize(IServer server)
        {
            RegisteredSpawner.MaxConcurrentRequests = MaxConcurrentSpawnRequests;
            RegisteredSpawner.SpawnRequestThrottleIntervalMs = SpawnRequestThrottleIntervalMs;

            // Add handlers
            server.RegisterMessageHandler(MstOpCodes.RegisterSpawner, RegisterSpawnerRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.UnregisterSpawner, UnregisterSpawnerRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientsSpawnRequest, ClientsSpawnRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.RegisterSpawnedProcess, RegisterSpawnedProcessRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.CompleteSpawnProcess, CompleteSpawnProcessRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ProcessStarted, SetProcessStartedRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ProcessKilled, SetProcessKilledRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.AbortSpawnRequest, AbortSpawnRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.GetSpawnFinalizationData, GetCompletionDataRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.UpdateSpawnerProcessesCount, SetSpawnedProcessesCountRequestHandler);

        }

        public override void StartServerRun(CancellationToken runCancellationToken)
        {
            lock (runGate)
                acceptsSpawnerWork = true;

            if (queueUpdaterCoroutine == null)
                queueUpdaterCoroutine = StartCoroutine(StartQueueUpdater());
        }

        public override async Task StopServerRunAsync()
        {
            List<RegisteredSpawner> registeredSpawners;
            List<Lazy<Task>> supervisors;

            lock (runGate)
            {
                acceptsSpawnerWork = false;
                registeredSpawners = spawnersList.Values
                    .Concat(closingSpawnersList.Values)
                    .Distinct()
                    .ToList();
                supervisors = closeSupervisors.Values
                    .Concat(registeredSpawners.Select(GetOrCreateCloseSupervisor))
                    .Distinct()
                    .ToList();
            }

            if (queueUpdaterCoroutine != null)
            {
                StopCoroutine(queueUpdaterCoroutine);
                queueUpdaterCoroutine = null;
            }

            foreach (RegisteredSpawner spawner in registeredSpawners)
            {
                if (TryDestroySpawner(spawner, true))
                    continue;

                foreach (SpawnTask task in spawner.GetAllTasksSnapshot())
                    SendShutdownKillRequest(spawner, task, true);
            }

            await Task.WhenAll(supervisors.Select(supervisor => supervisor.Value)).ConfigureAwait(false);

            foreach (OwnerSpawnersState ownerState in ownerSpawnersByPeerId.Values)
                ownerState.Peer.OnConnectionCloseEvent -= OnRegisteredPeerDisconnect;

            ownerSpawnersByPeerId.Clear();
            foreach (ClientSpawnRequestState requestState in clientSpawnRequestStates.Values)
                requestState.Peer.OnConnectionCloseEvent -= OnClientSpawnRequesterDisconnect;

            clientSpawnRequestStates.Clear();
            spawnTasksList.Clear();
            closingSpawnersList.Clear();
            closeSupervisors.Clear();
        }

        public override MstJson Details()
        {
            var info = base.Details();
            info.SetField("description", "This module manages the processes of running rooms.");
            info["properties"].SetField("totalSpawners", spawnersList.Count);

            int totalRooms = 0;

            MstJson spawners = MstJson.CreateArray();

            foreach (var spawner in spawnersList.Values)
            {
                totalRooms += spawner.ProcessesRunning;

                var spawnerJson = MstJson.CreateObject();
                spawnerJson.AddField("id", spawner.SpawnerId);
                spawnerJson.AddField("processes", spawner.ProcessesRunning);
                spawnerJson.AddField("processes", spawner.ProcessesRunning);

                var options = MstJson.CreateObject();
                options.AddField("machineIp", spawner.Options.MachineIp);
                options.AddField("maxProcesses", spawner.Options.MaxProcesses);
                options.AddField("region", spawner.Options.Region);

                var customOptions = MstJson.CreateObject();

                foreach (var option in spawner.Options.CustomOptions)
                    customOptions.AddField(option.Key, option.Value);

                options.AddField("customOptions", customOptions);

                spawnerJson.AddField("options", options);

                spawners.Add(spawnerJson);
            }

            info["properties"].SetField("totalStartedRooms", totalRooms);

            var allRegions = MstJson.CreateArray();

            foreach (var region in GetRegions().Select(i => i.Name))
                allRegions.Add(region);

            info["properties"].SetField("allRegions", allRegions);
            info["properties"].SetField("maxConcurrentRequests", MaxConcurrentSpawnRequests);
            info["properties"].SetField("spawnRequestThrottleIntervalMs", SpawnRequestThrottleIntervalMs);
            info["properties"].SetField("spawners", spawners);

            return info;
        }

        /// <summary>
        /// Creates spawner for given peer using options
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public virtual RegisteredSpawner CreateSpawner(IPeer peer, SpawnerOptions options)
        {
            if (peer == null)
                throw new ArgumentNullException(nameof(peer));

            if (options == null)
                throw new ArgumentNullException(nameof(options));

            RegisteredSpawner spawnerInstance;
            bool shouldPublishLifecycleEvents;

            lock (runGate)
            {
                if (!acceptsSpawnerWork || !peer.IsConnected)
                    return null;

                OwnerSpawnersState ownerState = GetOrCreateOwnerSpawnersState(peer);
                spawnerInstance = new RegisteredSpawner(GenerateSpawnerId(), peer, options, logger,
                    MaxConcurrentSpawnRequests, SpawnRequestThrottleIntervalMs);

                lock (ownerState.Gate)
                {
                    if (!ownerState.IsActive || !peer.IsConnected)
                        return null;

                    ownerState.Spawners[spawnerInstance.SpawnerId] = spawnerInstance;
                    spawnersList[spawnerInstance.SpawnerId] = spawnerInstance;
                }

                shouldPublishLifecycleEvents = spawnerInstance.QueueLifecycleEvent(() =>
                    InvokeSpawnerEventSafely(OnSpawnerRegisteredEvent, spawnerInstance,
                        nameof(OnSpawnerRegisteredEvent)));
            }

            if (shouldPublishLifecycleEvents)
                spawnerInstance.PublishPendingLifecycleEvents();

            return spawnerInstance;
        }

        private OwnerSpawnersState GetOrCreateOwnerSpawnersState(IPeer peer)
        {
            while (true)
            {
                if (ownerSpawnersByPeerId.TryGetValue(peer.Id, out OwnerSpawnersState existingState))
                    return existingState;

                var newState = new OwnerSpawnersState(peer);

                if (!ownerSpawnersByPeerId.TryAdd(peer.Id, newState))
                    continue;

                peer.SetProperty(MstPeerPropertyCodes.RegisteredSpawners, newState.Spawners);
                peer.OnConnectionCloseEvent += OnRegisteredPeerDisconnect;

                if (!peer.IsConnected)
                    OnRegisteredPeerDisconnect(peer);

                return newState;
            }
        }

        /// <summary>
        /// Invokes when peer disconnected from server
        /// </summary>
        /// <param name="peer"></param>
        private void OnRegisteredPeerDisconnect(IPeer peer)
        {
            if (peer == null || !ownerSpawnersByPeerId.TryRemove(peer.Id, out OwnerSpawnersState ownerState))
                return;

            List<RegisteredSpawner> registeredSpawners;

            lock (ownerState.Gate)
            {
                ownerState.IsActive = false;
                registeredSpawners = ownerState.Spawners.Values.ToList();
                ownerState.Spawners.Clear();
            }

            foreach (var registeredSpawner in registeredSpawners)
                DestroySpawner(registeredSpawner);
        }

        private ClientSpawnRequestState GetOrCreateClientSpawnRequestState(IPeer peer)
        {
            while (true)
            {
                if (clientSpawnRequestStates.TryGetValue(peer.Id, out ClientSpawnRequestState existingState))
                    return existingState;

                var newState = new ClientSpawnRequestState(peer);

                if (!clientSpawnRequestStates.TryAdd(peer.Id, newState))
                    continue;

                peer.OnConnectionCloseEvent += OnClientSpawnRequesterDisconnect;

                if (!peer.IsConnected)
                    OnClientSpawnRequesterDisconnect(peer);

                return newState;
            }
        }

        private void OnClientSpawnRequesterDisconnect(IPeer peer)
        {
            if (peer == null ||
                !clientSpawnRequestStates.TryRemove(peer.Id, out ClientSpawnRequestState requestState))
            {
                return;
            }

            requestState.Peer.OnConnectionCloseEvent -= OnClientSpawnRequesterDisconnect;
        }

        /// <summary>
        /// Destroys spawner
        /// </summary>
        /// <param name="spawner"></param>
        public void DestroySpawner(RegisteredSpawner spawner)
        {
            TryDestroySpawner(spawner);
        }

        private bool TryDestroySpawner(RegisteredSpawner spawner, bool abandonOnKillResponse = false)
        {
            Lazy<Task> closeSupervisor;

            lock (runGate)
            {
                if (spawner == null ||
                    !spawnersList.TryGetValue(spawner.SpawnerId, out RegisteredSpawner current) ||
                    !ReferenceEquals(spawner, current) ||
                    !((ICollection<KeyValuePair<int, RegisteredSpawner>>)spawnersList).Remove(
                        new KeyValuePair<int, RegisteredSpawner>(spawner.SpawnerId, spawner)))
                {
                    return false;
                }

                closingSpawnersList[spawner.SpawnerId] = spawner;
                closeSupervisor = GetOrCreateCloseSupervisor(spawner);
            }

            var peer = spawner.Peer;

            if (peer != null && ownerSpawnersByPeerId.TryGetValue(peer.Id, out OwnerSpawnersState ownerState))
            {
                lock (ownerState.Gate)
                    ownerState.Spawners.TryRemove(spawner.SpawnerId, out _);
            }

            IReadOnlyList<SpawnTask> liveTasks = spawner.CloseAndGetTasksSnapshot();

            if (peer == null || !peer.IsConnected)
            {
                foreach (SpawnTask task in spawner.ForceCloseAfterTimeout())
                    spawnTasksList.TryRemove(task.Id, out _);

                closingSpawnersList.TryRemove(spawner.SpawnerId, out _);
            }
            else
            {
                foreach (SpawnTask task in liveTasks)
                    SendShutdownKillRequest(spawner, task, abandonOnKillResponse);
            }

            _ = closeSupervisor.Value;

            bool shouldPublishLifecycleEvents = spawner.QueueLifecycleEvent(() =>
                InvokeSpawnerEventSafely(OnSpawnerDestroyedEvent, spawner, nameof(OnSpawnerDestroyedEvent)));

            if (shouldPublishLifecycleEvents)
                spawner.PublishPendingLifecycleEvents();

            if (spawner.LifecycleState == RegisteredSpawnerLifecycleState.Closed)
                closingSpawnersList.TryRemove(spawner.SpawnerId, out _);

            return true;
        }

        private Lazy<Task> GetOrCreateCloseSupervisor(RegisteredSpawner spawner)
        {
            return closeSupervisors.GetOrAdd(spawner.SpawnerId, _ =>
                new Lazy<Task>(() => SuperviseSpawnerCloseAsync(spawner),
                    LazyThreadSafetyMode.ExecutionAndPublication));
        }

        private async Task SuperviseSpawnerCloseAsync(RegisteredSpawner spawner)
        {
            try
            {
                using var timeoutCancellation = new CancellationTokenSource();
                Task timeout = Task.Delay(ShutdownConfirmationTimeoutMs, timeoutCancellation.Token);

                if (await Task.WhenAny(spawner.CloseCompletion, timeout).ConfigureAwait(false) ==
                    spawner.CloseCompletion)
                {
                    timeoutCancellation.Cancel();
                    await spawner.CloseCompletion.ConfigureAwait(false);
                    return;
                }

                if (spawner.CloseCompletion.IsCompleted)
                {
                    await spawner.CloseCompletion.ConfigureAwait(false);
                    return;
                }

                IReadOnlyList<SpawnTask> abandonedTasks = spawner.ForceCloseAfterTimeout();

                foreach (SpawnTask task in abandonedTasks)
                    spawnTasksList.TryRemove(task.Id, out _);

                logger.Warn($"Spawner [{spawner.SpawnerId}] close confirmation timed out after " +
                            $"{ShutdownConfirmationTimeoutMs} ms. Abandoned task supervision: {abandonedTasks.Count}");
            }
            finally
            {
                ((ICollection<KeyValuePair<int, RegisteredSpawner>>)closingSpawnersList).Remove(
                    new KeyValuePair<int, RegisteredSpawner>(spawner.SpawnerId, spawner));
                closeSupervisors.TryRemove(spawner.SpawnerId, out _);
            }
        }

        private void SendShutdownKillRequest(RegisteredSpawner spawner, SpawnTask task,
            bool abandonOnResponse = false)
        {
            spawner.SendKillRequest(task.Id, status =>
            {
                if (spawner.LifecycleState == RegisteredSpawnerLifecycleState.Closed)
                    return;

                if (status == ResponseStatus.NotFound)
                {
                    CompleteKilledTask(spawner, task);
                    return;
                }

                if (abandonOnResponse)
                {
                    AbandonTaskSupervision(spawner, task);

                    if (status != ResponseStatus.Success)
                    {
                        logger.Warn($"Spawner [{spawner.SpawnerId}] could not confirm shutdown kill request for task " +
                                    $"[{task.Id}]. Status: {status}. Process state remains unknown");
                    }

                    return;
                }

                if (status != ResponseStatus.Success)
                {
                    logger.Warn($"Spawner [{spawner.SpawnerId}] did not accept shutdown kill request for task " +
                                $"[{task.Id}]. Status: {status}");
                }
            });
        }

        private void AbandonTaskSupervision(RegisteredSpawner spawner, SpawnTask task)
        {
            if (!spawner.AbandonTaskSupervision(task))
                return;

            spawnTasksList.TryRemove(task.Id, out _);

            if (spawner.LifecycleState == RegisteredSpawnerLifecycleState.Closed)
                closingSpawnersList.TryRemove(spawner.SpawnerId, out _);
        }

        private void CompleteKilledTask(RegisteredSpawner spawner, SpawnTask task)
        {
            if (!spawner.TryMarkProcessKilled(task))
                return;

            spawnTasksList.TryRemove(task.Id, out _);
            spawner.RemoveTask(task);

            if (spawner.LifecycleState == RegisteredSpawnerLifecycleState.Closed)
                closingSpawnersList.TryRemove(spawner.SpawnerId, out _);
        }

        private void InvokeSpawnerEventSafely(Action<RegisteredSpawner> handlers,
            RegisteredSpawner spawner, string eventName)
        {
            if (handlers == null)
                return;

            foreach (Action<RegisteredSpawner> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler.Invoke(spawner);
                }
                catch (Exception exception)
                {
                    logger.Error($"{eventName} subscriber failed for spawner {spawner.SpawnerId}: {exception}");
                }
            }
        }

        private bool IsAcceptingSpawnerWork()
        {
            lock (runGate)
                return acceptsSpawnerWork;
        }

        /// <summary>
        /// Creates unique spawner id
        /// </summary>
        /// <returns></returns>
        public int GenerateSpawnerId()
        {
            return Interlocked.Increment(ref lastSpawnerId);
        }

        /// <summary>
        /// Creates unique spawner tsak id
        /// </summary>
        /// <returns></returns>
        public int GenerateSpawnTaskId()
        {
            return Interlocked.Increment(ref lastSpawnTaskId);
        }

        /// <summary>
        /// Start process on spawner side with given spawn options
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        public SpawnTask Spawn(MstProperties options)
        {
            return Spawn(options, string.Empty);
        }

        /// <summary>
        /// Start process on spawner side with given spawn <paramref name="options"/>, <paramref name="region"/> and <paramref name="customOptions"/>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="region"></param>
        /// <param name="customOptions"></param>
        /// <returns></returns>
        public virtual SpawnTask Spawn(MstProperties options, string region)
        {
            return SpawnConfigured(options, region, null);
        }

        private SpawnTask SpawnConfigured(MstProperties options, string region, Action<SpawnTask> configureTask)
        {
            if (options == null)
                return null;

            List<RegisteredSpawner> spawners = (GetFilteredSpawners(options, region) ??
                    Enumerable.Empty<RegisteredSpawner>())
                .Where(spawner => spawner != null && spawner.IsActive)
                .OrderByDescending(spawner => spawner.CalculateFreeSlotsCount())
                .ToList();

            if (spawners.Count == 0)
            {
                logger.Warn($"No spawner was returned after filtering. Region: {options.AsString(Mst.Args.Names.RoomRegion, string.IsNullOrEmpty(region) ? "International" : region)}");
                return null;
            }

            foreach (RegisteredSpawner spawner in spawners)
            {
                SpawnTask task = TryCreateSpawnTask(options, spawner, configureTask);

                if (task != null)
                    return task;
            }

            return null;
        }

        /// <summary>
        /// Start process on spawner side with given spawn <paramref name="options"/>, <paramref name="customOptions"/> and <paramref name="spawner"/>
        /// </summary>
        /// <param name="options"></param>
        /// <param name="customOptions"></param>
        /// <param name="spawner"></param>
        /// <returns></returns>
        public virtual SpawnTask Spawn(MstProperties options, RegisteredSpawner spawner)
        {
            return TryCreateSpawnTask(options, spawner, null);
        }

        private SpawnTask TryCreateSpawnTask(MstProperties options, RegisteredSpawner spawner,
            Action<SpawnTask> configureTask)
        {
            lock (runGate)
            {
                if (!acceptsSpawnerWork || options == null || spawner == null || !spawner.IsActive)
                    return null;

                var task = new SpawnTask(GenerateSpawnTaskId(), spawner, options);
                Action<SpawnStatus> lifecycleHandler = null;
                lifecycleHandler = status =>
                {
                    if (status != SpawnStatus.Aborted && status != SpawnStatus.Killed)
                        return;

                    if (status == SpawnStatus.Aborted && task.IsProcessRunning)
                        return;

                    task.OnStatusChangedEvent -= lifecycleHandler;
                    spawnTasksList.TryRemove(task.Id, out _);
                    spawner.RemoveTask(task);
                };

                task.OnStatusChangedEvent += lifecycleHandler;
                configureTask?.Invoke(task);

                spawnTasksList[task.Id] = task;

                if (!spawner.TryReserveAndEnqueue(task))
                {
                    task.OnStatusChangedEvent -= lifecycleHandler;
                    spawnTasksList.TryRemove(task.Id, out _);
                    return null;
                }

                logger.Debug($"Spawner was found, and spawn task created: {task}");
                return task;
            }
        }

        /// <summary>
        /// Retrieves a list of spawner that can be used with given properties and region name
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public virtual IEnumerable<RegisteredSpawner> GetFilteredSpawners(MstProperties properties, string region)
        {
            return GetSpawners(region);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<RegisteredSpawner> GetSpawners()
        {
            return GetSpawners(null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        public virtual IEnumerable<RegisteredSpawner> GetSpawners(string region)
        {
            // If region is not provided, retrieve all spawners
            if (string.IsNullOrEmpty(region))
            {
                return spawnersList.Values.ToList();
            }

            return spawnersList.Values
                .Where(s => s.Options.Region == region)
                .ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<RegionInfo> GetRegions()
        {
            var list = new List<RegionInfo>();
            var regions = spawnersList.Values.Select(i => new RegionInfo()
            {
                Name = i.Options.Region,
                Ip = i.Options.MachineIp
            });

            foreach (var region in regions)
            {
                if (!list.Contains(region))
                {
                    list.Add(region);
                }
            }

            return list;
        }

        /// <summary>
        /// Returns true, if peer has permissions to register a spawner
        /// </summary>
        /// <param name="peer"></param>
        /// <returns></returns>
        protected virtual bool HasCreationPermissions(IPeer peer)
        {
            var extension = peer.GetExtension<SecurityInfoPeerExtension>();
            return extension != null &&
                   (extension.HasPermission(MstPermissionKeys.Spawner) ||
                    extension.HasAccountPermission(MstPermissionLevels.Admin));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="peer"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        protected virtual bool CanClientSpawn(IPeer peer, MstProperties options)
        {
            return enableClientSpawnRequests && peer.GetExtension<IUserPeerExtension>() != null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected virtual IEnumerator StartQueueUpdater()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, queueUpdateFrequency));

                foreach (var spawner in Spawners)
                {
                    try
                    {
                        spawner.UpdateQueue();
                    }
                    catch (Exception e)
                    {
                        logger.Error(e);
                    }
                }
            }
        }

        #region Message Handlers

        /// <summary>
        /// Fired whe connected client has made request to spawn process
        /// </summary>
        /// <param name="message"></param>
        protected virtual Task ClientsSpawnRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!IsAcceptingSpawnerWork())
                {
                    message.RespondError(ResponseStatus.ServiceUnavailable, MstErrorCodes.SPAWNER_MODULE_STOPPING);
                    return Task.CompletedTask;
                }

                // Parse data from message
                var options = MstProperties.FromBytes(message.AsBytes());
                var peer = message.Peer;
                ClientSpawnRequestState requestState = GetOrCreateClientSpawnRequestState(peer);
                var pendingStatuses = new Queue<SpawnStatus>();
                var statusGate = new object();
                bool statusDeliveryEnabled = false;
                bool isDispatchingStatuses = false;
                int spawnTaskId = -1;

                void DispatchPendingStatuses()
                {
                    while (true)
                    {
                        SpawnStatus status;

                        lock (statusGate)
                        {
                            if (!statusDeliveryEnabled || pendingStatuses.Count == 0)
                            {
                                isDispatchingStatuses = false;
                                return;
                            }

                            status = pendingStatuses.Dequeue();
                        }

                        try
                        {
                            if (peer.IsConnected)
                            {
                                peer.SendMessage(MessageHelper.Create(MstOpCodes.SpawnRequestStatusChange,
                                    new SpawnStatusUpdatePacket
                                    {
                                        SpawnId = spawnTaskId,
                                        Status = status
                                    }));
                            }
                        }
                        catch (Exception exception)
                        {
                            logger.Error($"Failed to send status [{status}] for spawn task [{spawnTaskId}]: {exception}");
                        }
                    }
                }

                void QueueStatus(SpawnStatus status)
                {
                    bool shouldDispatch;

                    lock (statusGate)
                    {
                        pendingStatuses.Enqueue(status);
                        shouldDispatch = statusDeliveryEnabled && !isDispatchingStatuses;

                        if (shouldDispatch)
                            isDispatchingStatuses = true;
                    }

                    if (shouldDispatch)
                        DispatchPendingStatuses();
                }

                void EnableStatusDelivery()
                {
                    bool shouldDispatch;

                    lock (statusGate)
                    {
                        statusDeliveryEnabled = true;
                        shouldDispatch = pendingStatuses.Count > 0 && !isDispatchingStatuses;

                        if (shouldDispatch)
                            isDispatchingStatuses = true;
                    }

                    if (shouldDispatch)
                        DispatchPendingStatuses();
                }

                logger.Info($"Client {peer.Id} requested to spawn room with options: {options}");

                if (spawnersList.Count == 0)
                {
                    logger.Error("But no registered spawner was found!");
                    message.RespondError(ResponseStatus.ServiceUnavailable,
                        MstErrorCodes.SPAWNER_NOT_REGISTERED);
                    return Task.CompletedTask;
                }

                if (!CanClientSpawn(peer, options))
                {
                    logger.Error("Unauthorized request");
                    message.RespondError(ResponseStatus.Unauthorized, MstErrorCodes.SPAWN_PERMISSION_DENIED);
                    return Task.CompletedTask;
                }

                SpawnTask task;

                lock (requestState.Gate)
                {
                    if (!peer.IsConnected)
                    {
                        message.RespondError(ResponseStatus.NotConnected,
                            MstErrorCodes.SPAWN_REQUESTER_DISCONNECTED);
                        return Task.CompletedTask;
                    }

                    SpawnTask prevRequest = peer.GetProperty(MstPeerPropertyCodes.ClientSpawnRequest) as SpawnTask;

                    if (prevRequest != null && !prevRequest.IsDoneStartingProcess)
                    {
                        logger.Warn("And he already has an active request");
                        message.RespondError(ResponseStatus.Conflict,
                            MstErrorCodes.SPAWN_REQUEST_ALREADY_ACTIVE);
                        return Task.CompletedTask;
                    }

                    task = SpawnConfigured(options, options.AsString(Mst.Args.Names.RoomRegion), configuredTask =>
                    {
                        spawnTaskId = configuredTask.Id;
                        configuredTask.Requester = peer;
                        configuredTask.OnStatusChangedEvent += QueueStatus;
                    });

                    if (task == null)
                    {
                        if (!IsAcceptingSpawnerWork())
                        {
                            message.RespondError(ResponseStatus.ServiceUnavailable,
                                MstErrorCodes.SPAWNER_MODULE_STOPPING);
                            return Task.CompletedTask;
                        }

                        logger.Warn("But all the servers are busy. Let him try again later");
                        message.RespondError(ResponseStatus.ServiceUnavailable,
                            MstErrorCodes.SPAWNER_CAPACITY_UNAVAILABLE);
                        return Task.CompletedTask;
                    }

                    if (!peer.IsConnected)
                    {
                        task.Abort();
                        return Task.CompletedTask;
                    }

                    peer.SetProperty(MstPeerPropertyCodes.ClientSpawnRequest, task);
                }

                try
                {
                    message.Respond(task.Id, ResponseStatus.Success);
                    EnableStatusDelivery();
                }
                catch
                {
                    task.Abort();
                    throw;
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        private Task AbortSpawnRequestHandler(IIncomingMessage message)
        {
            try
            {
                SpawnTask prevRequest = message.Peer.GetProperty(MstPeerPropertyCodes.ClientSpawnRequest) as SpawnTask;

                if (prevRequest == null)
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.SPAWN_REQUEST_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (prevRequest.Status == SpawnStatus.Finalized)
                {
                    message.RespondError(ResponseStatus.Conflict,
                        MstErrorCodes.SPAWN_REQUEST_ALREADY_COMPLETED);
                    return Task.CompletedTask;
                }

                if (prevRequest.Status == SpawnStatus.Aborting ||
                    prevRequest.Status == SpawnStatus.Aborted ||
                    prevRequest.Status == SpawnStatus.Killed)
                {
                    message.Respond("Already aborting", ResponseStatus.Success);
                    return Task.CompletedTask;
                }

                logger.Debug($"Client [{message.Peer.Id}] requested to terminate process [{prevRequest.Id}]");

                prevRequest.Abort();
                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        protected virtual Task GetCompletionDataRequestHandler(IIncomingMessage message)
        {
            try
            {
                var spawnId = message.AsInt();

                if (!spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
                {
                    task = message.Peer.GetProperty(MstPeerPropertyCodes.ClientSpawnRequest) as SpawnTask;

                    if (task == null || task.Id != spawnId)
                    {
                        message.RespondError(ResponseStatus.NotFound, MstErrorCodes.SPAWN_REQUEST_NOT_FOUND);
                        return Task.CompletedTask;
                    }
                }

                if (task.Requester != message.Peer)
                {
                    message.RespondError(ResponseStatus.Forbidden, MstErrorCodes.SPAWN_REQUEST_OWNER_REQUIRED);
                    return Task.CompletedTask;
                }

                if (task.FinalizationPacket == null)
                {
                    message.RespondError(ResponseStatus.Conflict,
                        MstErrorCodes.SPAWN_FINALIZATION_UNAVAILABLE);
                    return Task.CompletedTask;
                }

                // Respond with data (dictionary of strings)
                message.Respond(task.FinalizationPacket.FinalizationData.ToBytes(), ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        protected virtual Task RegisterSpawnerRequestHandler(IIncomingMessage message)
        {
            try
            {
                logger.Debug($"Client [{message.Peer.Id}] requested to be registered as spawner");

                if (!IsAcceptingSpawnerWork())
                {
                    message.RespondError(ResponseStatus.ServiceUnavailable, MstErrorCodes.SPAWNER_MODULE_STOPPING);
                    return Task.CompletedTask;
                }

                // Check if peer has permissions to register spawner
                if (!HasCreationPermissions(message.Peer))
                {
                    message.RespondError(ResponseStatus.Unauthorized,
                        MstErrorCodes.SPAWNER_REGISTRATION_PERMISSION_DENIED);
                    return Task.CompletedTask;
                }

                // Read options
                var options = message.AsPacket<SpawnerOptions>();

                // Create new spawner
                var spawner = CreateSpawner(message.Peer, options);

                if (spawner == null)
                {
                    if (!message.Peer.IsConnected)
                    {
                        message.RespondError(ResponseStatus.NotConnected,
                            MstErrorCodes.SPAWNER_REGISTRATION_DISCONNECTED);
                    }
                    else if (!IsAcceptingSpawnerWork())
                    {
                        message.RespondError(ResponseStatus.ServiceUnavailable,
                            MstErrorCodes.SPAWNER_MODULE_STOPPING);
                    }
                    else
                    {
                        message.RespondError(ResponseStatus.Conflict,
                            MstErrorCodes.SPAWNER_REGISTRATION_REJECTED);
                    }

                    return Task.CompletedTask;
                }

                logger.Debug($"Client [{message.Peer.Id}] was successfully registered as spawner [{spawner.SpawnerId}] with options: {options}");

                // Respond with spawner id
                message.Respond(spawner.SpawnerId, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        protected virtual Task UnregisterSpawnerRequestHandler(IIncomingMessage message)
        {
            try
            {
                int spawnerId = message.AsInt();

                if (!spawnersList.TryGetValue(spawnerId, out RegisteredSpawner spawner))
                {
                    message.RespondError(ResponseStatus.NotFound,
                        MstErrorCodes.SPAWNER_REGISTRATION_NOT_FOUND);
                    return Task.CompletedTask;
                }

                if (!ReferenceEquals(spawner.Peer, message.Peer))
                {
                    logger.Warn($"Peer [{message.Peer.Id}] tried to unregister spawner [{spawnerId}] owned by another peer");
                    message.RespondError(ResponseStatus.Forbidden,
                        MstErrorCodes.SPAWNER_REGISTRATION_OWNER_REQUIRED);
                    return Task.CompletedTask;
                }

                if (!TryDestroySpawner(spawner))
                {
                    message.RespondError(ResponseStatus.NotFound,
                        MstErrorCodes.SPAWNER_REGISTRATION_NOT_FOUND);
                    return Task.CompletedTask;
                }

                message.Respond(ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        /// <summary>
        /// Handles a message from spawned process. Spawned process send this message
        /// to notify server that it was started
        /// </summary>
        /// <param name="message"></param>
        protected virtual Task RegisterSpawnedProcessRequestHandler(IIncomingMessage message)
        {
            try
            {
                var security = message.Peer.GetExtension<SecurityInfoPeerExtension>();

                if (security == null ||
                    (!security.HasPermission(MstPermissionKeys.RoomServer) &&
                     !security.HasAccountPermission(MstPermissionLevels.Admin)))
                {
                    message.RespondError(ResponseStatus.Unauthorized,
                        MstErrorCodes.SPAWNED_PROCESS_PERMISSION_DENIED);
                    logger.Warn($"Peer [{message.Peer.Id}] tried to register a spawned process without '{MstPermissionKeys.RoomServer}' permission");
                    return Task.CompletedTask;
                }

                var data = message.AsPacket<RegisterSpawnedProcessPacket>();

                // Try get spawn task by ID
                if (!spawnTasksList.TryGetValue(data.SpawnId, out SpawnTask task))
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.SPAWN_TASK_NOT_FOUND);
                    logger.Error("Process tried to register to an unknown task");
                    return Task.CompletedTask;
                }

                // Check spawn task unique code
                if (task.UniqueCode != data.SpawnCode)
                {
                    message.RespondError(ResponseStatus.Unauthorized, MstErrorCodes.SPAWN_CODE_INVALID);
                    logger.Error("Spawned process tried to register, but failed due to mismaching unique code");
                    return Task.CompletedTask;
                }

                if (!task.TryRegister(message.Peer))
                {
                    message.RespondError(ResponseStatus.Conflict,
                        MstErrorCodes.SPAWN_TASK_ALREADY_REGISTERED);
                    return Task.CompletedTask;
                }

                SpawnedProcessRegistrationHandler registeredHandlers = OnSpawnedProcessRegisteredEvent;

                if (registeredHandlers != null)
                {
                    foreach (SpawnedProcessRegistrationHandler handler in registeredHandlers.GetInvocationList())
                    {
                        try
                        {
                            handler.Invoke(task, message.Peer);
                        }
                        catch (Exception exception)
                        {
                            logger.Error($"Spawned process registration subscriber failed for task {task.Id}: {exception}");
                        }
                    }
                }

                // Respon to requester
                message.Respond(task.Options.ToBytes(), ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        protected virtual Task CompleteSpawnProcessRequestHandler(IIncomingMessage message)
        {
            try
            {
                var data = message.AsPacket<SpawnFinalizationPacket>();

                if (spawnTasksList.TryGetValue(data.SpawnTaskId, out SpawnTask task))
                {
                    if (task.RegisteredPeer != message.Peer)
                    {
                        message.RespondError(ResponseStatus.Forbidden,
                            MstErrorCodes.SPAWN_TASK_OWNER_REQUIRED);
                        logger.Error("Spawned process tried to complete spawn task, but it's not the same peer who registered to the task");
                    }
                    else
                    {
                        if (task.TryFinalize(data))
                        {
                            message.Respond(ResponseStatus.Success);
                        }
                        else
                        {
                            message.RespondError(ResponseStatus.Conflict,
                                MstErrorCodes.SPAWN_TASK_ALREADY_COMPLETED);
                        }
                    }
                }
                else
                {
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.SPAWN_TASK_NOT_FOUND);
                    logger.Error("Process tried to complete to an unknown task");
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        protected virtual Task SetProcessKilledRequestHandler(IIncomingMessage message)
        {
            try
            {
                var spawnId = message.AsInt();

                if (spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
                {
                    if (message.Peer != task.Spawner.Peer)
                    {
                        logger.Warn($"Peer [{message.Peer.Id}] tried to report a killed process for task [{spawnId}] owned by another spawner");
                        return Task.CompletedTask;
                    }

                    CompleteKilledTask(task.Spawner, task);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex) ;
            }
        }

        protected virtual Task SetProcessStartedRequestHandler(IIncomingMessage message)
        {
            try
            {
                var spawnId = message.AsInt();

                if (spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
                {
                    if (message.Peer != task.Spawner.Peer)
                    {
                        logger.Warn($"Peer [{message.Peer.Id}] tried to report a started process for task [{spawnId}] owned by another spawner");
                        return Task.CompletedTask;
                    }

                    task.Spawner.TryMarkProcessStarted(task);
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        protected virtual Task SetSpawnedProcessesCountRequestHandler(IIncomingMessage message)
        {
            try
            {
                var packet = message.AsPacket<IntPairPacket>();

                if (spawnersList.TryGetValue(packet.A, out RegisteredSpawner spawner))
                {
                    if (message.Peer == spawner.Peer)
                    {
                        spawner.UpdateProcessesCount(packet.B);
                    }
                    else
                    {
                        logger.Warn($"Peer [{message.Peer.Id}] tried to update process count for spawner [{packet.A}] owned by another peer");
                    }
                }

                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        #endregion
    }
}
