using MasterServerToolkit.Json;

using MasterServerToolkit.MasterServer;

namespace MasterServerToolkit.GameService
{
    /// <summary>
    /// Represents a callback that receives leaderboard metadata.
    /// </summary>
    /// <param name="leaderboardInfo">The leaderboard metadata.</param>
    public delegate void LeaderboardInfoHandler(LeaderboardInfo leaderboardInfo);

    /// <summary>
    /// Represents a callback that receives leaderboard entries.
    /// </summary>
    /// <param name="leaderboardEntries">The leaderboard entries.</param>
    public delegate void LeaderboardEntriesHandler(LeaderboardEntries leaderboardEntries);

    /// <summary>
    /// Represents a callback that receives the current player's leaderboard entry.
    /// </summary>
    /// <param name="leaderboardPlayerInfo">The current player's leaderboard entry.</param>
    public delegate void LeaderboardPlayerInfoHandler(LeaderboardPlayerInfo leaderboardPlayerInfo);

    /// <summary>
    /// Defines the contract for leaderboard features exposed by the current game service.
    /// </summary>
    public interface ILeaderboardsModule : IServiceModule
    {
        /// <summary>
        /// Gets the most recently loaded leaderboard metadata.
        /// </summary>
        LeaderboardInfo Description { get; }

        /// <summary>
        /// Gets the most recently loaded leaderboard entries.
        /// </summary>
        LeaderboardEntries Entries { get; }

        /// <summary>
        /// Gets the most recently loaded current-player leaderboard entry.
        /// </summary>
        LeaderboardPlayerInfo PlayerEntry { get; }

        /// <summary>
        /// Submits a score to the specified leaderboard.
        /// </summary>
        /// <param name="name">The name or identifier of the leaderboard.</param>
        /// <param name="score">The signed 64-bit canonical score to mirror.</param>
        /// <param name="extra">Additional metadata to associate with the score.</param>
        /// <param name="callback">Completion callback reporting whether the platform accepted the score.</param>
        void SetScore(string name, long score, MstJson extra, SuccessCallback callback = null);

        /// <summary>
        /// Retrieves metadata for the specified leaderboard.
        /// </summary>
        /// <param name="name">The name or identifier of the leaderboard.</param>
        /// <param name="callback">The callback that receives the leaderboard metadata.</param>
        void GetInfo(string name, LeaderboardInfoHandler callback);

        /// <summary>
        /// Retrieves entries from the specified leaderboard.
        /// </summary>
        /// <param name="name">The name or identifier of the leaderboard.</param>
        /// <param name="options">The platform-specific query options, such as paging or range parameters.</param>
        /// <param name="callback">The callback that receives the leaderboard entries.</param>
        void GetEntries(string name, MstJson options, LeaderboardEntriesHandler callback);

        /// <summary>
        /// Retrieves the current player's entry from the specified leaderboard.
        /// </summary>
        /// <param name="name">The name or identifier of the leaderboard.</param>
        /// <param name="callback">The callback that receives the current player's leaderboard entry.</param>
        void GetPlayerInfo(string name, LeaderboardPlayerInfoHandler callback);
    }
}
