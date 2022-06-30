using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Lobby factory implementation, which simply invokes
    /// an anonymous method
    /// </summary>
    public class LobbyFactoryAnonymous : ILobbyFactory
    {
        private LobbiesModule _module;
        private readonly LobbyCreationFactory _factory;

        public delegate ILobby LobbyCreationFactory(LobbiesModule module, MstProperties options, IPeer creator);

        public LobbyFactoryAnonymous(string id, LobbiesModule module, LobbyCreationFactory factory)
        {
            Id = id;
            _factory = factory;
            _module = module;
        }

        public ILobby CreateLobby(MstProperties options, IPeer creator)
        {
            var lobby = _factory.Invoke(_module, options, creator);

            // Add the lobby type if it's not set by the factory method
            if (lobby != null && lobby.Type == null)
            {
                lobby.Type = Id;
            }

            return lobby;
        }

        public string Id { get; private set; }
    }
}


