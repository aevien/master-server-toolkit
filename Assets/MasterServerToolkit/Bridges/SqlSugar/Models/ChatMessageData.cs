using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.ChatMessages)]
    public class ChatMessageData
    {
        [SugarColumn(ColumnName = "id", Length = 38, IsPrimaryKey = true, IsNullable = false)]
        public string Id { get; set; }

        [SugarColumn(ColumnName = "message_type", IsNullable = false)]
        public int MessageType { get; set; }

        [SugarColumn(ColumnName = "sender", Length = 64, IsNullable = false)]
        public string Sender { get; set; }

        [SugarColumn(ColumnName = "receiver", Length = 64, IsNullable = false)]
        public string Receiver { get; set; }

        [SugarColumn(ColumnName = "message", Length = 512, IsNullable = false)]
        public string Message { get; set; }

        [SugarColumn(ColumnName = "created_at", IsNullable = false)]
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
