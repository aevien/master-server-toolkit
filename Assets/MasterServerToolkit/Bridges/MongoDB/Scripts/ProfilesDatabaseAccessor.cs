#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using System;
using System.Linq;
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

        public async Task RestoreProfileAsync(ObservableServerProfile profile)
        {
            try
            {
                var data = await FindOrCreateData(profile);
                profile.FromBytes(data.Data);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError(e);
            }
        }

        public async Task UpdateProfileAsync(ObservableServerProfile profile)
        {
            var data = await FindOrCreateData(profile);
            data.Data = profile.ToBytes();

            var filter = Builders<ProfileInfoDataMongoDB>.Filter.Eq(e => e.UserId, profile.UserId);

            await Task.Run(() =>
            {
                _profiles.ReplaceOne(filter, data);
            });
        }

        private async Task<ProfileInfoDataMongoDB> FindOrCreateData(ObservableServerProfile profile)
        {
            string userId = profile.UserId;

            var data = await Task.Run(() =>
            {
                return _profiles.Find(a => a.UserId == userId).FirstOrDefault();
            });

            if (data == null)
            {
                data = new ProfileInfoDataMongoDB()
                {
                    UserId = profile.UserId,
                    Data = profile.ToBytes()
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