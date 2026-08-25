using MasterServerToolkit.Json;

using MasterServerToolkit.MasterServer;

namespace MasterServerToolkit.GameService
{
    public class BaseLeaderboardsModule : BaseServiceModule, ILeaderboardsModule
    {
        public LeaderboardInfo Description { get; protected set; }
        public LeaderboardEntries Entries { get; protected set; }
        public LeaderboardPlayerInfo PlayerEntry { get; protected set; }
        public override bool IsSupported { get; protected set; }

        private LeaderboardInfoHandler leaderboardInfoCallback;
        private LeaderboardEntriesHandler leaderboardEntriesCallback;
        private LeaderboardPlayerInfoHandler leaderboardPlayerInfoCallback;

        public virtual void GetEntries(string name, MstJson options, LeaderboardEntriesHandler callback)
        {
            if (IsSupported)
            {
                leaderboardEntriesCallback = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        public virtual void GetInfo(string name, LeaderboardInfoHandler callback)
        {
            if (IsSupported)
            {
                leaderboardInfoCallback = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        public virtual void GetPlayerInfo(string name, LeaderboardPlayerInfoHandler callback)
        {
            if (IsSupported)
            {
                leaderboardPlayerInfoCallback = callback;
            }
            else
            {
                callback?.Invoke(null);
            }
        }

        public virtual void SetScore(string name, long score, MstJson extra, SuccessCallback callback = null)
        {
            callback?.Invoke(false, "leaderboards_not_supported");
        }

        protected void NotifyOnGetLeaderboardInfo(LeaderboardInfo leaderboardInfo)
        {
            leaderboardInfoCallback?.Invoke(leaderboardInfo);
            leaderboardInfoCallback = null;
        }

        protected void NotifyOnGetLeaderboardEntries(LeaderboardEntries leaderboardEntries)
        {
            leaderboardEntriesCallback?.Invoke(leaderboardEntries);
            leaderboardEntriesCallback = null;
        }

        protected void NotifyOnGetLeaderboardPlayerInfo(LeaderboardPlayerInfo leaderboardPlayerInfo)
        {
            leaderboardPlayerInfoCallback?.Invoke(leaderboardPlayerInfo);
            leaderboardPlayerInfoCallback = null;
        }
    }
}
