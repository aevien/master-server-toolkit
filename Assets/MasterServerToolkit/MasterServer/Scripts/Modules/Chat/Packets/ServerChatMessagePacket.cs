using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ServerChatMessagePacket : SerializablePacket
    {
        public ChatMessageType MessageType { get; set; } = ChatMessageType.Unknown;
        public string Receiver { get; set; } = string.Empty;
        public string Sender { get; set; } = string.Empty;
        public string SenderDisplayName { get; set; } = string.Empty;
        public string ReceiverDisplayName { get; set; } = string.Empty;
        public string SenderAvatar { get; set; } = string.Empty;
        public string ReceiverAvatar { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public List<string> Recipients { get; set; } = new List<string>();

        public ChatMessagePacket ToChatMessagePacket()
        {
            return new ChatMessagePacket
            {
                MessageType = MessageType,
                Receiver = Receiver,
                Sender = Sender,
                SenderDisplayName = SenderDisplayName,
                ReceiverDisplayName = ReceiverDisplayName,
                SenderAvatar = SenderAvatar,
                ReceiverAvatar = ReceiverAvatar,
                Message = Message
            };
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            Recipients ??= new List<string>();

            if (Recipients.Count > ushort.MaxValue)
            {
                throw new InvalidOperationException($"Server chat message cannot contain more than {ushort.MaxValue} recipients");
            }

            ChatMessagePacket.WriteMessageType(writer, MessageType);
            writer.Write(Receiver ?? string.Empty);
            writer.Write(Sender ?? string.Empty);
            writer.Write(SenderDisplayName ?? string.Empty);
            writer.Write(ReceiverDisplayName ?? string.Empty);
            writer.Write(SenderAvatar ?? string.Empty);
            writer.Write(ReceiverAvatar ?? string.Empty);
            writer.Write(Message ?? string.Empty);
            writer.Write((ushort)Recipients.Count);

            foreach (string recipient in Recipients)
            {
                writer.Write(recipient ?? string.Empty);
            }
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            MessageType = ChatMessagePacket.ReadMessageType(reader);
            Receiver = reader.ReadString();
            Sender = reader.ReadString();
            SenderDisplayName = reader.ReadString();
            ReceiverDisplayName = reader.ReadString();
            SenderAvatar = reader.ReadString();
            ReceiverAvatar = reader.ReadString();
            Message = reader.ReadString();

            int count = reader.ReadUInt16();
            Recipients = new List<string>(count);

            for (int i = 0; i < count; i++)
            {
                Recipients.Add(reader.ReadString());
            }
        }
    }
}
