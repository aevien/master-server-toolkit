using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class MstServer : MstBaseClient
    {
        public MstAuthServer Auth { get; private set; }
        public MstLobbiesServer Lobbies { get; private set; }
        public MstProfilesServer Profiles { get; private set; }
        public MstRoomsServer Rooms { get; private set; }
        public MstSpawnersServer Spawners { get; private set; }
        public MstDbAccessor DbAccessors { get; private set; }
        public MstNotificationServer Notifications { get; private set; }
        public MstTrafficStatistics Analytics { get; private set; }
        public AchievementsModuleServer Achievements { get; private set; }

        public MstServer(IClientSocket connection) : base(connection)
        {
            DbAccessors = new MstDbAccessor();
            Rooms = new MstRoomsServer(connection);
            Spawners = new MstSpawnersServer(connection);
            Auth = new MstAuthServer(connection);
            Lobbies = new MstLobbiesServer(connection);
            Profiles = new MstProfilesServer(connection);
            Notifications = new MstNotificationServer(connection);
            Achievements = new AchievementsModuleServer(connection);
            Analytics = new MstTrafficStatistics();
        }
    }
}