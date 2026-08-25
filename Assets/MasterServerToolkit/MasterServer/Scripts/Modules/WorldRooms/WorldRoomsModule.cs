using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class WorldRoomsModule : RoomsModule
    {
        #region Unity Inspector

        [Header("Zones Settings"), SerializeField, Tooltip("Scene names treated as persistent world zones. Every entry is requested from the spawner when a spawner registers; do not leave entries empty, and match the room startup scene identifiers exactly.")]
        private string[] zoneScenes;

        #endregion

        protected SpawnersModule spawnersModule;
        private readonly object runSync = new object();
        private readonly HashSet<Task> pendingZoneStarts = new HashSet<Task>();
        private CancellationToken runCancellationToken;
        private long runGeneration;
        private bool isRunActive;

        protected override void Awake()
        {
            base.Awake();
            AddDependency<SpawnersModule>();
        }

        public override void Initialize(IServer server)
        {
            base.Initialize(server);

            spawnersModule = server.GetModule<SpawnersModule>();

            if (!spawnersModule)
            {
                logger.Error($"{GetType().Name} was set to use {nameof(SpawnersModule)}, but {nameof(SpawnersModule)} was not found");
            }

            server.RegisterMessageHandler(MstOpCodes.GetZoneRoomInfo, GetZoneRoomInfoMessageHandler);
        }

        public override void StartServerRun(CancellationToken runCancellationToken)
        {
            base.StartServerRun(runCancellationToken);

            lock (runSync)
            {
                this.runCancellationToken = runCancellationToken;
                runGeneration++;
                isRunActive = true;
            }

            if (spawnersModule)
            {
                spawnersModule.OnSpawnerRegisteredEvent -= Spawners_OnSpawnerRegisteredEvent;
                spawnersModule.OnSpawnerRegisteredEvent += Spawners_OnSpawnerRegisteredEvent;
            }
        }

        public override async Task StopServerRunAsync()
        {
            if (spawnersModule)
                spawnersModule.OnSpawnerRegisteredEvent -= Spawners_OnSpawnerRegisteredEvent;

            Task[] pendingTasks;

            lock (runSync)
            {
                isRunActive = false;
                runGeneration++;
                pendingTasks = pendingZoneStarts.ToArray();
            }

            // RoomsModule owns Unity Invoke state, so stop it on the caller's owner thread
            // before pending background zone scheduling is drained.
            Task roomsStopTask = base.StopServerRunAsync();

            try
            {
                if (pendingTasks.Length > 0)
                {
                    await Task.WhenAll(pendingTasks).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
                // Individual scheduling failures are logged by ObserveWorldZoneStartAsync.
            }
            finally
            {
                await roomsStopTask.ConfigureAwait(false);
            }
        }

        protected virtual void OnDestroy()
        {
            if (spawnersModule)
                spawnersModule.OnSpawnerRegisteredEvent -= Spawners_OnSpawnerRegisteredEvent;

            lock (runSync)
            {
                isRunActive = false;
                runGeneration++;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spawner"></param>
        private void Spawners_OnSpawnerRegisteredEvent(RegisteredSpawner spawner)
        {
            Task startTask;

            lock (runSync)
            {
                if (!isRunActive)
                    return;

                startTask = StartWorldZonesAsync(runGeneration, runCancellationToken);
                pendingZoneStarts.Add(startTask);
            }

            _ = ObserveWorldZoneStartAsync(startTask);
        }

        private async Task StartWorldZonesAsync(long generation, CancellationToken cancellationToken)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);

            foreach (string zoneScene in zoneScenes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lock (runSync)
                {
                    if (!isRunActive || generation != runGeneration)
                        return;
                }

                SpawnTask spawnTask = spawnersModule.Spawn(SpawnerProperties(zoneScene));

                if (spawnTask == null)
                {
                    logger.Warn($"World zone {zoneScene} was not scheduled because no spawner has capacity");
                    continue;
                }

                spawnTask.WhenDone(task => logger.Info($"{zoneScene} zone status is: {task.Status}"));
            }
        }

        private async Task ObserveWorldZoneStartAsync(Task startTask)
        {
            try
            {
                await startTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                logger.Error($"Failed to schedule world zones: {exception}");
            }
            finally
            {
                lock (runSync)
                    pendingZoneStarts.Remove(startTask);
            }
        }

        protected virtual MstProperties SpawnerProperties(string zoneId)
        {
            var properties = new MstProperties();
            properties.Set(Mst.Args.Names.RoomTitle, zoneId);
            properties.Set(Mst.Args.Names.RoomOnlineScene, zoneId);
            properties.Set(Mst.Args.Names.RoomIsPrivate, true);
            properties.Set(MstParamKeys.WORLD_ZONE, zoneId);

            return properties;
        }

        #region MESSAGE HANDLERS

        private Task GetZoneRoomInfoMessageHandler(IIncomingMessage message)
        {
            try
            {
                var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

                if (userExtension == null)
                {
                    message.RespondError(ResponseStatus.Unauthorized,
                        MstErrorCodes.USER_IS_NOT_LOGGED_IN);
                    return Task.CompletedTask;
                }

                string zoneId = message.AsString();

                RegisteredRoom zoneRoom = null;
                RoomOptions zoneOptions = null;
                IReadOnlyDictionary<int, IPeer> zonePlayers = null;
                int zoneOnlineCount = 0;

                foreach (RegisteredRoom candidate in roomsList.Values)
                {
                    if (!candidate.TryGetSnapshot(out RoomOptions candidateOptions,
                            out IReadOnlyDictionary<int, IPeer> candidatePlayers, out int candidateOnlineCount) ||
                        candidateOptions.ExtraParameters.AsString(MstParamKeys.WORLD_ZONE) != zoneId)
                    {
                        continue;
                    }

                    zoneRoom = candidate;
                    zoneOptions = candidateOptions;
                    zonePlayers = candidatePlayers;
                    zoneOnlineCount = candidateOnlineCount;
                    break;
                }

                if (zoneRoom == null)
                {
                    logger.Error($"No room found for zone {zoneId}");
                    var properties = new MstProperties();
                    properties.Set(MstErrorPropertyKeys.ZONE_ID, zoneId);
                    message.RespondError(ResponseStatus.NotFound, MstErrorCodes.WORLD_ZONE_NOT_FOUND,
                        properties);
                    return Task.CompletedTask;
                }

                var game = new GameInfoPacket
                {
                    Id = zoneRoom.RoomId,
                    Address = zoneOptions.RoomIp + ":" + zoneOptions.RoomPort,
                    MaxPlayers = zoneOptions.MaxPlayers,
                    Name = zoneOptions.Name,
                    OnlinePlayers = zoneOnlineCount,
                    Properties = GetPublicRoomOptions(message.Peer, zoneRoom, null, zoneOptions),
                    IsPasswordProtected = !string.IsNullOrEmpty(zoneOptions.Password),
                    Type = GameInfoType.Room,
                    Region = zoneOptions.Region
                };

                var players = zonePlayers.Values
                    .Where(player => player.HasExtension<IUserPeerExtension>())
                    .Select(player => player.GetExtension<IUserPeerExtension>().Username);
                game.OnlinePlayersList = players.ToList();

                message.Respond(game, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            catch (System.Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        #endregion
    }
}
