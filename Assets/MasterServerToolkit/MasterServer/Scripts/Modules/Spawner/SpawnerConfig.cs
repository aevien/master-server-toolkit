namespace MasterServerToolkit.MasterServer
{
    public class SpawnerConfig
    {
        public string MachineIp { get; set; } = "127.0.0.1";
        public string Region { get; set; } = "International";
        public string MasterIp { get; set; } = string.Empty;
        public int MasterPort { get; set; } = -1;
        public string ExecutablePath { get; set; } = string.Empty;
        public bool UseWebSockets { get; set; } = false;
    }
}