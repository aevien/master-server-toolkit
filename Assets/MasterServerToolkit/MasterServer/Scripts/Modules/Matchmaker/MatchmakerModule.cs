using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class MatchmakerModule : BaseServerModule
    {
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
            AddOptionalDependency<SpawnersModule>();
        }

        public override void Initialize(IServer server)
        {
            GameProviders = new HashSet<IGamesProvider>();

            var roomsModule = server.GetModule<RoomsModule>();
            var lobbiesModule = server.GetModule<LobbiesModule>();
            spawnersModule = server.GetModule<SpawnersModule>();

            if (!spawnersModule)
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

        protected virtual void FindGamesRequestHandler(IIncomingMessage message)
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
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error(e.Message);
                message.Respond(ResponseStatus.Error);
            }
        }

        protected virtual void GetRegionsRequestHandler(IIncomingMessage message)
        {
            try
            {
                if (!spawnersModule)
                {
                    message.Respond("Getting a list of regions is not allowed", ResponseStatus.Failed);
                    logger.Error("No spawner module found");
                    return;
                }

                var list = spawnersModule.GetRegions();

                if (list.Count == 0)
                {
                    message.Respond("No regions found. Please start spawner to get regions", ResponseStatus.Failed);
                    logger.Error("No spawner started");
                    return;
                }

                message.Respond(new RegionsPacket()
                {
                    Regions = list
                }, ResponseStatus.Success);
            }
            // If we got another exception
            catch (Exception e)
            {
                logger.Error(e.Message);
                message.Respond(e.Message, ResponseStatus.Error);
            }
        }

        #endregion
    }
}