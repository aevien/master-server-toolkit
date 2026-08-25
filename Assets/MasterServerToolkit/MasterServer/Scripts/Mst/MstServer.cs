using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class MstServer
    {
        public AuthServer Auth { get; private set; }
        public LobbiesServer Lobbies { get; private set; }
        public ProfilesServer Profiles { get; private set; }
        public RoomsServer Rooms { get; private set; }
        public SpawnersServer Spawners { get; private set; }
        public MstDbAccessor DbAccessors { get; private set; }
        public NotificationServer Notifications { get; private set; }
        public ChatServer Chat { get; private set; }
        public GroupServer Groups { get; private set; }
        public MstTrafficStatistics Traffic { get; private set; }
        public AchievementsModuleServer Achievements { get; private set; }
        public LeaderboardsModuleServer Leaderboards { get; private set; }
        public AnalyticsModuleServer Analytics { get; private set; }
        public QuestsModuleClient Quests { get; private set; }
        public RemoteConfigModuleServer RemoteConfigs { get; private set; }

        public MstServer(IClientSocket connection)
        {
            DbAccessors = new MstDbAccessor();
            Rooms = new RoomsServer(connection);
            Spawners = new SpawnersServer(connection);
            Auth = new AuthServer(connection);
            Lobbies = new LobbiesServer(connection);
            Profiles = new ProfilesServer(connection);
            Notifications = new NotificationServer(connection);
            Chat = new ChatServer(connection);
            Groups = new GroupServer(connection);
            Achievements = new AchievementsModuleServer(connection);
            Leaderboards = new LeaderboardsModuleServer(connection);
            Traffic = new MstTrafficStatistics();
            Analytics = new AnalyticsModuleServer(connection);
            Quests = new QuestsModuleClient(connection);
            RemoteConfigs = new RemoteConfigModuleServer(connection);
        }
    }
}
