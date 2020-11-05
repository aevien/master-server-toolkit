using MasterServerToolkit.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class UpdateAccountPropertiesPacket : SerializablePacket
    {
        public UpdateAccountPropertiesPacket()
        {
            UserId = string.Empty;
            Properties = new MstProperties();
        }

        public UpdateAccountPropertiesPacket(string userId, MstProperties properties)
        {
            UserId = userId ?? throw new ArgumentNullException(nameof(userId));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }

        public string UserId { get; set; }
        public MstProperties Properties { get; set; }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            UserId = reader.ReadString();
            Properties = new MstProperties(reader.ReadDictionary());
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(UserId);
            writer.Write(Properties.ToDictionary());
        }
    }
}
