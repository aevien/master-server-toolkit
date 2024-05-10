using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnersModule : BaseServerModule
    {
        public delegate void SpawnedProcessRegistrationHandler(SpawnTask task, IPeer peer);

        #region INSPECTOR

        [Header("Permissions"), SerializeField, Tooltip("Minimal permission level, necessary to register a spanwer")]
        protected int createSpawnerPermissionLevel = 0;

        [Tooltip("How often spawner queues are updated"), SerializeField]
        protected float queueUpdateFrequency = 0.1f;

        [Tooltip("If true, clients will be able to request spawns"), SerializeField]
        protected bool enableClientSpawnRequests = true;

        #endregion

        private int nextSpawnerId = 0;
        private int nextSpawnTaskId = 0;

        protected readonly ConcurrentDictionary<int, RegisteredSpawner> spawnersList = new ConcurrentDictionary<int, RegisteredSpawner>();
        protected readonly ConcurrentDictionary<int, SpawnTask> spawnTasksList = new ConcurrentDictionary<int, SpawnTask>();

        public event Action<RegisteredSpawner> OnSpawnerRegisteredEvent;
        public event Action<RegisteredSpawner> OnSpawnerDestroyedEvent;
        public event SpawnedProcessRegistrationHandler OnSpawnedProcessRegisteredEvent;

        public override void Initialize(IServer server)
        {
            // Add handlers
            server.RegisterMessageHandler(MstOpCodes.RegisterSpawner, RegisterSpawnerRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ClientsSpawnRequest, ClientsSpawnRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.RegisterSpawnedProcess, RegisterSpawnedProcessRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.CompleteSpawnProcess, CompleteSpawnProcessRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ProcessStarted, SetProcessStartedRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.ProcessKilled, SetProcessKilledRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.AbortSpawnRequest, AbortSpawnRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.GetSpawnFinalizationData, GetCompletionDataRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.UpdateSpawnerProcessesCount, SetSpawnedProcessesCountRequestHandler);

            // Coroutines
            StartCoroutine(StartQueueUpdater());
        }

        public override MstJson JsonInfo()
        {
            var data = base.JsonInfo();
            data.SetField("description", "This module manages the processes of running rooms.");
            data.SetField("totalSpawners", spawnersList.Count);

            int totalRooms = 0;

            MstJson spawners = new MstJson();

            foreach (var spawner in spawnersList.Values)
            {
                totalRooms += spawner.ProcessesRunning;

                var spawnerJson = new MstJson();
                spawnerJson.AddField("id", spawner.SpawnerId);
                spawnerJson.AddField("processes", spawner.ProcessesRunning);
                spawnerJson.AddField("processes", spawner.ProcessesRunning);

                var options = new MstJson();
                options.AddField("machineIp", spawner.Options.MachineIp);
                options.AddField("maxProcesses", spawner.Options.MaxProcesses);
                options.AddField("region", spawner.Options.Region);

                var customOptions = new MstJson();

                foreach (var option in spawner.Options.CustomOptions)
                    customOptions.AddField(option.Key, option.Value);

                options.AddField("customOptions", customOptions);

                spawnerJson.AddField("options", options);

                spawners.Add(spawnerJson);
            }

            data.SetField("totalStartedRooms", totalRooms);

            var allRegions = new MstJson();

            foreach (var region in GetRegions().Select(i => i.Name))
                allRegions.Add(region);

            data.SetField("allRegions", allRegions);
            data.SetField("maxConcurrentRequests", RegisteredSpawner.MaxConcurrentRequests);
            data.SetField("spawners", spawners);

            return data;
        }

        public override MstProperties Info()
        {
            int totalRooms = 0;

            var info = base.Info();
            info.Set("Description", "This module manages the processes of running rooms.");
            info.Set("Total spawners", spawnersList.Count);

            StringBuilder html = new StringBuilder();

            html.Append("<ol class=\"list-group list-group-numbered\">");

            foreach (var spawner in spawnersList.Values)
            {
                totalRooms += spawner.ProcessesRunning;

                var options = spawner.Options;

                html.Append("<li class=\"list-group-item\">");

                html.Append($"<b>SpawnerId:</b> {spawner.SpawnerId}, ");
                html.Append($"<b>Processes:</b> {spawner.ProcessesRunning}, ");
                html.Append($"<b>MachineIp:</b> {options.MachineIp}, ");
                html.Append($"<b>MaxProcesses:</b> {options.MaxProcesses}, ");
                html.Append($"<b>Region:</b> {options.Region}, ");
                html.Append($"<b>CustomOptions:</b> {options.CustomOptions}");

                html.Append("</li>");
            }

            html.Append("</ol>");

            info.Set("Total processes (Rooms)", totalRooms);
            info.Set("Total regions", string.Join(",", GetRegions().Select(i => i.Name)));
            info.Set("MaxConcurrentRequests", RegisteredSpawner.MaxConcurrentRequests);
            info.Set("Spawners Info", html.ToString());

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
            // Create registered spawner instance
            var spawnerInstance = new RegisteredSpawner(GenerateSpawnerId(), peer, options, logger);

            // Find spawners in peer property
            Dictionary<int, RegisteredSpawner> peerSpawners = peer.GetProperty(MstPeerPropertyCodes.RegisteredSpawners) as Dictionary<int, RegisteredSpawner>;

            // If this is the first time registering a spawners
            if (peerSpawners == null)
            {
                // Save the dictionary
                peerSpawners = new Dictionary<int, RegisteredSpawner>();
                peer.SetProperty(MstPeerPropertyCodes.RegisteredSpawners, peerSpawners);

                // Listen to disconnection
                peer.OnConnectionCloseEvent += OnRegisteredPeerDisconnect;
            }

            // Add a new spawner
            peerSpawners[spawnerInstance.SpawnerId] = spawnerInstance;

            // Add the spawner to a list of all spawners
            spawnersList[spawnerInstance.SpawnerId] = spawnerInstance;

            // Invoke the event
            OnSpawnerRegisteredEvent?.Invoke(spawnerInstance);

            return spawnerInstance;
        }

        /// <summary>
        /// Invokes when peer disconnected from server
        /// </summary>
        /// <param name="peer"></param>
        private void OnRegisteredPeerDisconnect(IPeer peer)
        {
            // Get registered spawners from peer property
            var peerSpawners = peer.GetProperty(MstPeerPropertyCodes.RegisteredSpawners) as Dictionary<int, RegisteredSpawner>;

            // Return if now spawner found
            if (peerSpawners == null)
            {
                return;
            }

            // Create a copy so that we can iterate safely
            var registeredSpawners = peerSpawners.Values.ToList();

            // Destroy all spawners
            foreach (var registeredSpawner in registeredSpawners)
            {
                DestroySpawner(registeredSpawner);
            }
        }

        /// <summary>
        /// Destroys spawner
        /// </summary>
        /// <param name="spawner"></param>
        public void DestroySpawner(RegisteredSpawner spawner)
        {
            // Get spawner owner peer
            var peer = spawner.Peer;

            // If peer exists
            if (peer != null)
            {
                // Get spawners from peer property
                var peerSpawners = peer.GetProperty(MstPeerPropertyCodes.RegisteredSpawners) as Dictionary<int, RegisteredSpawner>;

                // Remove the spawner from peer
                if (peerSpawners != null)
                    peerSpawners.Remove(spawner.SpawnerId);
            }

            // Remove the spawner from all spawners
            if (spawnersList.TryRemove(spawner.SpawnerId, out _))
                // Invoke the event
                OnSpawnerDestroyedEvent?.Invoke(spawner);
        }

        /// <summary>
        /// Creates unique spawner id
        /// </summary>
        /// <returns></returns>
        public int GenerateSpawnerId()
        {
            return nextSpawnerId++;
        }

        /// <summary>
        /// Creates unique spawner tsak id
        /// </summary>
        /// <returns></returns>
        public int GenerateSpawnTaskId()
        {
            return nextSpawnTaskId++;
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
            // Get registered spawner by options and region
            var spawners = GetFilteredSpawners(options, region);

            if (spawners.Count == 0)
            {
                logger.Warn($"No spawner was returned after filtering. Region: {options.AsString(Mst.Args.Names.RoomRegion, string.IsNullOrEmpty(region) ? "International" : region)}");
                return null;
            }

            // Order from least busy server
            var orderedSpawners = spawners.OrderByDescending(s => s.CalculateFreeSlotsCount());
            var availableSpawner = orderedSpawners.FirstOrDefault(s => s.CanSpawnAnotherProcess());

            // Ignore, if all of the spawners are busy
            if (availableSpawner == null)
            {
                return null;
            }

            return Spawn(options, availableSpawner);
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
            // Create new spawn task
            var task = new SpawnTask(GenerateSpawnTaskId(), spawner, options);

            // List this task
            spawnTasksList[task.Id] = task;

            // Add this task to queue
            spawner.AddTaskToQueue(task);

            logger.Debug($"Spawner was found, and spawn task created: {task}");

            return task;
        }

        /// <summary>
        /// Retrieves a list of spawner that can be used with given properties and region name
        /// </summary>
        /// <param name="properties"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        public virtual List<RegisteredSpawner> GetFilteredSpawners(MstProperties properties, string region)
        {
            return GetSpawners(region);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual List<RegisteredSpawner> GetSpawners()
        {
            return GetSpawners(null);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        public virtual List<RegisteredSpawner> GetSpawners(string region)
        {
            // If region is not provided, retrieve all spawners
            if (string.IsNullOrEmpty(region))
            {
                return spawnersList.Values.ToList();
            }

            return GetSpawnersInRegion(region);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="region"></param>
        /// <returns></returns>
        public virtual List<RegisteredSpawner> GetSpawnersInRegion(string region)
        {
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
            return extension != null && extension.PermissionLevel >= createSpawnerPermissionLevel;
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
                yield return new WaitForSeconds(queueUpdateFrequency);

                foreach (var spawner in spawnersList.Values)
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
        protected virtual void ClientsSpawnRequestHandler(IIncomingMessage message)
        {
            // Parse data from message
            var options = MstProperties.FromBytes(message.AsBytes());
            var peer = message.Peer;

            logger.Info($"Client {peer.Id} requested to spawn room with options: {options}");

            if (spawnersList.Count == 0)
            {
                logger.Error("But no registered spawner was found!");
                message.Respond("No registered spawner was found", ResponseStatus.Failed);
                return;
            }

            if (!CanClientSpawn(peer, options))
            {
                logger.Error("Unauthorized request");
                message.Respond(ResponseStatus.Unauthorized);
                return;
            }

            // Try to find existing request to prevent new one
            SpawnTask prevRequest = peer.GetProperty(MstPeerPropertyCodes.ClientSpawnRequest) as SpawnTask;

            if (prevRequest != null && !prevRequest.IsDoneStartingProcess)
            {
                logger.Warn("And he already has an active request");
                // Client has unfinished request
                message.Respond("You already have an active request", ResponseStatus.Failed);
                return;
            }

            // Create a new spawn task
            var task = Spawn(options, options.AsString(Mst.Args.Names.RoomRegion));

            // If spawn task is not created
            if (task == null)
            {
                logger.Warn("But all the servers are busy. Let him try again later");
                message.Respond("All the servers are busy. Try again later", ResponseStatus.Failed);
                return;
            }

            // Save spawn task requester
            task.Requester = peer;

            // Save the task as peer property
            peer.SetProperty(MstPeerPropertyCodes.ClientSpawnRequest, task);

            // Listen to status changes
            task.OnStatusChangedEvent += (status) =>
            {
                // Send status update
                var msg = Mst.Create.Message(MstOpCodes.SpawnRequestStatusChange, new SpawnStatusUpdatePacket()
                {
                    SpawnId = task.Id,
                    Status = status
                });

                if (task.Requester != null && task.Requester.IsConnected)
                {
                    peer.SendMessage(msg);
                }
            };

            message.Respond(task.Id, ResponseStatus.Success);
        }

        private void AbortSpawnRequestHandler(IIncomingMessage message)
        {
            SpawnTask prevRequest = message.Peer.GetProperty(MstPeerPropertyCodes.ClientSpawnRequest) as SpawnTask;

            if (prevRequest == null)
            {
                message.Respond("There's nothing to abort", ResponseStatus.Failed);
                return;
            }

            if (prevRequest.Status >= SpawnStatus.Finalized)
            {
                message.Respond("You can't abort a completed request", ResponseStatus.Failed);
                return;
            }

            if (prevRequest.Status <= SpawnStatus.None)
            {
                message.Respond("Already aborting", ResponseStatus.Success);
                return;
            }

            logger.Debug($"Client [{message.Peer.Id}] requested to terminate process [{prevRequest.Id}]");

            prevRequest.Abort();

            message.Respond(ResponseStatus.Success);
        }

        protected virtual void GetCompletionDataRequestHandler(IIncomingMessage message)
        {
            var spawnId = message.AsInt();

            if (!spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
            {
                message.Respond("Invalid request", ResponseStatus.Failed);
                return;
            }

            if (task.Requester != message.Peer)
            {
                message.Respond("You're not the requester", ResponseStatus.Unauthorized);
                return;
            }

            if (task.FinalizationPacket == null)
            {
                message.Respond("Task has no completion data", ResponseStatus.Failed);
                return;
            }

            // Respond with data (dictionary of strings)
            message.Respond(task.FinalizationPacket.FinalizationData.ToBytes(), ResponseStatus.Success);
        }

        protected virtual void RegisterSpawnerRequestHandler(IIncomingMessage message)
        {
            logger.Debug($"Client [{message.Peer.Id}] requested to be registered as spawner");

            // Check if peer has permissions to register spawner
            if (!HasCreationPermissions(message.Peer))
            {
                message.Respond("Insufficient permissions", ResponseStatus.Unauthorized);
                return;
            }

            // Read options
            var options = message.AsPacket<SpawnerOptions>();

            // Create new spawner
            var spawner = CreateSpawner(message.Peer, options);

            logger.Debug($"Client [{message.Peer.Id}] was successfully registered as spawner [{spawner.SpawnerId}] with options: {options}");

            // Respond with spawner id
            message.Respond(spawner.SpawnerId, ResponseStatus.Success);
        }

        /// <summary>
        /// Handles a message from spawned process. Spawned process send this message
        /// to notify server that it was started
        /// </summary>
        /// <param name="message"></param>
        protected virtual void RegisterSpawnedProcessRequestHandler(IIncomingMessage message)
        {
            var data = message.AsPacket<RegisterSpawnedProcessPacket>();

            // Try get spawn task by ID
            if (!spawnTasksList.TryGetValue(data.SpawnId, out SpawnTask task))
            {
                message.Respond("Invalid spawn task", ResponseStatus.Failed);
                logger.Error("Process tried to register to an unknown task");
                return;
            }

            // Check spawn task unique code
            if (task.UniqueCode != data.SpawnCode)
            {
                message.Respond("Unauthorized", ResponseStatus.Unauthorized);
                logger.Error("Spawned process tried to register, but failed due to mismaching unique code");
                return;
            }

            // Set task as registered
            task.OnRegistered(message.Peer);

            // Invoke event
            OnSpawnedProcessRegisteredEvent?.Invoke(task, message.Peer);

            // Respon to requester
            message.Respond(task.Options.ToBytes(), ResponseStatus.Success);
        }

        protected virtual void CompleteSpawnProcessRequestHandler(IIncomingMessage message)
        {
            var data = message.AsPacket<SpawnFinalizationPacket>();

            if (spawnTasksList.TryGetValue(data.SpawnTaskId, out SpawnTask task))
            {
                if (task.RegisteredPeer != message.Peer)
                {
                    message.Respond(ResponseStatus.Unauthorized);
                    logger.Error("Spawned process tried to complete spawn task, but it's not the same peer who registered to the task");
                }
                else
                {
                    task.OnFinalized(data);
                    message.Respond(ResponseStatus.Success);
                }
            }
            else
            {
                message.Respond(ResponseStatus.Invalid);
                logger.Error("Process tried to complete to an unknown task");
            }
        }

        protected virtual void SetProcessKilledRequestHandler(IIncomingMessage message)
        {
            var spawnId = message.AsInt();

            if (spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
            {
                task.OnProcessKilled();
                task.Spawner.OnProcessKilled();
            }
        }

        protected virtual void SetProcessStartedRequestHandler(IIncomingMessage message)
        {
            var spawnId = message.AsInt();

            if (spawnTasksList.TryGetValue(spawnId, out SpawnTask task))
            {
                task.OnProcessStarted();
                task.Spawner.OnProcessStarted();
            }
        }

        private void SetSpawnedProcessesCountRequestHandler(IIncomingMessage message)
        {
            var packet = message.AsPacket<IntPairPacket>();

            if (spawnersList.TryGetValue(packet.A, out RegisteredSpawner spawner))
            {
                spawner.UpdateProcessesCount(packet.B);
            }
        }

        #endregion
    }
}