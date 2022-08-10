using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public partial class MstClient : MstBaseClient
    {
        public MstRoomsClient Rooms { get; private set; }

        public MstSpawnersClient Spawners { get; private set; }

        public MstMatchmakerClient Matchmaker { get; private set; }

        public MstAuthClient Auth { get; private set; }

        public MstChatClient Chat { get; private set; }

        public MstLobbiesClient Lobbies { get; private set; }

        public MstProfilesClient Profiles { get; private set; }

        public MstNotificationClient Notifications { get; private set; }

        public MstClient(IClientSocket connection) : base(connection)
        {
            Rooms = new MstRoomsClient(connection);
            Spawners = new MstSpawnersClient(connection);
            Matchmaker = new MstMatchmakerClient(connection);
            Auth = new MstAuthClient(connection);
            Chat = new MstChatClient(connection);
            Lobbies = new MstLobbiesClient(connection);
            Profiles = new MstProfilesClient(connection);
            Notifications = new MstNotificationClient(connection);
        }
    }
}