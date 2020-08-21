using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class IntPairPacket : SerializablePacket
    {
        public int A { get; set; }
        public int B { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(A);
            writer.Write(B);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            A = reader.ReadInt32();
            B = reader.ReadInt32();
        }
    }
}