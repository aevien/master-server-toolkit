using System;

namespace MasterServerToolkit.MasterServer
{
    public class RegionInfo : IEquatable<RegionInfo>
    {
        public string Name { get; set; }
        public string Ip { get; set; }
        public int PingTime { get; set; }

        public bool Equals(RegionInfo other)
        {
            return Name == other.Name;
        }
    }
}