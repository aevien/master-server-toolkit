#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
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
    public class ProfilesDatabaseAccessor : IProfilesDatabaseAccessor
    {
        private MongoClient _client;
        private IMongoDatabase _database;

        private IMongoCollection<ProfileInfoDataMongoDB> _profiles;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public ProfilesDatabaseAccessor(string connectionString, string databaseName)
            : this(new MongoClient(connectionString), databaseName) { }

        public ProfilesDatabaseAccessor(MongoClient client, string databaseName)
        {
            _client = client;
            _database = _client.GetDatabase(databaseName);

            _profiles = _database.GetCollection<ProfileInfoDataMongoDB>("profiles");

            _profiles.Indexes.CreateOne(
                new CreateIndexModel<ProfileInfoDataMongoDB>(
                    Builders<ProfileInfoDataMongoDB>.IndexKeys.Ascending(e => e.UserId), new CreateIndexOptions() { Unique = true }
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
                    profile.FromBytes(data.Data);
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

        private async Task<ProfileInfoDataMongoDB> FindOrCreateData(ObservableServerProfile profile,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string userId = profile.UserId;

            var data = await _profiles
                .Find(a => a.UserId == userId)
                .FirstOrDefaultAsync(cancellationToken);

            if (data == null)
            {
                data = new ProfileInfoDataMongoDB()
                {
                    UserId = profile.UserId,
                    Data = profile.ToBytes()
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
            List<MongoBinaryProfileSaveSnapshot> snapshots =
                MongoProfileSaveSnapshotFactory.CreateBinarySnapshots(profiles);
            cancellationToken.ThrowIfCancellationRequested();

            if (snapshots.Count == 0)
                return;

            var bulkOperations = new List<WriteModel<ProfileInfoDataMongoDB>>(snapshots.Count);

            foreach (MongoBinaryProfileSaveSnapshot snapshot in snapshots)
            {
                var filter = Builders<ProfileInfoDataMongoDB>.Filter.Eq(e => e.UserId, snapshot.UserId);
                var update = Builders<ProfileInfoDataMongoDB>.Update
                    .Set(e => e.Data, snapshot.Data)
                    .SetOnInsert(e => e.UserId, snapshot.UserId);

                bulkOperations.Add(new UpdateOneModel<ProfileInfoDataMongoDB>(filter, update)
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
