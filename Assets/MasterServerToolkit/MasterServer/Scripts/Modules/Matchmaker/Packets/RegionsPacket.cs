using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class RegionsPacket : SerializablePacket
    {
        public List<RegionInfo> Regions = new List<RegionInfo>();

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            byte count = reader.ReadByte();

            for (byte i = 0; i < count; i++)
            {
                Regions.Add(new RegionInfo()
                {
                    Name = reader.ReadString(),
                    Ip = reader.ReadString(),
                });
            }
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write((byte)Regions.Count);

            foreach (var region in Regions)
            {
                writer.Write(region.Name);
                writer.Write(region.Ip);
            }
        }
    }
}
