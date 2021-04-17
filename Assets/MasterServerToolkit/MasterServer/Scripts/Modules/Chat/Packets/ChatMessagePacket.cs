using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public enum ChatMessageType
    {
        Unknown, PrivateMessage, ChannelMessage
    }

    public class ChatMessagePacket : SerializablePacket
    {
        public ChatMessageType MessageType { get; set; }

        /// <summary>
        /// Represents receiver username if it's a private message,
        /// or channel name, if channel message
        /// </summary>
        public string Receiver { get; set; } = string.Empty;
        /// <summary>
        /// Represents sender username
        /// </summary>
        public string Sender { get; set; } = string.Empty;
        /// <summary>
        /// Messages text
        /// </summary>
        public string Message { get; set; } = string.Empty;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write((byte)MessageType);
            writer.Write(Receiver);
            writer.Write(Sender);
            writer.Write(Message);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            MessageType = (ChatMessageType)reader.ReadByte();
            Receiver = reader.ReadString();
            Sender = reader.ReadString();
            Message = reader.ReadString();
        }
    }
}