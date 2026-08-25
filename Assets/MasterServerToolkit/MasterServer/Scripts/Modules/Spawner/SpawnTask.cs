using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents a spawn request and owns its thread-safe state transitions.
    /// </summary>
    public class SpawnTask
    {
        private const int DefaultKillConfirmationTimeoutMilliseconds = 5000;

        /// <summary>
        /// Gets how long an accepted kill request waits for terminal process confirmation
        /// before it is retried.
        /// </summary>
        public int KillConfirmationTimeoutMilliseconds { get; }

        private sealed class TransitionNotification
        {
            public SpawnStatus Status { get; set; }
            public Action<SpawnStatus>[] StatusCallbacks { get; set; }
            public Action<SpawnTask>[] DoneCallbacks { get; set; }
        }

        private sealed class StatusSubscription : IDisposable
        {
            private Action unsubscribe;

            public StatusSubscription(Action unsubscribeAction)
            {
                unsubscribe = unsubscribeAction;
            }

            public void Dispose()
            {
                Interlocked.Exchange(ref unsubscribe, null)?.Invoke();
            }
        }

        private readonly object stateGate = new object();
        private readonly List<Action<SpawnTask>> whenDoneCallbacks;
        private readonly Queue<TransitionNotification> pendingNotifications;
        private SpawnStatus status;
        private bool processStarted;
        private bool processKilled;
        private bool killRequestSent;
        private bool isSupervisionAbandoned;
        private bool isDispatchingNotifications;

        /// <summary>
        /// Id of current task.
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// Unique symbol code of current task.
        /// </summary>
        public string UniqueCode { get; private set; }

        /// <summary>
        /// Spawner assigned to current task.
        /// </summary>
        public RegisteredSpawner Spawner { get; private set; }

        /// <summary>
        /// Options assigned to current task.
        /// </summary>
        public MstProperties Options { get; private set; }

        /// <summary>
        /// Packet that has finalization info for current task.
        /// </summary>
        public SpawnFinalizationPacket FinalizationPacket
        {
            get
            {
                lock (stateGate)
                    return finalizationPacket;
            }
        }

        private SpawnFinalizationPacket finalizationPacket;

        /// <summary>
        /// Check if current task reached an aborted terminal state.
        /// </summary>
        public bool IsAborted
        {
            get
            {
                lock (stateGate)
                    return status == SpawnStatus.Aborted || status == SpawnStatus.Killed;
            }
        }

        /// <summary>
        /// Check if the operating-system process has been observed as started.
        /// </summary>
        public bool IsProcessStarted
        {
            get
            {
                lock (stateGate)
                    return processStarted;
            }
        }

        /// <summary>
        /// Check if the operating-system process is currently considered alive.
        /// </summary>
        public bool IsProcessRunning
        {
            get
            {
                lock (stateGate)
                    return processStarted && !processKilled;
            }
        }

        /// <summary>
        /// Check if starting the process has completed or failed. Aborting alone is not completion.
        /// </summary>
        public bool IsDoneStartingProcess
        {
            get
            {
                lock (stateGate)
                    return status >= SpawnStatus.WaitingForProcess ||
                           status == SpawnStatus.Aborted ||
                           status == SpawnStatus.Killed;
            }
        }

        /// <summary>
        /// Current spawn task status.
        /// </summary>
        public SpawnStatus Status
        {
            get
            {
                lock (stateGate)
                    return status;
            }
            set
            {
                TrySetStatus(value);
            }
        }

        /// <summary>
        /// Peer which first registered a started process for this task.
        /// </summary>
        public IPeer RegisteredPeer
        {
            get
            {
                lock (stateGate)
                    return registeredPeer;
            }
        }

        private IPeer registeredPeer;

        /// <summary>
        /// Who requested to spawn. Can be null.
        /// </summary>
        public IPeer Requester
        {
            get
            {
                lock (stateGate)
                    return requester;
            }
            set
            {
                lock (stateGate)
                    requester = value;
            }
        }

        private IPeer requester;

        /// <summary>
        /// Fired when spawn task status changed.
        /// </summary>
        public event Action<SpawnStatus> OnStatusChangedEvent
        {
            add
            {
                lock (stateGate)
                    statusChangedCallbacks += value;
            }
            remove
            {
                lock (stateGate)
                    statusChangedCallbacks -= value;
            }
        }

        private Action<SpawnStatus> statusChangedCallbacks;

        public SpawnTask(int spawnTaskId, RegisteredSpawner spawner, MstProperties options,
            int killConfirmationTimeoutMilliseconds = DefaultKillConfirmationTimeoutMilliseconds)
        {
            Id = spawnTaskId;
            Spawner = spawner;
            Options = options;
            UniqueCode = Mst.Helper.CreateRandomAlphanumericString(6);
            whenDoneCallbacks = new List<Action<SpawnTask>>();
            pendingNotifications = new Queue<TransitionNotification>();
            KillConfirmationTimeoutMilliseconds = Math.Max(1, killConfirmationTimeoutMilliseconds);
        }

        /// <summary>
        /// Atomically subscribes to status changes and queues the current status for the subscriber.
        /// Dispose the returned token to remove exactly this subscription.
        /// </summary>
        public IDisposable SubscribeStatusChanged(Action<SpawnStatus> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            Action<SpawnStatus> registeredCallback = status => callback(status);

            lock (stateGate)
            {
                statusChangedCallbacks += registeredCallback;
                pendingNotifications.Enqueue(new TransitionNotification
                {
                    Status = status,
                    StatusCallbacks = new[] { registeredCallback }
                });
            }

            var subscription = new StatusSubscription(
                () => UnsubscribeStatusChanged(registeredCallback));
            DispatchPendingNotifications();
            return subscription;
        }

        /// <summary>
        /// Atomically changes the status when the transition is legal.
        /// </summary>
        public bool TrySetStatus(SpawnStatus nextStatus)
        {
            bool changed = TrySetStatusDeferred(nextStatus, out Action notification);
            InvokeDeferredNotification(notification);
            return changed;
        }

        internal bool TryMarkQueuedDeferred(out Action notification)
        {
            return TrySetStatusDeferred(SpawnStatus.InQueue, out notification);
        }

        internal bool TryMarkStartingDeferred(out Action notification)
        {
            return TrySetStatusDeferred(SpawnStatus.StartingProcess, out notification);
        }

        /// <summary>
        /// Marks the process as started once.
        /// </summary>
        public bool TryMarkProcessStarted()
        {
            bool changed = TryMarkProcessStartedDeferred(out Action notification);
            InvokeDeferredNotification(notification);
            return changed;
        }

        internal bool TryMarkProcessStartedDeferred(out Action notification)
        {
            TransitionNotification transitionNotification = null;

            lock (stateGate)
            {
                if (processStarted || processKilled)
                {
                    notification = null;
                    return false;
                }

                processStarted = true;

                if (!IsTerminalStatus(status) && status != SpawnStatus.Aborting && status < SpawnStatus.WaitingForProcess)
                    TrySetStatusLocked(SpawnStatus.WaitingForProcess, out transitionNotification);
            }

            notification = CreateDeferredNotification(transitionNotification);
            return true;
        }

        /// <summary>
        /// Marks the process as killed once.
        /// </summary>
        public bool TryMarkProcessKilled()
        {
            bool changed = TryMarkProcessKilledDeferred(out _, out Action notification);
            InvokeDeferredNotification(notification);
            return changed;
        }

        internal bool TryMarkProcessKilledDeferred(out bool wasRunning, out Action notification)
        {
            TransitionNotification transitionNotification = null;

            lock (stateGate)
            {
                if (processKilled)
                {
                    wasRunning = false;
                    notification = null;
                    return false;
                }

                wasRunning = processStarted;
                processKilled = true;

                if (status != SpawnStatus.Killed && status != SpawnStatus.Aborted)
                    TrySetStatusLocked(SpawnStatus.Killed, out transitionNotification);
            }

            notification = CreateDeferredNotification(transitionNotification);
            return true;
        }

        /// <summary>
        /// Call when process is started.
        /// </summary>
        public void OnProcessStarted()
        {
            TryMarkProcessStarted();
        }

        /// <summary>
        /// Call when process is killed.
        /// </summary>
        public void OnProcessKilled()
        {
            TryMarkProcessKilled();
        }

        /// <summary>
        /// Registers the first process peer. Later peers are rejected.
        /// </summary>
        public bool TryRegister(IPeer peerWhoRegistered)
        {
            if (peerWhoRegistered == null)
                return false;

            TransitionNotification transitionNotification = null;

            lock (stateGate)
            {
                if (registeredPeer != null || IsTerminalStatus(status) || status == SpawnStatus.Aborting)
                    return false;

                registeredPeer = peerWhoRegistered;

                if (status < SpawnStatus.ProcessRegistered)
                    TrySetStatusLocked(SpawnStatus.ProcessRegistered, out transitionNotification);
            }

            InvokeDeferredNotification(CreateDeferredNotification(transitionNotification));
            return true;
        }

        /// <summary>
        /// Call when process is registered.
        /// </summary>
        public void OnRegistered(IPeer peerWhoRegistered)
        {
            TryRegister(peerWhoRegistered);
        }

        /// <summary>
        /// Stores the first finalization packet and finalizes the task once.
        /// </summary>
        public bool TryFinalize(SpawnFinalizationPacket packet)
        {
            if (packet == null)
                return false;

            TransitionNotification transitionNotification;

            lock (stateGate)
            {
                if (finalizationPacket != null || IsTerminalStatus(status) || status == SpawnStatus.Aborting)
                    return false;

                finalizationPacket = packet;

                if (!TrySetStatusLocked(SpawnStatus.Finalized, out transitionNotification))
                {
                    finalizationPacket = null;
                    return false;
                }
            }

            InvokeDeferredNotification(CreateDeferredNotification(transitionNotification));
            return true;
        }

        /// <summary>
        /// Call when process is finalized.
        /// </summary>
        public void OnFinalized(SpawnFinalizationPacket packet)
        {
            TryFinalize(packet);
        }

        /// <summary>
        /// Callback is called exactly once when the task reaches a terminal state.
        /// A late subscription is invoked immediately.
        /// </summary>
        public SpawnTask WhenDone(Action<SpawnTask> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            bool invokeImmediately;

            lock (stateGate)
            {
                invokeImmediately = IsTerminalStatus(status);

                if (!invokeImmediately)
                    whenDoneCallbacks.Add(callback);
            }

            if (invokeImmediately)
                InvokeDoneCallback(callback);

            return this;
        }

        /// <summary>
        /// Call to abort a process that is not finalized.
        /// </summary>
        public void Abort()
        {
            if (Spawner != null)
            {
                if (Status == SpawnStatus.Aborting)
                {
                    SendKillRequest();
                    return;
                }

                if (!Spawner.TryAbortTask(this, out bool killRequired))
                    return;

                if (killRequired)
                    SendKillRequest();

                return;
            }

            if (TryBeginAbort())
                SendKillRequest();
        }

        /// <summary>
        /// Call to kill a spawned process.
        /// </summary>
        public void KillSpawnedProcess()
        {
            if (Status != SpawnStatus.Finalized)
            {
                Abort();

                if (Status != SpawnStatus.Finalized)
                    return;
            }

            SendKillRequest();
        }

        internal bool TryBeginAbortDeferred(out Action notification)
        {
            return TrySetStatusDeferred(SpawnStatus.Aborting, out notification);
        }

        internal bool TryCompleteAbortDeferred(out Action notification)
        {
            return TrySetStatusDeferred(SpawnStatus.Aborted, out notification);
        }

        /// <summary>
        /// Stops remote kill retries when the owning module can no longer observe process lifecycle.
        /// This deliberately does not claim that the operating-system process was killed.
        /// </summary>
        internal void AbandonSupervision()
        {
            lock (stateGate)
            {
                isSupervisionAbandoned = true;
                killRequestSent = false;
            }
        }

        private bool TryBeginAbort()
        {
            bool changed = TryBeginAbortDeferred(out Action notification);
            InvokeDeferredNotification(notification);
            return changed;
        }

        private void SendKillRequest()
        {
            if (Spawner == null || !TryReserveKillRequest())
                return;

            try
            {
                Spawner.SendKillRequest(Id, responseStatus =>
                {
                    if (responseStatus == ResponseStatus.Success)
                    {
                        ScheduleKillConfirmationRetry();
                        return;
                    }

                    if (responseStatus == ResponseStatus.NotFound)
                    {
                        Spawner.TryMarkProcessKilled(this);
                        return;
                    }

                    ReleaseKillRequestReservation();
                    ScheduleKillRetry();
                    Logs.Warn($"Spawned process for task [{Id}] might not have been killed. Status: {responseStatus}");
                });
            }
            catch (Exception exception)
            {
                ReleaseKillRequestReservation();
                ScheduleKillRetry();
                Logs.Error($"Failed to send a kill request for spawn task [{Id}]: {exception}");
            }
        }

        private bool TryReserveKillRequest()
        {
            lock (stateGate)
            {
                if (killRequestSent || processKilled || isSupervisionAbandoned)
                    return false;

                killRequestSent = true;
                return true;
            }
        }

        private void ReleaseKillRequestReservation()
        {
            lock (stateGate)
            {
                if (!processKilled)
                    killRequestSent = false;
            }
        }

        private void ScheduleKillConfirmationRetry()
        {
            lock (stateGate)
            {
                if (processKilled || isSupervisionAbandoned)
                    return;
            }

            _ = WaitForKillConfirmationAndRetryAsync();
        }

        private void ScheduleKillRetry()
        {
            lock (stateGate)
            {
                if (processKilled || isSupervisionAbandoned)
                    return;
            }

            _ = WaitAndRetryKillAsync();
        }

        private async Task WaitAndRetryKillAsync()
        {
            try
            {
                await Task.Delay(KillConfirmationTimeoutMilliseconds).ConfigureAwait(false);

                bool shouldRetry;

                lock (stateGate)
                {
                    shouldRetry = !processKilled &&
                                  !isSupervisionAbandoned &&
                                  (status == SpawnStatus.Aborting || status == SpawnStatus.Finalized);
                }

                if (shouldRetry)
                    SendKillRequest();
            }
            catch (Exception exception)
            {
                Logs.Error($"Kill retry failed for spawn task [{Id}]: {exception}");
            }
        }

        private async Task WaitForKillConfirmationAndRetryAsync()
        {
            try
            {
                await Task.Delay(KillConfirmationTimeoutMilliseconds).ConfigureAwait(false);

                bool shouldRetry;

                lock (stateGate)
                {
                    shouldRetry = killRequestSent &&
                                  !processKilled &&
                                  !isSupervisionAbandoned &&
                                  (status == SpawnStatus.Aborting || status == SpawnStatus.Finalized);

                    if (shouldRetry)
                        killRequestSent = false;
                }

                if (shouldRetry)
                    SendKillRequest();
            }
            catch (Exception exception)
            {
                ReleaseKillRequestReservation();
                Logs.Error($"Kill confirmation watchdog failed for spawn task [{Id}]: {exception}");
            }
        }

        private bool TrySetStatusDeferred(SpawnStatus nextStatus, out Action notification)
        {
            TransitionNotification transitionNotification;
            bool changed;

            lock (stateGate)
                changed = TrySetStatusLocked(nextStatus, out transitionNotification);

            notification = CreateDeferredNotification(transitionNotification);
            return changed;
        }

        private bool TrySetStatusLocked(SpawnStatus nextStatus, out TransitionNotification notification)
        {
            notification = null;

            if (!CanTransitionLocked(nextStatus))
                return false;

            status = nextStatus;
            notification = CaptureNotificationLocked(nextStatus);
            pendingNotifications.Enqueue(notification);
            return true;
        }

        private bool CanTransitionLocked(SpawnStatus nextStatus)
        {
            if (nextStatus == status || status == SpawnStatus.Killed || status == SpawnStatus.Aborted)
                return false;

            if (status == SpawnStatus.Finalized)
                return nextStatus == SpawnStatus.Killed;

            if (nextStatus == SpawnStatus.Aborting)
                return status >= SpawnStatus.None;

            if (nextStatus == SpawnStatus.Aborted)
                return status == SpawnStatus.Aborting;

            if (nextStatus == SpawnStatus.Killed)
                return true;

            if (status == SpawnStatus.Aborting || nextStatus < SpawnStatus.None)
                return false;

            return nextStatus > status;
        }

        private TransitionNotification CaptureNotificationLocked(SpawnStatus nextStatus)
        {
            return new TransitionNotification
            {
                Status = nextStatus,
                StatusCallbacks = GetStatusCallbacksLocked(),
                DoneCallbacks = IsTerminalStatus(nextStatus) ? DrainDoneCallbacksLocked() : null
            };
        }

        private Action<SpawnStatus>[] GetStatusCallbacksLocked()
        {
            Delegate[] invocationList = statusChangedCallbacks?.GetInvocationList();

            if (invocationList == null || invocationList.Length == 0)
                return null;

            var callbacks = new Action<SpawnStatus>[invocationList.Length];

            for (int i = 0; i < invocationList.Length; i++)
                callbacks[i] = (Action<SpawnStatus>)invocationList[i];

            return callbacks;
        }

        private void UnsubscribeStatusChanged(Action<SpawnStatus> callback)
        {
            lock (stateGate)
                statusChangedCallbacks -= callback;
        }

        private Action<SpawnTask>[] DrainDoneCallbacksLocked()
        {
            if (whenDoneCallbacks.Count == 0)
                return null;

            Action<SpawnTask>[] callbacks = whenDoneCallbacks.ToArray();
            whenDoneCallbacks.Clear();
            return callbacks;
        }

        private Action CreateDeferredNotification(TransitionNotification notification)
        {
            return notification == null ? null : DispatchPendingNotifications;
        }

        internal static void InvokeDeferredNotification(Action notification)
        {
            notification?.Invoke();
        }

        private void DispatchPendingNotifications()
        {
            lock (stateGate)
            {
                if (isDispatchingNotifications || pendingNotifications.Count == 0)
                    return;

                isDispatchingNotifications = true;
            }

            while (true)
            {
                TransitionNotification notification;

                lock (stateGate)
                {
                    if (pendingNotifications.Count == 0)
                    {
                        isDispatchingNotifications = false;
                        return;
                    }

                    notification = pendingNotifications.Dequeue();
                }

                try
                {
                    InvokeTransitionNotification(notification);
                }
                catch (Exception exception)
                {
                    Logs.Error($"Spawn task [{Id}] notification dispatch failed: {exception}");
                }
            }
        }

        private void InvokeTransitionNotification(TransitionNotification notification)
        {
            if (notification == null)
                return;

            if (notification.StatusCallbacks != null)
            {
                foreach (Action<SpawnStatus> callback in notification.StatusCallbacks)
                {
                    try
                    {
                        callback.Invoke(notification.Status);
                    }
                    catch (Exception exception)
                    {
                        Logs.Error($"Spawn task [{Id}] status callback failed: {exception}");
                    }
                }
            }

            if (notification.DoneCallbacks != null)
            {
                foreach (Action<SpawnTask> callback in notification.DoneCallbacks)
                    InvokeDoneCallback(callback);
            }
        }

        private void InvokeDoneCallback(Action<SpawnTask> callback)
        {
            try
            {
                callback.Invoke(this);
            }
            catch (Exception exception)
            {
                Logs.Error($"Spawn task [{Id}] completion callback failed: {exception}");
            }
        }

        private static bool IsTerminalStatus(SpawnStatus value)
        {
            return value == SpawnStatus.Killed ||
                   value == SpawnStatus.Aborted ||
                   value == SpawnStatus.Finalized;
        }

        public override string ToString()
        {
            return $"[SpawnTask: id - {Id}]";
        }
    }
}
