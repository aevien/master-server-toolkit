using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public enum ChatMessageType : byte
    {
        Unknown = 0,
        Private = 1,
        Channel = 2,
        Users = 3
    }

    public class ChatMessagePacket : SerializablePacket
    {
        public const int MaxDisplayNameLength = 64;
        public const int MaxAvatarLength = 512;

        public ChatMessageType MessageType { get; set; }

        /// <summary>
        /// Represents receiver username if it's a private message,
        /// channel name if it's a channel message,
        /// or server-defined route/context when message is sent to a selected user list.
        /// </summary>
        public string Receiver { get; set; } = string.Empty;
        /// <summary>
        /// Represents sender username
        /// </summary>
        public string Sender { get; set; } = string.Empty;
        /// <summary>
        /// Optional sender name used only for presentation. Routing and permissions always use Sender.
        /// </summary>
        public string SenderDisplayName { get; set; } = string.Empty;
        /// <summary>
        /// Optional receiver name used only for presentation of outgoing private messages.
        /// Routing and permissions always use Receiver.
        /// </summary>
        public string ReceiverDisplayName { get; set; } = string.Empty;
        /// <summary>
        /// Optional HTTPS avatar URL used only for presentation of the sender.
        /// Routing and permissions always use Sender.
        /// </summary>
        public string SenderAvatar { get; set; } = string.Empty;
        /// <summary>
        /// Optional HTTPS avatar URL used only for presentation of outgoing private messages.
        /// Routing and permissions always use Receiver.
        /// </summary>
        public string ReceiverAvatar { get; set; } = string.Empty;
        /// <summary>
        /// Messages text
        /// </summary>
        public string Message { get; set; } = string.Empty;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            WriteMessageType(writer, MessageType);
            writer.Write(Receiver ?? string.Empty);
            writer.Write(Sender ?? string.Empty);
            writer.Write(SenderDisplayName ?? string.Empty);
            writer.Write(ReceiverDisplayName ?? string.Empty);
            writer.Write(SenderAvatar ?? string.Empty);
            writer.Write(ReceiverAvatar ?? string.Empty);
            writer.Write(Message ?? string.Empty);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            MessageType = ReadMessageType(reader);
            Receiver = reader.ReadString();
            Sender = reader.ReadString();
            SenderDisplayName = reader.ReadString();
            ReceiverDisplayName = reader.ReadString();
            SenderAvatar = reader.ReadString();
            ReceiverAvatar = reader.ReadString();
            Message = reader.ReadString();
        }

        internal static void WriteMessageType(EndianBinaryWriter writer, ChatMessageType messageType)
        {
            writer.Write((byte)messageType);
        }

        internal static ChatMessageType ReadMessageType(EndianBinaryReader reader)
        {
            byte value = reader.ReadByte();

            if (IsKnownMessageType(value))
                return (ChatMessageType)value;

            return ChatMessageType.Unknown;
        }

        private static bool IsKnownMessageType(int value)
        {
            return value >= (int)ChatMessageType.Unknown && value <= (int)ChatMessageType.Users;
        }
    }
}
