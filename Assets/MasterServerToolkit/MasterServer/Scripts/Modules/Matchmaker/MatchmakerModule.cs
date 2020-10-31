using MasterServerToolkit.Networking;
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

        protected override void Awake()
        {
            base.Awake();

            AddOptionalDependency<RoomsModule>();
            AddOptionalDependency<LobbiesModule>();
        }

        public override void Initialize(IServer server)
        {
            GameProviders = new HashSet<IGamesProvider>();

            var roomsModule = server.GetModule<RoomsModule>();
            var lobbiesModule = server.GetModule<LobbiesModule>();

            // Dependencies
            if (roomsModule != null)
            {
                AddProvider(roomsModule);
            }

            if (lobbiesModule != null)
            {
                AddProvider(lobbiesModule);
            }

            // Add handlers
            server.RegisterMessageHandler((short)MstMessageCodes.FindGamesRequest, FindGamesRequestHandler);
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
            var list = new List<GameInfoPacket>();

            var filters = MstProperties.FromBytes(message.AsBytes());

            foreach (var provider in GameProviders)
            {
                list.AddRange(provider.GetPublicGames(message.Peer, filters));
            }

            // Convert to generic list and serialize to bytes
            var bytes = list.Select(l => (ISerializablePacket)l).ToBytes();
            message.Respond(bytes, ResponseStatus.Success);
        }

        #endregion
    }
}