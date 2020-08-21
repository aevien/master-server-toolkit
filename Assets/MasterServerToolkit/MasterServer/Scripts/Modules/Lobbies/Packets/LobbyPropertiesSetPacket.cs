using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class LobbyPropertiesSetPacket : SerializablePacket
    {
        public int LobbyId;
        public Dictionary<string, string> Properties;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(LobbyId);
            writer.Write(Properties);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            LobbyId = reader.ReadInt32();
            Properties = reader.ReadDictionary();
        }
    }
}