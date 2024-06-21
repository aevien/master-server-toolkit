#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.Json;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using System;
using System.Linq;
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

        public async Task RestoreProfileAsync(ObservableServerProfile profile)
        {
            try
            {
                var data = await FindOrCreateData(profile);
                var json = new MstJson(data.Document.ToDictionary(x => x.Key, x => x.Value));
                profile.FromJson(json);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
            }
        }

        public async Task UpdateProfileAsync(ObservableServerProfile profile)
        {
            var data = await FindOrCreateData(profile);
            data.Document = profile.ToJson().ToDictionary();

            var filter = Builders<ProfileInfoDocumentMongoDB>.Filter.Eq(e => e.UserId, profile.UserId);

            await Task.Run(() =>
            {
                _profiles.ReplaceOne(filter, data);
            });
        }

        private async Task<ProfileInfoDocumentMongoDB> FindOrCreateData(ObservableServerProfile profile)
        {
            string userId = profile.UserId;

            var data = await Task.Run(() =>
            {
                return _profiles.Find(a => a.UserId == userId).FirstOrDefault();
            });

            if (data == null)
            {
                data = new ProfileInfoDocumentMongoDB()
                {
                    UserId = profile.UserId,
                    Document = profile.ToJson().ToDictionary()
                };

                await Task.Run(() =>
                {
                    _profiles.InsertOne(data);
                });
            }

            return data;
        }
    }
}
#endif