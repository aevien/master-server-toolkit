using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public partial class MstClient
    {
        public RoomsClient Rooms { get; private set; }
        public SpawnersClient Spawners { get; private set; }
        public MatchmakerClient Matchmaker { get; private set; }
        public AuthClient Auth { get; private set; }
        public ChatClient Chat { get; private set; }
        public GroupClient Groups { get; private set; }
        public LobbiesClient Lobbies { get; private set; }
        public ProfilesClient Profiles { get; private set; }
        public NotificationClient Notifications { get; private set; }
        public AchievementsModuleClient Achievements { get; private set; }
        public LeaderboardsModuleClient Leaderboards { get; private set; }
        public QuestsModuleClient Quests { get; private set; }
        public AnalyticsModuleClient Analytics { get; private set; }
        public RemoteConfigModuleClient RemoteConfigs { get; private set; }

        public MstClient(IClientSocket connection)
        {
            Rooms = new RoomsClient(connection);
            Spawners = new SpawnersClient(connection);
            Matchmaker = new MatchmakerClient(connection);
            Auth = new AuthClient(connection);
            Chat = new ChatClient(connection);
            Groups = new GroupClient(connection);
            Lobbies = new LobbiesClient(connection);
            Profiles = new ProfilesClient(connection);
            Notifications = new NotificationClient(connection);
            Achievements = new AchievementsModuleClient(connection);
            Leaderboards = new LeaderboardsModuleClient(connection);
            Analytics = new AnalyticsModuleClient(connection);
            Quests = new QuestsModuleClient(connection);
            RemoteConfigs = new RemoteConfigModuleClient(connection);
        }
    }
}
