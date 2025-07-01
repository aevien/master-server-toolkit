namespace MasterServerToolkit.GameService
{
    public enum LeaderboardType { Numeric, Time }

    public class LeaderboardInfo
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public bool IsDefault { get; set; }
        public bool Invert {  get; set; }
        public int DecimalOffset {  get; set; }
        public LeaderboardType Type { get; set; }
    } 
}