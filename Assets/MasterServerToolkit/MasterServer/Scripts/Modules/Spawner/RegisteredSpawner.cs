using MasterServerToolkit.DebounceThrottle;
using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public enum RegisteredSpawnerLifecycleState
    {
        Active,
        Closing,
        Closed
    }

    public class RegisteredSpawner
    {
        public delegate void KillRequestCallback(ResponseStatus status);

        private readonly object lifecycleGate = new object();
        private readonly Queue<SpawnTask> queue;
        private readonly HashSet<SpawnTask> beingSpawned;
        private readonly HashSet<SpawnTask> allTasks;
        private readonly HashSet<SpawnTask> dispatchedTasks;
        private readonly HashSet<SpawnTask> runningTasks;
        private readonly Queue<Action> pendingLifecycleEvents;
        private readonly Logger logger;
        private readonly ThrottleDispatcher spawnRequestThrottleDispatcher;
        private readonly int maxConcurrentRequests;
        private readonly TaskCompletionSource<bool> closeCompletionSource =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private RegisteredSpawnerLifecycleState lifecycleState = RegisteredSpawnerLifecycleState.Active;
        private bool isPublishingLifecycleEvents;
        private int processesRunning;

        public static int MaxConcurrentRequests { get; set; } = 8;
        public static int SpawnRequestThrottleIntervalMs { get; set; } = 100;
        public int SpawnerId { get; set; }
        public IPeer Peer { get; set; }
        public SpawnerOptions Options { get; set; }

        /// <summary>
        /// Number of processes currently considered running by this spawner.
        /// </summary>
        public int ProcessesRunning
        {
            get
            {
                lock (lifecycleGate)
                    return processesRunning;
            }
        }

        /// <summary>
        /// Returns true while this spawner accepts new tasks and lifecycle mutations.
        /// </summary>
        public bool IsActive
        {
            get
            {
                lock (lifecycleGate)
                    return lifecycleState == RegisteredSpawnerLifecycleState.Active;
            }
        }

        /// <summary>
        /// Current lifecycle state of this registered spawner.
        /// </summary>
        public RegisteredSpawnerLifecycleState LifecycleState
        {
            get
            {
                lock (lifecycleGate)
                    return lifecycleState;
            }
        }

        /// <summary>
        /// Completes when all tracked tasks have reached a terminal state or shutdown supervision is abandoned.
        /// </summary>
        public Task CloseCompletion => closeCompletionSource.Task;

        public RegisteredSpawner(int spawnerId, IPeer peer, SpawnerOptions options, Logger logger)
            : this(spawnerId, peer, options, logger, MaxConcurrentRequests, SpawnRequestThrottleIntervalMs)
        {
        }

        public RegisteredSpawner(int spawnerId, IPeer peer, SpawnerOptions options, Logger logger,
            int maxConcurrentRequests, int spawnRequestThrottleIntervalMs)
        {
            this.logger = logger;
            queue = new Queue<SpawnTask>();
            beingSpawned = new HashSet<SpawnTask>();
            allTasks = new HashSet<SpawnTask>();
            dispatchedTasks = new HashSet<SpawnTask>();
            runningTasks = new HashSet<SpawnTask>();
            pendingLifecycleEvents = new Queue<Action>();
            this.maxConcurrentRequests = maxConcurrentRequests <= 0 ? 1 : maxConcurrentRequests;
            spawnRequestThrottleDispatcher = new ThrottleDispatcher(
                spawnRequestThrottleIntervalMs < 0 ? 0 : spawnRequestThrottleIntervalMs);

            SpawnerId = spawnerId;
            Peer = peer;
            Options = options;
        }

        public int CalculateFreeSlotsCount()
        {
            lock (lifecycleGate)
                return CalculateFreeSlotsCountLocked();
        }

        public bool CanSpawnAnotherProcess()
        {
            lock (lifecycleGate)
                return CanReserveAnotherProcessLocked();
        }

        /// <summary>
        /// Atomically checks spawner capacity and reserves a queue slot for the task.
        /// </summary>
        public bool TryReserveAndEnqueue(SpawnTask task)
        {
            if (task == null || !ReferenceEquals(task.Spawner, this))
                return false;

            Action notification;

            lock (lifecycleGate)
            {
                if (lifecycleState != RegisteredSpawnerLifecycleState.Active ||
                    allTasks.Contains(task) ||
                    !CanReserveAnotherProcessLocked())
                    return false;

                if (!task.TryMarkQueuedDeferred(out notification))
                    return false;

                allTasks.Add(task);
                queue.Enqueue(task);
            }

            SpawnTask.InvokeDeferredNotification(notification);
            return true;
        }

        /// <summary>
        /// Legacy enqueue entry point. New owners should use <see cref="TryReserveAndEnqueue" />
        /// and handle a failed reservation explicitly.
        /// </summary>
        public void AddTaskToQueue(SpawnTask task)
        {
            if (!TryReserveAndEnqueue(task))
                Logs.Warn($"Spawner [{SpawnerId}] rejected spawn task [{task?.Id.ToString() ?? "null"}]");
        }

        /// <summary>
        /// Atomically aborts a tracked task. Queued tasks complete locally; dispatched tasks require a kill request.
        /// </summary>
        public bool TryAbortTask(SpawnTask task, out bool killRequired)
        {
            killRequired = false;

            if (task == null)
                return false;

            Action beginNotification;
            Action completionNotification = null;

            lock (lifecycleGate)
            {
                if (!allTasks.Contains(task))
                    return false;

                bool wasQueued = queue.Contains(task);
                bool canAbortLocally = wasQueued ||
                                       (beingSpawned.Contains(task) && !dispatchedTasks.Contains(task));

                if (!task.TryBeginAbortDeferred(out beginNotification))
                    return false;

                if (canAbortLocally)
                {
                    if (wasQueued)
                        RemoveQueuedTaskLocked(task);

                    task.TryCompleteAbortDeferred(out completionNotification);
                    beingSpawned.Remove(task);
                    dispatchedTasks.Remove(task);
                    allTasks.Remove(task);

                    if (runningTasks.Remove(task))
                        processesRunning = Math.Max(0, processesRunning - 1);

                    TryCompleteCloseLocked();
                }
                else
                {
                    killRequired = true;
                }
            }

            SpawnTask.InvokeDeferredNotification(beginNotification);
            SpawnTask.InvokeDeferredNotification(completionNotification);
            return true;
        }

        public void UpdateQueue()
        {
            lock (lifecycleGate)
            {
                ReconcileLifecycleLocked();

                if (!CanDispatchAnotherTaskLocked())
                    return;
            }

            _ = spawnRequestThrottleDispatcher.ThrottleAsync(() =>
            {
                SendNextQueuedSpawnRequest();
                return Task.CompletedTask;
            });
        }

        private void SendNextQueuedSpawnRequest()
        {
            if (!TryTakeNextTaskForSpawn(out SpawnTask task, out Action notification))
                return;

            SpawnTask.InvokeDeferredNotification(notification);

            var data = new SpawnRequestPacket
            {
                SpawnerId = SpawnerId,
                Options = task.Options,
                SpawnTaskId = task.Id,
                SpawnTaskUniqueCode = task.UniqueCode
            };

            var message = MessageHelper.Create(MstOpCodes.SpawnProcessRequest, data);

            try
            {
                bool sent = TrySendTrackedSpawnRequest(task, message, (status, response) =>
                {
                    try
                    {
                        if (status == ResponseStatus.Success)
                            return;

                        task.Abort();
                        string errorCode = GetStructuredErrorCode(response);
                        logger.Error($"Spawn request was not handled. Status: {status} | Code: {errorCode}");
                    }
                    catch (Exception exception)
                    {
                        Logs.Error($"Spawner [{SpawnerId}] spawn response callback failed for task [{task.Id}]: {exception}");
                    }
                });

                if (!sent && IsActive)
                    task.Abort();
            }
            catch (Exception exception)
            {
                task.Abort();
                Logs.Error($"Spawner [{SpawnerId}] failed to send spawn task [{task.Id}]: {exception}");
            }
        }

        private bool TrySendTrackedSpawnRequest(SpawnTask task, IOutgoingMessage message,
            ResponseCallback responseCallback)
        {
            lock (lifecycleGate)
            {
                if (lifecycleState != RegisteredSpawnerLifecycleState.Active ||
                    Peer == null ||
                    !Peer.IsConnected ||
                    !allTasks.Contains(task) ||
                    !beingSpawned.Contains(task))
                {
                    return false;
                }

                dispatchedTasks.Add(task);

                try
                {
                    Peer.SendMessage(message, responseCallback);
                    return true;
                }
                catch
                {
                    dispatchedTasks.Remove(task);
                    throw;
                }
            }
        }

        private bool TryTakeNextTaskForSpawn(out SpawnTask task, out Action notification)
        {
            task = null;
            notification = null;

            lock (lifecycleGate)
            {
                ReconcileLifecycleLocked();

                if (!CanDispatchAnotherTaskLocked())
                    return false;

                while (queue.Count > 0)
                {
                    SpawnTask candidate = queue.Dequeue();

                    if (!allTasks.Contains(candidate) || candidate.IsDoneStartingProcess)
                        continue;

                    if (!candidate.TryMarkStartingDeferred(out notification))
                        continue;

                    beingSpawned.Add(candidate);
                    task = candidate;
                    return true;
                }

                return false;
            }
        }

        private bool CanDispatchAnotherTaskLocked()
        {
            return lifecycleState == RegisteredSpawnerLifecycleState.Active &&
                   Peer != null &&
                   Peer.IsConnected &&
                   queue.Count > 0 &&
                   beingSpawned.Count < maxConcurrentRequests;
        }

        private void ReconcileLifecycleLocked()
        {
            beingSpawned.RemoveWhere(task => task.IsDoneStartingProcess || !allTasks.Contains(task));

            SpawnTask[] stoppedTasks = runningTasks
                .Where(task => !allTasks.Contains(task) || !task.IsProcessRunning)
                .ToArray();

            foreach (SpawnTask task in stoppedTasks)
            {
                if (runningTasks.Remove(task))
                    processesRunning = Math.Max(0, processesRunning - 1);
            }

            foreach (SpawnTask task in allTasks)
            {
                if (task.IsProcessRunning && runningTasks.Add(task))
                    processesRunning++;
            }

            SpawnTask[] completedTasks = allTasks
                .Where(task => !task.IsProcessRunning &&
                               (task.Status == SpawnStatus.Aborted || task.Status == SpawnStatus.Killed))
                .ToArray();

            foreach (SpawnTask task in completedTasks)
            {
                allTasks.Remove(task);
                beingSpawned.Remove(task);
                dispatchedTasks.Remove(task);
                RemoveQueuedTaskLocked(task);
            }

            TryCompleteCloseLocked();
        }

        /// <summary>
        /// Records the first process-start notification for a tracked task.
        /// </summary>
        public bool TryMarkProcessStarted(SpawnTask task)
        {
            if (task == null)
                return false;

            Action notification;
            bool changed;

            lock (lifecycleGate)
            {
                if (lifecycleState == RegisteredSpawnerLifecycleState.Closed || !allTasks.Contains(task))
                    return false;

                changed = task.TryMarkProcessStartedDeferred(out notification);

                if (changed)
                {
                    beingSpawned.Remove(task);
                    dispatchedTasks.Add(task);

                    if (runningTasks.Add(task))
                        processesRunning++;
                }
            }

            SpawnTask.InvokeDeferredNotification(notification);
            return changed;
        }

        /// <summary>
        /// Records the first process-killed notification for a tracked task.
        /// </summary>
        public bool TryMarkProcessKilled(SpawnTask task)
        {
            if (task == null)
                return false;

            Action notification;
            bool changed;

            lock (lifecycleGate)
            {
                if (lifecycleState == RegisteredSpawnerLifecycleState.Closed || !allTasks.Contains(task))
                    return false;

                changed = task.TryMarkProcessKilledDeferred(out bool wasRunning, out notification);

                if (changed)
                {
                    beingSpawned.Remove(task);
                    dispatchedTasks.Remove(task);

                    if (wasRunning && runningTasks.Remove(task))
                        processesRunning = Math.Max(0, processesRunning - 1);

                    allTasks.Remove(task);
                    RemoveQueuedTaskLocked(task);
                    TryCompleteCloseLocked();
                }
            }

            SpawnTask.InvokeDeferredNotification(notification);
            return changed;
        }

        /// <summary>
        /// Legacy recount hook used after <see cref="SpawnTask.OnProcessStarted" />.
        /// </summary>
        public void OnProcessStarted()
        {
            lock (lifecycleGate)
            {
                if (lifecycleState == RegisteredSpawnerLifecycleState.Closed)
                    return;

                ReconcileLifecycleLocked();
            }
        }

        /// <summary>
        /// Legacy recount hook used after <see cref="SpawnTask.OnProcessKilled" />.
        /// </summary>
        public void OnProcessKilled()
        {
            lock (lifecycleGate)
            {
                if (lifecycleState == RegisteredSpawnerLifecycleState.Closed)
                    return;

                ReconcileLifecycleLocked();
            }
        }

        public void UpdateProcessesCount(int count)
        {
            lock (lifecycleGate)
            {
                if (lifecycleState != RegisteredSpawnerLifecycleState.Active)
                    return;

                processesRunning = Math.Max(0, count);
            }
        }

        /// <summary>
        /// Removes a task from every collection owned by this spawner.
        /// </summary>
        public bool RemoveTask(SpawnTask task)
        {
            if (task == null)
                return false;

            lock (lifecycleGate)
            {
                bool removed = allTasks.Remove(task);
                removed |= beingSpawned.Remove(task);
                removed |= dispatchedTasks.Remove(task);

                if (runningTasks.Remove(task))
                {
                    processesRunning = Math.Max(0, processesRunning - 1);
                    removed = true;
                }

                removed |= RemoveQueuedTaskLocked(task);
                TryCompleteCloseLocked();
                return removed;
            }
        }

        /// <summary>
        /// Removes a task when the owner is shutting down and no longer accepts terminal notifications.
        /// The task keeps its last observed process status instead of being reported as killed.
        /// </summary>
        internal bool AbandonTaskSupervision(SpawnTask task)
        {
            if (task == null)
                return false;

            lock (lifecycleGate)
            {
                if (!allTasks.Contains(task))
                    return false;

                task.AbandonSupervision();
                allTasks.Remove(task);
                beingSpawned.Remove(task);
                dispatchedTasks.Remove(task);

                if (runningTasks.Remove(task))
                    processesRunning = Math.Max(0, processesRunning - 1);

                RemoveQueuedTaskLocked(task);
                TryCompleteCloseLocked();
                return true;
            }
        }

        /// <summary>
        /// Stops admission atomically and returns dispatched tasks that require remote kill confirmation.
        /// Tasks that have not been dispatched are aborted locally and removed immediately.
        /// </summary>
        public IReadOnlyList<SpawnTask> CloseAndGetTasksSnapshot()
        {
            SpawnTask[] liveTasks;
            var notifications = new List<Action>();

            lock (lifecycleGate)
            {
                if (lifecycleState == RegisteredSpawnerLifecycleState.Closed)
                    return Array.Empty<SpawnTask>();

                if (lifecycleState == RegisteredSpawnerLifecycleState.Closing)
                    return allTasks.ToArray();

                lifecycleState = RegisteredSpawnerLifecycleState.Closing;
                ReconcileLifecycleLocked();
                SpawnTask[] localTasks = allTasks
                    .Where(task => !dispatchedTasks.Contains(task) && !task.IsProcessStarted)
                    .ToArray();
                queue.Clear();

                foreach (SpawnTask task in localTasks)
                {
                    if (task.TryBeginAbortDeferred(out Action beginNotification) && beginNotification != null)
                        notifications.Add(beginNotification);

                    if (task.TryCompleteAbortDeferred(out Action completionNotification) &&
                        completionNotification != null)
                        notifications.Add(completionNotification);

                    beingSpawned.Remove(task);
                    dispatchedTasks.Remove(task);
                    allTasks.Remove(task);

                    if (runningTasks.Remove(task))
                        processesRunning = Math.Max(0, processesRunning - 1);
                }

                liveTasks = allTasks.ToArray();

                foreach (SpawnTask task in liveTasks)
                {
                    if (task.TryBeginAbortDeferred(out Action beginNotification) && beginNotification != null)
                        notifications.Add(beginNotification);
                }

                TryCompleteCloseLocked();
            }

            spawnRequestThrottleDispatcher.Cancel();

            foreach (Action notification in notifications)
                SpawnTask.InvokeDeferredNotification(notification);

            return liveTasks;
        }

        /// <summary>
        /// Ends shutdown supervision after the module's bounded wait expires without claiming that processes were killed.
        /// </summary>
        internal IReadOnlyList<SpawnTask> ForceCloseAfterTimeout()
        {
            SpawnTask[] abandonedTasks;
            var notifications = new List<Action>();

            lock (lifecycleGate)
            {
                if (lifecycleState == RegisteredSpawnerLifecycleState.Closed)
                    return Array.Empty<SpawnTask>();

                lifecycleState = RegisteredSpawnerLifecycleState.Closing;
                abandonedTasks = allTasks.ToArray();

                foreach (SpawnTask task in abandonedTasks)
                {
                    task.AbandonSupervision();

                    if (task.TryBeginAbortDeferred(out Action beginNotification) && beginNotification != null)
                        notifications.Add(beginNotification);

                    if (task.TryCompleteAbortDeferred(out Action completionNotification) &&
                        completionNotification != null)
                    {
                        notifications.Add(completionNotification);
                    }
                }

                queue.Clear();
                beingSpawned.Clear();
                dispatchedTasks.Clear();
                allTasks.Clear();
                runningTasks.Clear();
                processesRunning = 0;
                lifecycleState = RegisteredSpawnerLifecycleState.Closed;
                closeCompletionSource.TrySetResult(true);
            }

            spawnRequestThrottleDispatcher.Cancel();

            foreach (Action notification in notifications)
                SpawnTask.InvokeDeferredNotification(notification);

            return abandonedTasks;
        }

        internal bool QueueLifecycleEvent(Action lifecycleEvent)
        {
            if (lifecycleEvent == null)
                return false;

            lock (lifecycleGate)
            {
                pendingLifecycleEvents.Enqueue(lifecycleEvent);

                if (isPublishingLifecycleEvents)
                    return false;

                isPublishingLifecycleEvents = true;
                return true;
            }
        }

        internal void PublishPendingLifecycleEvents()
        {
            while (true)
            {
                Action lifecycleEvent;

                lock (lifecycleGate)
                {
                    if (pendingLifecycleEvents.Count == 0)
                    {
                        isPublishingLifecycleEvents = false;
                        return;
                    }

                    lifecycleEvent = pendingLifecycleEvents.Dequeue();
                }

                try
                {
                    lifecycleEvent.Invoke();
                }
                catch (Exception exception)
                {
                    logger.Error($"Spawner [{SpawnerId}] lifecycle event failed: {exception}");
                }
            }
        }

        public IReadOnlyList<SpawnTask> GetQueuedTasksSnapshot()
        {
            lock (lifecycleGate)
                return queue.ToArray();
        }

        private static string GetStructuredErrorCode(IIncomingMessage response)
        {
            if (response == null)
                return MstErrorCodes.UNKNOWN;

            try
            {
                MstProperties properties = MstProperties.FromBytes(response.AsBytes());
                return properties.AsString(MstErrorPropertyKeys.CODE, MstErrorCodes.UNKNOWN);
            }
            catch
            {
                return MstErrorCodes.RESPONSE_INVALID;
            }
        }

        public IReadOnlyList<SpawnTask> GetBeingSpawnedTasksSnapshot()
        {
            lock (lifecycleGate)
                return beingSpawned.ToArray();
        }

        public IReadOnlyList<SpawnTask> GetAllTasksSnapshot()
        {
            lock (lifecycleGate)
                return allTasks.ToArray();
        }

        /// <summary>
        /// Send request to kill process by given <paramref name="spawnId" />.
        /// </summary>
        public void SendKillRequest(int spawnId, KillRequestCallback callback)
        {
            var packet = new KillSpawnedProcessRequestPacket
            {
                SpawnerId = SpawnerId,
                SpawnId = spawnId
            };

            IPeer peer = Peer;

            if (peer == null || !peer.IsConnected)
            {
                callback?.Invoke(ResponseStatus.NotConnected);
                return;
            }

            try
            {
                peer.SendMessage(MstOpCodes.KillProcessRequest, packet, (status, response) =>
                {
                    try
                    {
                        callback?.Invoke(status);
                    }
                    catch (Exception exception)
                    {
                        Logs.Error($"Spawner [{SpawnerId}] kill response callback failed: {exception}");
                    }
                });
            }
            catch (Exception exception)
            {
                Logs.Error($"Spawner [{SpawnerId}] failed to send kill request for task [{spawnId}]: {exception}");

                try
                {
                    callback?.Invoke(ResponseStatus.Error);
                }
                catch (Exception callbackException)
                {
                    Logs.Error($"Spawner [{SpawnerId}] kill failure callback failed: {callbackException}");
                }
            }
        }

        private int CalculateFreeSlotsCountLocked()
        {
            if (Options.MaxProcesses == 0)
                return int.MaxValue;

            return Options.MaxProcesses - queue.Count - beingSpawned.Count - processesRunning;
        }

        private bool CanReserveAnotherProcessLocked()
        {
            if (lifecycleState != RegisteredSpawnerLifecycleState.Active)
                return false;

            return Options.MaxProcesses == 0 || CalculateFreeSlotsCountLocked() > 0;
        }

        private void TryCompleteCloseLocked()
        {
            if (lifecycleState != RegisteredSpawnerLifecycleState.Closing || allTasks.Count > 0)
                return;

            lifecycleState = RegisteredSpawnerLifecycleState.Closed;
            processesRunning = 0;
            closeCompletionSource.TrySetResult(true);
        }

        private bool RemoveQueuedTaskLocked(SpawnTask task)
        {
            if (queue.Count == 0)
                return false;

            bool removed = false;
            int count = queue.Count;

            for (int i = 0; i < count; i++)
            {
                SpawnTask queuedTask = queue.Dequeue();

                if (!removed && ReferenceEquals(queuedTask, task))
                {
                    removed = true;
                    continue;
                }

                queue.Enqueue(queuedTask);
            }

            return removed;
        }
    }
}
