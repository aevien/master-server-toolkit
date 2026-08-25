using System;

namespace MasterServerToolkit.MasterServer
{
    public class ChatMessageInfo
    {
        public const int MaxParticipantLength = 64;
        public const int MaxMessageLength = 512;
        public const string UsersReceiver = "users";

        public string Id { get; set; } = string.Empty;
        public ChatMessageType MessageType { get; set; }
        public string Sender { get; set; } = string.Empty;
        public string Receiver { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }

        public static ChatMessageInfo FromPacket(ChatMessagePacket packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));

            return new ChatMessageInfo
            {
                Id = Mst.Helper.CreateGuidString(),
                MessageType = packet.MessageType,
                Sender = NormalizeParticipant(packet.Sender),
                Receiver = NormalizeReceiver(packet),
                Message = NormalizeMessage(packet.Message),
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        public void Normalize()
        {
            Id = string.IsNullOrWhiteSpace(Id) ? Mst.Helper.CreateGuidString() : Id.Trim();
            Sender = NormalizeParticipant(Sender);
            Receiver = string.IsNullOrWhiteSpace(Receiver) && MessageType == ChatMessageType.Users
                ? UsersReceiver
                : NormalizeParticipant(Receiver);
            Message = NormalizeMessage(Message);

            if (CreatedAtUtc == default)
                CreatedAtUtc = DateTime.UtcNow;
        }

        private static string NormalizeReceiver(ChatMessagePacket packet)
        {
            string receiver = NormalizeParticipant(packet.Receiver);

            return string.IsNullOrWhiteSpace(receiver) && packet.MessageType == ChatMessageType.Users
                ? UsersReceiver
                : receiver;
        }

        private static string NormalizeParticipant(string value)
        {
            value = value?.Trim() ?? string.Empty;

            return value.Length <= MaxParticipantLength
                ? value
                : value.Substring(0, MaxParticipantLength);
        }

        private static string NormalizeMessage(string value)
        {
            value ??= string.Empty;

            return value.Length <= MaxMessageLength
                ? value
                : value.Substring(0, MaxMessageLength);
        }
    }
}
