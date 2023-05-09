using MasterServerToolkit.Networking;

namespace MasterServerToolkit.Dev
{
    public class BodyKits: SerializablePacket
    {
        public int Wheels { get; set; }
        public int FrontBumper { get; set; }
        public int RearBumper { get; set; }
        public int Bonnet { get; set; }
        public int Fenders { get; set; }
        public int SideSkirts { get; set; }
        public int Spoiler { get; set; }
        public int Materials { get; set; }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Wheels = reader.ReadInt32();
            FrontBumper = reader.ReadInt32();
            RearBumper = reader.ReadInt32();
            Bonnet = reader.ReadInt32();
            Fenders = reader.ReadInt32();
            SideSkirts = reader.ReadInt32();
            Spoiler = reader.ReadInt32();
            Materials = reader.ReadInt32();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Wheels);
            writer.Write(FrontBumper);
            writer.Write(RearBumper);
            writer.Write(Bonnet);
            writer.Write(Fenders);
            writer.Write(SideSkirts);
            writer.Write(Spoiler);
            writer.Write(Materials);
        }
    }
}
