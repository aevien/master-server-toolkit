namespace MasterServerToolkit.GameService
{
    public class PlayerInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; }
        public string Avatar { get; set; } = "https://i.pravatar.cc/300";
        public bool IsGuest { get; set; } = true;
    }
}