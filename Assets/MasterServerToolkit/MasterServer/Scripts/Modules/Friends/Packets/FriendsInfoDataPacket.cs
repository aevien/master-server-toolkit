using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class FriendsInfoDataPacket : SerializablePacket
    {
        public FriendInfo[] Users { get; set; }
        public DateTime LastUpdate { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            throw new System.NotImplementedException();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }
    }
}
