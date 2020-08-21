using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Just a helpful packet to have
    /// </summary>
    public class StringPairPacket : SerializablePacket
    {
        public string A { get; set; }
        public string B { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(A);
            writer.Write(B);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            A = reader.ReadString();
            B = reader.ReadString();
        }
    }
}