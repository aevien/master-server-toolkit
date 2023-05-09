#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor
    {
        private MongoClient _client;
        private IMongoDatabase _database;

        private IMongoCollection<AccountInfoMongoDB> _accountsCollection;
        private IMongoCollection<PasswordResetDataMongoDB> _resetCodesCollection;
        private IMongoCollection<EmailConfirmationDataMongoDB> _emailConfirmations;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();

        public AccountsDatabaseAccessor(string connectionString, string databaseName)
            : this(new MongoClient(connectionString), databaseName) { }

        public AccountsDatabaseAccessor(MongoClient client, string databaseName)
        {
            _client = client;
            _database = _client.GetDatabase(databaseName);

            _accountsCollection = _database.GetCollection<AccountInfoMongoDB>("accounts");
            _resetCodesCollection = _database.GetCollection<PasswordResetDataMongoDB>("resetCodes");
            _emailConfirmations = _database.GetCollection<EmailConfirmationDataMongoDB>("emailConfirmationCodes");

            // force reindex
            _accountsCollection.Indexes.DropAll();

            _accountsCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountInfoMongoDB>(
                    Builders<AccountInfoMongoDB>.IndexKeys.Ascending(e => e.Username), new CreateIndexOptions() { Unique = true }
                )
            );



            _resetCodesCollection.Indexes.CreateOne(
                new CreateIndexModel<PasswordResetDataMongoDB>(
                    Builders<PasswordResetDataMongoDB>.IndexKeys.Ascending(e => e.Email), new CreateIndexOptions() { Unique = true }
                )
            );

            _emailConfirmations.Indexes.CreateOne(
                new CreateIndexModel<EmailConfirmationDataMongoDB>(
                    Builders<EmailConfirmationDataMongoDB>.IndexKeys.Ascending(e => e.Email), new CreateIndexOptions() { Unique = true }
                )
            );
        }

        public IAccountInfoData CreateAccountInstance()
        {
            return new AccountInfoMongoDB();
        }


        public async Task<IAccountInfoData> GetAccountByIdAsync(string id)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, id);
            return await Task.Run(() =>
            {
                return _accountsCollection.Find(filter).FirstOrDefault();
            });
        }


        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Username, username);
            return await Task.Run(() =>
            {
                return _accountsCollection.Find(filter).FirstOrDefault();
            });
        }


        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Email, email);
            return await Task.Run(() =>
            {
                return _accountsCollection.Find(filter).FirstOrDefault();
            });
        }


        public async Task<IAccountInfoData> GetAccountByPhoneNumberAsync(string phoneNumber)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.PhoneNumber, phoneNumber);
            return await Task.Run(() =>
            {
                return _accountsCollection.Find(filter).FirstOrDefault();
            });
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Token, token);
            return await Task.Run(() =>
            {
                return _accountsCollection.Find(filter).FirstOrDefault();
            });
        }

        public async Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.DeviceId, deviceId.ToLower());
            return await Task.Run(() =>
            {
                return _accountsCollection.Find(filter).FirstOrDefault();
            });
        }

        public async Task SavePasswordResetCodeAsync(IAccountInfoData account, string code)
        {
            await Task.Run(() =>
            {
                _resetCodesCollection.DeleteMany(i => i.Email == account.Email.ToLower());
                _resetCodesCollection.InsertOne(new PasswordResetDataMongoDB()
                {
                    Email = account.Email,
                    Code = code
                });
            });
        }

        public async Task<string> GetPasswordResetDataAsync(string email)
        {
            PasswordResetDataMongoDB data = default;

            var filter = Builders<PasswordResetDataMongoDB>.Filter.Eq(i => i.Email, email.ToLower());

            await Task.Run(() =>
            {
                data = _resetCodesCollection.Find(filter).FirstOrDefault();
            });

            return data != null ? data.Code : "";
        }


        public async Task SaveEmailConfirmationCodeAsync(string email, string code)
        {
            await Task.Run(() =>
            {
                _emailConfirmations.DeleteMany(i => i.Email == email.ToLower());
                _emailConfirmations.InsertOne(new EmailConfirmationDataMongoDB()
                {
                    Code = code,
                    Email = email
                });
            });
        }
        public async Task<string> GetEmailConfirmationCodeAsync(string email)
        {
            return await Task.Run(() =>
            {
                var entry = _emailConfirmations.Find(i => i.Email == email).First();
                return entry != null ? entry.Code : string.Empty;
            });
        }


        public async Task<bool> UpdateAccountAsync(IAccountInfoData account)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id);

            return await Task.Run(() =>
            {
                _accountsCollection.ReplaceOne(filter, account as AccountInfoMongoDB);
                return true;
            });
        }


        public async Task<string> InsertNewAccountAsync(IAccountInfoData account)
        {
            var acc = account as AccountInfoMongoDB;
            await Task.Run(() =>
            {
                _accountsCollection.InsertOne(acc);
            });
            return acc.Id;
        }

        public async Task<bool> InsertTokenAsync(IAccountInfoData account, string token)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id);
            var update = Builders<AccountInfoMongoDB>.Update.Set(e => e.Token, token);

            return await Task.Run(() =>
            {
                account.Token = token;
                _accountsCollection.UpdateOne(filter, update);
                return true;
            });
        }

        public Task<string> GetPhoneNumberConfirmationCodeAsync(string phoneNumber)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CheckPhoneNumberConfirmationCodeAsync(string confirmationCode)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public Task<IAccountInfoData> GetAccountByPropertyAsync(string propertyKey, string propertyValue)
        {
            throw new NotImplementedException();
        }
    }
}
#endif