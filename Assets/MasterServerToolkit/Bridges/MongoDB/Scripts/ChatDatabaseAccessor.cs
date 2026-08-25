#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR

using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class ChatDatabaseAccessor : IChatDatabaseAccessor
    {
        private const int DefaultLimit = 100;
        private const int MaxLimit = 500;
        private const string CollectionName = "chat_messages";

        private readonly MongoClient client;
        private readonly IMongoDatabase database;
        private readonly IMongoCollection<ChatMessageMongoDB> messagesCollection;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public ChatDatabaseAccessor(string connectionString, string databaseName)
            : this(new MongoClient(connectionString), databaseName) { }

        public ChatDatabaseAccessor(MongoClient client, string databaseName)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            database = this.client.GetDatabase(databaseName);
            messagesCollection = database.GetCollection<ChatMessageMongoDB>(CollectionName);

            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<ChatMessageMongoDB>(
                    Builders<ChatMessageMongoDB>.IndexKeys
                        .Ascending(message => message.MessageType)
                        .Ascending(message => message.Receiver)
                        .Descending(message => message.CreatedAtUtc)));

            messagesCollection.Indexes.CreateOne(
                new CreateIndexModel<ChatMessageMongoDB>(
                    Builders<ChatMessageMongoDB>.IndexKeys
                        .Ascending(message => message.Sender)
                        .Ascending(message => message.Receiver)
                        .Descending(message => message.CreatedAtUtc)));
        }

        public async Task SaveMessageAsync(ChatMessageInfo message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            ChatMessageMongoDB messageData = ChatMessageMongoDB.FromInfo(message);
            cancellationToken.ThrowIfCancellationRequested();
            await messagesCollection.InsertOneAsync(
                messageData,
                cancellationToken: CancellationToken.None);
        }

        public async Task<List<ChatMessageInfo>> GetMessagesAsync(ChatMessageType messageType, string receiver, int limit,
            DateTime? beforeUtc = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            receiver = NormalizeReceiver(messageType, receiver);
            limit = NormalizeLimit(limit);
            int type = (int)messageType;

            var filter = Builders<ChatMessageMongoDB>.Filter.And(
                Builders<ChatMessageMongoDB>.Filter.Eq(message => message.MessageType, type),
                Builders<ChatMessageMongoDB>.Filter.Eq(message => message.Receiver, receiver));

            if (beforeUtc.HasValue)
            {
                filter = Builders<ChatMessageMongoDB>.Filter.And(
                    filter,
                    Builders<ChatMessageMongoDB>.Filter.Lt(message => message.CreatedAtUtc, beforeUtc.Value));
            }

            List<ChatMessageMongoDB> messages = await messagesCollection
                .Find(filter)
                .SortByDescending(message => message.CreatedAtUtc)
                .Limit(limit)
                .ToListAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return messages.ConvertAll(message => message.ToInfo());
        }

        public async Task<List<ChatMessageInfo>> GetPrivateMessagesAsync(string userA, string userB, int limit,
            DateTime? beforeUtc = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            userA = NormalizeParticipant(userA);
            userB = NormalizeParticipant(userB);
            limit = NormalizeLimit(limit);

            if (string.IsNullOrWhiteSpace(userA) || string.IsNullOrWhiteSpace(userB))
                return new List<ChatMessageInfo>();

            int type = (int)ChatMessageType.Private;
            var builder = Builders<ChatMessageMongoDB>.Filter;

            var filter = builder.And(
                builder.Eq(message => message.MessageType, type),
                builder.Or(
                    builder.And(
                        builder.Eq(message => message.Sender, userA),
                        builder.Eq(message => message.Receiver, userB)),
                    builder.And(
                        builder.Eq(message => message.Sender, userB),
                        builder.Eq(message => message.Receiver, userA))));

            if (beforeUtc.HasValue)
                filter = builder.And(filter, builder.Lt(message => message.CreatedAtUtc, beforeUtc.Value));

            List<ChatMessageMongoDB> messages = await messagesCollection
                .Find(filter)
                .SortByDescending(message => message.CreatedAtUtc)
                .Limit(limit)
                .ToListAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            return messages.ConvertAll(message => message.ToInfo());
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

#endif
