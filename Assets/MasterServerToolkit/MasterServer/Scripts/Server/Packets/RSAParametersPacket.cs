using MasterServerToolkit.Networking;

namespace MasterServerToolkit
{
    public class RSAParametersPacket : SerializablePacket
    {
        public byte[] exponent;
        public byte[] modulus;

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            ushort length = reader.ReadUInt16();
            exponent = reader.ReadBytes(length);

            length = reader.ReadUInt16();
            modulus = reader.ReadBytes(length);
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write((ushort)exponent.Length);
            writer.Write(exponent);
            writer.Write((ushort)modulus.Length);
            writer.Write(modulus);
        }
    }
}
