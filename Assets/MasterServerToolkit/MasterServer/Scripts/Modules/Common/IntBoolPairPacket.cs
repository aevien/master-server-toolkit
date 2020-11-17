using MasterServerToolkit.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class IntBoolPairPacket : SerializablePacket
    {
        public int A { get; set; }
        public bool B { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(A);
            writer.Write(B);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            A = reader.ReadInt32();
            B = reader.ReadBoolean();
        }
    }
}
