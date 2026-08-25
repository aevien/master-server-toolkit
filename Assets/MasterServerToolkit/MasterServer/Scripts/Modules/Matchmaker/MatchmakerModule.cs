using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class MatchmakerModule : BaseServerModule
    {
        [Header("Settings"), SerializeField, Tooltip("Uses SpawnersModule as the source of available regions. Disable when game search is required but no process spawner is installed; region-list requests then return an error.")]
        private bool useSpawnerModule = true;

        /// <summary>
        /// List of game providers
        /// </summary>
        public HashSet<IGamesProvider> GameProviders { get; protected set; }

        /// <summary>
        /// 
        /// </summary>
        protected SpawnersModule spawnersModule;

        protected override void Awake()
        {
            base.Awake();

            AddOptionalDependency<LobbiesModule>();
            AddOptionalDependency<RoomsModule>();

            if (useSpawnerModule)
                AddOptionalDependency<SpawnersModule>();
        }

        public override void Initialize(IServer server)
        {
            GameProviders = new HashSet<IGamesProvider>();

            var roomsModule = server.GetModule<RoomsModule>();
            var lobbiesModule = server.GetModule<LobbiesModule>();

            if (useSpawnerModule)
                spawnersModule = server.GetModule<SpawnersModule>();

            if (useSpawnerModule && !spawnersModule)
                logger.Error($"{GetType().Name} was set to use {nameof(SpawnersModule)}, but {nameof(SpawnersModule)} was not found." +
                    $"In this case, you will not be able to get regions list");

            // Dependencies
            if (roomsModule != null)
                AddProvider(roomsModule);

            if (lobbiesModule != null)
                AddProvider(lobbiesModule);

            // Add handlers
            server.RegisterMessageHandler(MstOpCodes.FindGamesRequest, FindGamesRequestHandler);
            server.RegisterMessageHandler(MstOpCodes.GetRegionsRequest, GetRegionsRequestHandler);
        }

        /// <summary>
        /// Add given provider to list
        /// </summary>
        /// <param name="provider"></param>
        public void AddProvider(IGamesProvider provider)
        {
            GameProviders.Add(provider);
        }

        #region INCOMING MESSAGES HANDLERS

        protected virtual Task FindGamesRequestHandler(IIncomingMessage message)
        {
            try
            {
                var list = new List<GameInfoPacket>();
                var filters = MstProperties.FromBytes(message.AsBytes());

                foreach (var game in GameProviders.SelectMany(pr => pr.GetPublicGames(message.Peer, filters), (provider, game) => game))
                {
                    list.Add(game);
                }

                // Convert to generic list and serialize to bytes
                var bytes = list.Select(game => (ISerializablePacket)game).ToBytes();
                message.Respond(bytes, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            // If we got another exception
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        protected virtual Task GetRegionsRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!spawnersModule)
                {
                    message.RespondError(ResponseStatus.ServiceUnavailable,
                        MstErrorCodes.MATCHMAKER_REGIONS_DISABLED);
                    logger.Error("No spawner module found");
                    return Task.CompletedTask;
                }

                var list = spawnersModule.GetRegions();

                if (list.Count == 0)
                {
                    message.RespondError(ResponseStatus.ServiceUnavailable,
                        MstErrorCodes.MATCHMAKER_REGIONS_UNAVAILABLE);
                    logger.Error("No spawner started");
                    return Task.CompletedTask;
                }

                message.Respond(new RegionsPacket()
                {
                    Regions = list
                }, ResponseStatus.Success);
                return Task.CompletedTask;
            }
            // If we got another exception
            catch (Exception ex)
            {
                return Task.FromException(ex);
            }
        }

        #endregion
    }
}
