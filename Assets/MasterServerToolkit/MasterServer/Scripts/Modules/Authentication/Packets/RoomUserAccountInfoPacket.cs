using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class RoomUserAccountInfoPacket : SerializablePacket
    {
        public int PeerId { get; set; }
        public string Username { get; set; }
        public string UserId { get; set; }
        public bool IsGuest { get; set; }
        public Dictionary<string, string> ExtraProperties { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(PeerId);
            writer.Write(Username);
            writer.Write(UserId);
            writer.Write(IsGuest);
            writer.Write(ExtraProperties);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            PeerId = reader.ReadInt32();
            Username = reader.ReadString();
            UserId = reader.ReadString();
            IsGuest = reader.ReadBoolean();
            ExtraProperties = reader.ReadDictionary();
        }

        public override string ToString()
        {
            return string.Format($"[Peer account info: Peer ID: {PeerId}, UserId: {UserId}, Username: {Username}, Properties: {new MstProperties(ExtraProperties)}]");
        }
    }
}