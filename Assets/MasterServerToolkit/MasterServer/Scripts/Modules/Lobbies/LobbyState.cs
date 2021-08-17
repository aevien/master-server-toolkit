namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Represents the current state of the lobby
    /// </summary>
    public enum LobbyState
    {
        /// <summary>
        /// Failed
        /// </summary>
        FailedToStart = -1,
        /// <summary>
        /// Before game
        /// </summary>
        Preparations = 0,
        /// <summary>
        /// When game server is starting
        /// </summary>
        StartingGameServer,
        /// <summary>
        /// During the game
        /// </summary>
        GameInProgress,
        /// <summary>
        /// After the game
        /// </summary>
        GameOver
    }
}