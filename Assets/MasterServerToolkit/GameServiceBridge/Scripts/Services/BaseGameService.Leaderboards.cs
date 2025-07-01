using MasterServerToolkit.Json;
using UnityEngine;

namespace MasterServerToolkit.GameService
{
    public abstract partial class BaseGameService : MonoBehaviour, IGameService
    {
        public bool IsLeaderboardSupported { get; protected set; }
        public LeaderboardInfo LeaderboardDescription { get; protected set; }
        public LeaderboardEntries LeaderboardEntries { get; protected set; } = new LeaderboardEntries();
        public LeaderboardPlayerInfo LeaderboardPlayerEntry { get; protected set; }

        public virtual void SetLeaderboardScore(string name, int score, MstJson extra) { }

        public virtual void GetLeaderboardInfo(string name, LeaderboardInfoHandler callback)
        {
            if (IsLeaderboardSupported)
            {
                leaderboardInfoCallback = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        public virtual void GetLeaderboardEntries(string name, MstJson options, LeaderboardEntriesHandler callback)
        {
            if (IsLeaderboardSupported)
            {
                leaderboardEntriesCallback = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        public virtual void GetLeaderboardPlayerInfo(string name, LeaderboardPlayerInfoHandler callback)
        {
            if (IsLeaderboardSupported)
            {
                leaderboardPlayerInfoCallback = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }
    }
}
