#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class ProfilesDocumentDatabaseAccessor : IProfilesDatabaseAccessor
    {
        private MongoClient _client;
        private IMongoDatabase _database;

        private IMongoCollection<ProfileInfoDocumentMongoDB> _profiles;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public ProfilesDocumentDatabaseAccessor(string connectionString, string databaseName)
            : this(new MongoClient(connectionString), databaseName) { }

        public ProfilesDocumentDatabaseAccessor(MongoClient client, string databaseName)
        {
            _client = client;
            _database = _client.GetDatabase(databaseName);

            _profiles = _database.GetCollection<ProfileInfoDocumentMongoDB>("profiles");

            _profiles.Indexes.CreateOne(
                new CreateIndexModel<ProfileInfoDocumentMongoDB>(
                    Builders<ProfileInfoDocumentMongoDB>.IndexKeys.Ascending(e => e.UserId), new CreateIndexOptions() { Unique = true }
                )
            );
        }

        public void Dispose() { }

        public async Task RestoreProfileAsync(ObservableServerProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RestoreProfilesAsync(new[] { profile }, cancellationToken);
        }

        public async Task RestoreProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (profiles == null)
                throw new ArgumentNullException(nameof(profiles));

            var profilesList = profiles as IList<ObservableServerProfile> ?? profiles.ToList();

            foreach (var profile in profilesList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (profile == null)
                    continue;

                try
                {
                    var data = await FindOrCreateData(profile, cancellationToken);
                    var json = MstJson.Create(data.Document.ToDictionary(x => x.Key, x => x.Value));
                    profile.FromJson(json);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        public Task<DatabaseEntriesInfo<IProfilePropertyData>> Search(Dictionary<string, object> filter,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DatabaseEntriesInfo<IProfilePropertyData>());
        }

        public Task UpdateProfileAsync(ObservableServerProfile profile,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            return UpdateProfilesAsync(new[] { profile }, cancellationToken);
        }

        private async Task<ProfileInfoDocumentMongoDB> FindOrCreateData(ObservableServerProfile profile,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string userId = profile.UserId;

            var data = await _profiles
                .Find(a => a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (data == null)
            {
                data = new ProfileInfoDocumentMongoDB()
                {
                    UserId = profile.UserId,
                    Document = profile.ToJson().ToDictionary()
                };

                cancellationToken.ThrowIfCancellationRequested();
                await _profiles.InsertOneAsync(data, cancellationToken: CancellationToken.None);
            }

            return data;
        }

        public async Task UpdateProfilesAsync(IEnumerable<ObservableServerProfile> profiles,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<MongoDocumentProfileSaveSnapshot> snapshots =
                MongoProfileSaveSnapshotFactory.CreateDocumentSnapshots(profiles);
            cancellationToken.ThrowIfCancellationRequested();

            if (snapshots.Count == 0)
                return;

            var bulkOperations = new List<WriteModel<ProfileInfoDocumentMongoDB>>(snapshots.Count);

            foreach (MongoDocumentProfileSaveSnapshot snapshot in snapshots)
            {
                var filter = Builders<ProfileInfoDocumentMongoDB>.Filter.Eq(e => e.UserId, snapshot.UserId);
                var update = Builders<ProfileInfoDocumentMongoDB>.Update
                    .Set(e => e.Document, snapshot.Document)
                    .SetOnInsert(e => e.UserId, snapshot.UserId);

                bulkOperations.Add(new UpdateOneModel<ProfileInfoDocumentMongoDB>(filter, update)
                {
                    IsUpsert = true
                });
            }

            cancellationToken.ThrowIfCancellationRequested();
            await _profiles.BulkWriteAsync(
                bulkOperations,
                cancellationToken: CancellationToken.None);
        }
    }
}
#endif
