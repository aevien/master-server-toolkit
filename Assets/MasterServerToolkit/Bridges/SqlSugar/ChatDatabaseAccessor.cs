using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class ChatDatabaseAccessor : IChatDatabaseAccessor
    {
        private const int DefaultLimit = 100;
        private const int MaxLimit = 500;

        private readonly ConnectionConfig configuration;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public ChatDatabaseAccessor(ConnectionConfig configuration)
        {
            this.configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            using SqlSugarClient db = new(configuration);

            string tableName = db.EntityMaintenance.GetTableName(typeof(ChatMessageData));

            if (!db.DbMaintenance.IsAnyTable(tableName, false))
                db.CodeFirst.InitTables(typeof(ChatMessageData));

            if (!db.DbMaintenance.IsAnyTable(tableName, false))
                throw new InvalidOperationException($"Required database table '{tableName}' was not created");
        }

        public async Task SaveMessageAsync(ChatMessageInfo message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            using SqlSugarClient db = new(configuration);

            await db.Insertable(ChatMessageData.FromInfo(message)).ExecuteCommandAsync();
        }

        public async Task<List<ChatMessageInfo>> GetMessagesAsync(ChatMessageType messageType, string receiver, int limit,
            DateTime? beforeUtc = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            receiver = NormalizeReceiver(messageType, receiver);
            limit = NormalizeLimit(limit);
            int type = (int)messageType;

            using SqlSugarClient db = new(configuration);

            var query = db.Queryable<ChatMessageData>()
                .Where(message => message.MessageType == type && message.Receiver == receiver);

            if (beforeUtc.HasValue)
            {
                DateTime before = beforeUtc.Value;
                query = query.Where(message => message.CreatedAtUtc < before);
            }

            var messages = await query
                .OrderBy(message => message.CreatedAtUtc, OrderByType.Desc)
                .Take(limit)
                .ToListAsync();

            cancellationToken.ThrowIfCancellationRequested();
            return messages.Select(message => message.ToInfo()).ToList();
        }

        public async Task<List<ChatMessageInfo>> GetPrivateMessagesAsync(string userA, string userB, int limit,
            DateTime? beforeUtc = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            userA = NormalizeParticipant(userA);
            userB = NormalizeParticipant(userB);
            limit = NormalizeLimit(limit);
            int type = (int)ChatMessageType.Private;

            if (string.IsNullOrWhiteSpace(userA) || string.IsNullOrWhiteSpace(userB))
                return new List<ChatMessageInfo>();

            using SqlSugarClient db = new(configuration);

            var query = db.Queryable<ChatMessageData>()
                .Where(message => message.MessageType == type &&
                    ((message.Sender == userA && message.Receiver == userB) ||
                     (message.Sender == userB && message.Receiver == userA)));

            if (beforeUtc.HasValue)
            {
                DateTime before = beforeUtc.Value;
                query = query.Where(message => message.CreatedAtUtc < before);
            }

            var messages = await query
                .OrderBy(message => message.CreatedAtUtc, OrderByType.Desc)
                .Take(limit)
                .ToListAsync();

            cancellationToken.ThrowIfCancellationRequested();
            return messages.Select(message => message.ToInfo()).ToList();
        }

        public void Dispose()
        {
            CustomProperties?.Clear();
        }

        private static int NormalizeLimit(int limit)
        {
            if (limit <= 0)
                return DefaultLimit;

            return Math.Min(limit, MaxLimit);
        }

        private static string NormalizeReceiver(ChatMessageType messageType, string receiver)
        {
            receiver = NormalizeParticipant(receiver);

            return string.IsNullOrWhiteSpace(receiver) && messageType == ChatMessageType.Users
                ? ChatMessageInfo.UsersReceiver
                : receiver;
        }

        private static string NormalizeParticipant(string value)
        {
            value = value?.Trim() ?? string.Empty;

            return value.Length <= ChatMessageInfo.MaxParticipantLength
                ? value
                : value.Substring(0, ChatMessageInfo.MaxParticipantLength);
        }
    }
}
