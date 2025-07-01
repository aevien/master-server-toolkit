using System.Collections.Generic;

namespace MasterServerToolkit.GameService
{
    public class LeaderboardEntries
    {
        public LeaderboardEntries()
        {
            Entries = new List<LeaderboardPlayerInfo>();
        }

        public string Id { get; set; }
        public string Title { get; set; }
        public bool IsDefault { get; set; }
        public bool Invert { get; set; }
        public int DecimalOffset { get; set; }
        public LeaderboardType Type { get; set; }
        public int UserRank { get; set; }
        public int Start { get; set; }
        public int Size { get; set; }
        public List<LeaderboardPlayerInfo> Entries { get; set; }
    }
}