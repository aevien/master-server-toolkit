using MasterServerToolkit.Json;

namespace MasterServerToolkit.GameService
{
    public class LeaderboardPlayerInfo
    {
        public long Score { get; set; }
        public string FormatedScore { get; set; }
        public MstJson Extra { get; set; } = MstJson.CreateObject();
        public int Rank { get; set; }
        public string PlayerId { get; set; }
        public string PlayerAvatar { get; set; }
        public string PlayerName { get; set; }
        public string PlayerLang { get; set; }
        public bool IsPlayerAvatarAllowed { get; set; }
        public bool IsPlayerNameAllowed { get; set; }
    } 
}
