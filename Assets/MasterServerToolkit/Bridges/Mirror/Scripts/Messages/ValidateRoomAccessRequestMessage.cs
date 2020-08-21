#if MIRROR
using Mirror;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MasterServerToolkit.Bridges.Mirror
{
    public class ValidateRoomAccessRequestMessage : IMessageBase
    {
        public ValidateRoomAccessRequestMessage()
        {
        }

        public ValidateRoomAccessRequestMessage(string token)
        {
            Token = token ?? throw new ArgumentNullException(nameof(token));
        }

        public string Token { get; set; }

        public void Deserialize(NetworkReader reader)
        {
            Token = reader.ReadString();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteString(Token);
        }
    }
}
#endif