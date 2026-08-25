#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.MasterServer;
using System;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ChatMessageData
    {
        [BsonId]
        public string Id { get; set; }

        public int MessageType { get; set; }
        public string Sender { get; set; }
        public string Receiver { get; set; }
        public string Message { get; set; }
        public DateTime CreatedAtUtc { get; set; }

        public ChatMessageInfo ToInfo()
        {
            return new ChatMessageInfo
            {
                Id = Id ?? string.Empty,
                MessageType = (ChatMessageType)MessageType,
                Sender = Sender ?? string.Empty,
                Receiver = Receiver ?? string.Empty,
                Message = Message ?? string.Empty,
                CreatedAtUtc = CreatedAtUtc
            };
        }

        public static ChatMessageData FromInfo(ChatMessageInfo info)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            info.Normalize();

            return new ChatMessageData
            {
                Id = info.Id,
                MessageType = (int)info.MessageType,
                Sender = info.Sender,
                Receiver = info.Receiver,
                Message = info.Message,
                CreatedAtUtc = info.CreatedAtUtc
            };
        }
    }
}

#endif
