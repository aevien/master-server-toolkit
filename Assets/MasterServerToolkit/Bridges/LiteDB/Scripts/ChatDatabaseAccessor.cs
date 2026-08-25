#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using LiteDB;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class ChatDatabaseAccessor : IChatDatabaseAccessor
    {
        private const int DefaultLimit = 100;
        private const int MaxLimit = 500;
        private const string CollectionName = "chat_messages";

        private readonly LiteDatabase database;
        private readonly ILiteCollection<ChatMessageData> messagesCollection;
        private readonly SemaphoreSlim dbSemaphore = new(1, 1);

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public ChatDatabaseAccessor(string databaseName)
        {
            database = new LiteDatabase($"{databaseName}.db");
            database.UtcDate = true;

            messagesCollection = database.GetCollection<ChatMessageData>(CollectionName);
            messagesCollection.EnsureIndex(message => message.Id, true);
            messagesCollection.EnsureIndex(message => message.MessageType);
            messagesCollection.EnsureIndex(message => message.Receiver);
            messagesCollection.EnsureIndex(message => message.Sender);
            messagesCollection.EnsureIndex(message => message.CreatedAtUtc);
        }

        public async Task SaveMessageAsync(ChatMessageInfo message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                messagesCollection.Upsert(ChatMessageData.FromInfo(message));
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<List<ChatMessageInfo>> GetMessagesAsync(ChatMessageType messageType, string receiver, int limit,
            DateTime? beforeUtc = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            receiver = NormalizeReceiver(messageType, receiver);
            limit = NormalizeLimit(limit);
            int type = (int)messageType;

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                IEnumerable<ChatMessageData> query = messagesCollection.Find(message =>
                    message.MessageType == type &&
                    message.Receiver == receiver);

                if (beforeUtc.HasValue)
                {
                    DateTime before = beforeUtc.Value;
                    query = query.Where(message => message.CreatedAtUtc < before);
                }

                List<ChatMessageInfo> result = query
                    .OrderByDescending(message => message.CreatedAtUtc)
                    .Take(limit)
                    .Select(message => message.ToInfo())
                    .ToList();

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                dbSemaphore.Release();
            }
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

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                IEnumerable<ChatMessageData> query = messagesCollection.Find(message =>
                    message.MessageType == type &&
                    ((message.Sender == userA && message.Receiver == userB) ||
                     (message.Sender == userB && message.Receiver == userA)));

                if (beforeUtc.HasValue)
                {
                    DateTime before = beforeUtc.Value;
                    query = query.Where(message => message.CreatedAtUtc < before);
                }

                List<ChatMessageInfo> result = query
                    .OrderByDescending(message => message.CreatedAtUtc)
                    .Take(limit)
                    .Select(message => message.ToInfo())
                    .ToList();

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public void Dispose()
        {
            CustomProperties?.Clear();
            database?.Dispose();
            dbSemaphore?.Dispose();
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

#endif
