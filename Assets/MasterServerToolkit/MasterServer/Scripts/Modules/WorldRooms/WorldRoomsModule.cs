using MasterServerToolkit.Networking;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class WorldRoomsModule : RoomsModule
    {
        #region Unity Inspector

        [Header("Zones Settings"), SerializeField]
        private string[] zoneScenes;

        #endregion

        protected SpawnersModule spawnersModule;

        protected override void Awake()
        {
            base.Awake();
            AddDependency<SpawnersModule>();
        }

        public override void Initialize(IServer server)
        {
            base.Initialize(server);

            spawnersModule = server.GetModule<SpawnersModule>();

            if (spawnersModule)
            {
                spawnersModule.OnSpawnerRegisteredEvent += Spawners_OnSpawnerRegisteredEvent;
            }
            else
            {
                logger.Error($"{GetType().Name} was set to use {nameof(SpawnersModule)}, but {nameof(SpawnersModule)} was not found");
            }

            server.RegisterMessageHandler(MstOpCodes.GetZoneRoomInfo, GetZoneRoomInfoMessageHandler);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spawner"></param>
        private async void Spawners_OnSpawnerRegisteredEvent(RegisteredSpawner spawner)
        {
            await Task.Delay(100);

            foreach (string zoneScene in zoneScenes)
            {
                spawnersModule.Spawn(SpawnerProperties(zoneScene)).WhenDone(task =>
                {
                    logger.Info($"{zoneScene} zone status is: {task.Status}");
                });
            }
        }

        protected virtual MstProperties SpawnerProperties(string zoneId)
        {
            var properties = new MstProperties();
            properties.Set(Mst.Args.Names.RoomName, zoneId);
            properties.Set(Mst.Args.Names.RoomOnlineScene, zoneId);
            properties.Set(Mst.Args.Names.RoomIsPrivate, true);
            properties.Set(MstDictKeys.WORLD_ZONE, zoneId);

            return properties;
        }

        #region MESSAGE HANDLERS

        private void GetZoneRoomInfoMessageHandler(IIncomingMessage message)
        {
            var userExtension = message.Peer.GetExtension<IUserPeerExtension>();

            if (userExtension == null)
            {
                message.Respond(ResponseStatus.Unauthorized);
                return;
            }

            string zoneId = message.AsString();

            RegisteredRoom zoneRoom = roomsList.Values
                .Where(r => r.Options.CustomOptions.AsString(MstDictKeys.WORLD_ZONE) == zoneId)
                .FirstOrDefault();

            if (zoneRoom == null)
            {
                logger.Error($"No room found for zone {zoneId}");
                message.Respond(ResponseStatus.NotFound);
                return;
            }

            var game = new GameInfoPacket
            {
                Id = zoneRoom.RoomId,
                Address = zoneRoom.Options.RoomIp + ":" + zoneRoom.Options.RoomPort,
                MaxPlayers = zoneRoom.Options.MaxConnections,
                Name = zoneRoom.Options.Name,
                OnlinePlayers = zoneRoom.OnlineCount,
                Properties = GetPublicRoomOptions(message.Peer, zoneRoom, null),
                IsPasswordProtected = !string.IsNullOrEmpty(zoneRoom.Options.Password),
                Type = GameInfoType.Room,
                Region = zoneRoom.Options.Region
            };

            var players = zoneRoom.Players.Values.Where(pl => pl.HasExtension<IUserPeerExtension>()).Select(pl => pl.GetExtension<IUserPeerExtension>().Username);
            game.OnlinePlayersList = players.ToList();

            message.Respond(game, ResponseStatus.Success);
        }

        #endregion
    }
}