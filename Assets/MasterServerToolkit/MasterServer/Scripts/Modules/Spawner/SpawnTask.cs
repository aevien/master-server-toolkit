using MasterServerToolkit.Logging;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents a spawn request, and manages the state of request
    /// from start to finalization
    /// </summary>
    public class SpawnTask
    {
        private SpawnStatus status;
        protected List<Action<SpawnTask>> whenDoneCallbacks;

        /// <summary>
        /// Id of current task
        /// </summary>
        public int Id { get; private set; }

        /// <summary>
        /// Unique symbol code of current task
        /// </summary>
        public string UniqueCode { get; private set; }

        /// <summary>
        /// Spawner assigned to current task
        /// </summary>
        public RegisteredSpawner Spawner { get; private set; }

        /// <summary>
        /// Options assigned to current task
        /// </summary>
        public MstProperties Options { get; private set; }

        /// <summary>
        /// Packet that has finalization info for current task
        /// </summary>
        public SpawnFinalizationPacket FinalizationPacket { get; private set; }

        /// <summary>
        /// Check if current task is aborted
        /// </summary>
        public bool IsAborted { get { return status < SpawnStatus.None; } }

        /// <summary>
        /// Check if spawn process is started
        /// </summary>
        public bool IsProcessStarted { get { return Status >= SpawnStatus.WaitingForProcess; } }

        /// <summary>
        /// Check is spawn start process is finished
        /// </summary>
        public bool IsDoneStartingProcess { get { return IsAborted || IsProcessStarted; } }

        /// <summary>
        /// Current spawn task status
        /// </summary>
        public SpawnStatus Status
        {
            get { return status; }
            set
            {
                status = value;

                OnStatusChangedEvent?.Invoke(status);

                if (status >= SpawnStatus.Finalized || status < SpawnStatus.None)
                {
                    NotifyDoneListeners();
                }
            }
        }

        /// <summary>
        /// Peer, who registered a started process for this task
        /// (for example, a game server)
        /// </summary>
        public IPeer RegisteredPeer { get; private set; }

        /// <summary>
        /// Who requested to spawn
        /// (most likely clients peer)
        /// Can be null
        /// </summary>
        public IPeer Requester { get; set; }

        /// <summary>
        /// Fired when spawn task status changed
        /// </summary>
        public event Action<SpawnStatus> OnStatusChangedEvent;

        public SpawnTask(int spawnTaskId, RegisteredSpawner spawner, MstProperties options)
        {
            Id = spawnTaskId;

            Spawner = spawner;
            Options = options;

            UniqueCode = Mst.Helper.CreateRandomAlphanumericString(6);
            whenDoneCallbacks = new List<Action<SpawnTask>>();
        }

        /// <summary>
        /// Call when process is siarted
        /// </summary>
        public void OnProcessStarted()
        {
            if (!IsAborted && Status < SpawnStatus.WaitingForProcess)
            {
                Status = SpawnStatus.WaitingForProcess;
            }
        }

        /// <summary>
        /// Call when process is killed
        /// </summary>
        public void OnProcessKilled()
        {
            Status = SpawnStatus.Killed;
        }

        /// <summary>
        /// Call when process is registered
        /// </summary>
        /// <param name="peerWhoRegistered"></param>
        public void OnRegistered(IPeer peerWhoRegistered)
        {
            RegisteredPeer = peerWhoRegistered;

            if (!IsAborted && Status < SpawnStatus.ProcessRegistered)
            {
                Status = SpawnStatus.ProcessRegistered;
            }
        }

        /// <summary>
        /// Call when processis finalized
        /// </summary>
        /// <param name="finalizationPacket"></param>
        public void OnFinalized(SpawnFinalizationPacket finalizationPacket)
        {
            FinalizationPacket = finalizationPacket;
            if (!IsAborted && Status < SpawnStatus.Finalized)
            {
                Status = SpawnStatus.Finalized;
            }
        }

        protected void NotifyDoneListeners()
        {
            foreach (var callback in whenDoneCallbacks)
            {
                callback.Invoke(this);
            }

            whenDoneCallbacks.Clear();
        }

        /// <summary>
        /// Callback will be called when spawn task is aborted or completed 
        /// (game server is opened)
        /// </summary>
        /// <param name="callback"></param>
        public SpawnTask WhenDone(Action<SpawnTask> callback)
        {
            whenDoneCallbacks.Add(callback);
            return this;
        }

        /// <summary>
        /// Call to abort spawned process that is not finalized
        /// </summary>
        public void Abort()
        {
            if (Status >= SpawnStatus.Finalized)
            {
                return;
            }

            Status = SpawnStatus.Aborting;

            KillSpawnedProcess();
        }

        /// <summary>
        /// Call to kill spawned process
        /// </summary>
        public void KillSpawnedProcess()
        {
            Spawner.SendKillRequest(Id, killed =>
            {
                Status = SpawnStatus.Aborted;

                if (!killed)
                {
                    Logs.Warn("Spawned Process might not have been killed");
                }
            });
        }

        public override string ToString()
        {
            return $"[SpawnTask: id - {Id}]";
        }
    }
}