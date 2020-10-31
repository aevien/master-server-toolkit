namespace MasterServerToolkit.MasterServer
{
    public class LobbyConfig
    {
        /// <summary>
        /// If true, players will be able to switch teams
        /// </summary>
        public bool EnableTeamSwitching { get; set; } = true;

        /// <summary>
        /// If true, after the game is over, lobby will be
        /// set to preparation state, and players will be able to start the game again
        /// </summary>
        public bool PlayAgainEnabled { get; set; } = true;

        /// <summary>
        /// If true, players will be able to set whether they're ready
        /// to play or not.
        /// </summary>
        public bool EnableReadySystem { get; set; } = true;

        /// <summary>
        /// If ture, players will be allowed to join lobby when
        /// game is live (game server is running)
        /// </summary>
        public bool AllowJoiningWhenGameIsLive { get; set; } = true;

        /// <summary>
        /// If true, lobby will have a game master, otherwise
        /// no player will be assigned as a master
        /// </summary>
        public bool EnableGameMasters { get; set; } = true;

        /// <summary>
        /// If true, game server will start automatically when all players are ready
        /// </summary>
        public bool StartGameWhenAllReady { get; set; } = false;

        /// <summary>
        /// If true, game master will be able to start game
        /// manually
        /// </summary>
        public bool EnableManualStart { get; set; } = true;

        /// <summary>
        /// If true, lobby will not destroy when last player leaves.
        /// </summary>
        public bool KeepAliveWithZeroPlayers { get; set; } = false;

        /// <summary>
        /// 
        /// </summary>
        public bool AllowPlayersChangeLobbyProperties { get; set; } = true;
    }
}