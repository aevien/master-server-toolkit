#if MIRROR
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.MirrorNetworking
{
    public struct ValidateRoomAccessRequestMessage : NetworkMessage
    {
        public string Token { get; set; }
    }

    public static class ValidateRoomAccessRequestMessageExtension
    {
        public static void Serialize(this NetworkWriter writer, ValidateRoomAccessRequestMessage value)
        {
            writer.WriteString(value.Token);
        }

        public static ValidateRoomAccessRequestMessage Deserialize(this NetworkReader reader)
        {
            ValidateRoomAccessRequestMessage value = new ValidateRoomAccessRequestMessage()
            {
                Token = reader.ReadString()
            };

            return value;
        }
    }
}
#endif