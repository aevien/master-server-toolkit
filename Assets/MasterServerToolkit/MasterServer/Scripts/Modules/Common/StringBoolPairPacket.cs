using MasterServerToolkit.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class StringBoolPairPacket : SerializablePacket
    {
        public string A { get; set; }
        public bool B { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(A);
            writer.Write(B);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            A = reader.ReadString();
            B = reader.ReadBoolean();
        }
    }
}