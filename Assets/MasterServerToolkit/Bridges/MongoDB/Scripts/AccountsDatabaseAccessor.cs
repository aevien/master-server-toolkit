#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor
    {
        private readonly MongoClient client;
        private readonly IMongoDatabase database;

        private readonly IMongoCollection<AccountInfoMongoDB> accountsCollection;
        private readonly IMongoCollection<ExtraPropertyDataMongoDB> extraPropertiesCollection;
        private readonly IMongoCollection<PasswordResetDataMongoDB> resetCodesCollection;
        private readonly IMongoCollection<EmailConfirmationDataMongoDB> emailConfirmations;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public AccountsDatabaseAccessor(string connectionString, string databaseName)
            : this(new MongoClient(connectionString), databaseName) { }

        public AccountsDatabaseAccessor(MongoClient client, string databaseName)
        {
            this.client = client;
            database = this.client.GetDatabase(databaseName);

            accountsCollection = database.GetCollection<AccountInfoMongoDB>("accounts");
            extraPropertiesCollection = database.GetCollection<ExtraPropertyDataMongoDB>("extraProperties");
            resetCodesCollection = database.GetCollection<PasswordResetDataMongoDB>("resetCodes");
            emailConfirmations = database.GetCollection<EmailConfirmationDataMongoDB>("emailConfirmationCodes");

            // force reindex
            accountsCollection.Indexes.DropAll();

            accountsCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountInfoMongoDB>(
                    Builders<AccountInfoMongoDB>.IndexKeys.Ascending(e => e.Username), new CreateIndexOptions() {Unique = true}
                )
            );

            extraPropertiesCollection.Indexes.CreateOne(
                new CreateIndexModel<ExtraPropertyDataMongoDB>(
                    Builders<ExtraPropertyDataMongoDB>.IndexKeys
                        .Ascending(e => e.PropertyKey)
                        .Ascending(e => e.PropertyValue),
                    new CreateIndexOptions {Unique = true}
                )
            );

            resetCodesCollection.Indexes.CreateOne(
                new CreateIndexModel<PasswordResetDataMongoDB>(
                    Builders<PasswordResetDataMongoDB>.IndexKeys.Ascending(e => e.Email), new CreateIndexOptions() { Unique = true }
                )
            );

            emailConfirmations.Indexes.CreateOne(
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
            var account = await accountsCollection.Find(filter).FirstOrDefaultAsync();
            if (account != null)
            {
                account.LastLogin = DateTime.UtcNow;
                await accountsCollection.ReplaceOneAsync(Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id), account);
                account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
            }

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Username, username);
            var account = await accountsCollection.Find(filter).FirstOrDefaultAsync();
            if (account != null)
            {
                account.LastLogin = DateTime.UtcNow;
                await accountsCollection.ReplaceOneAsync(Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id), account);
                account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
            }

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Email, email);
            var account = await accountsCollection.Find(filter).FirstOrDefaultAsync();
            if (account != null)
            {
                account.LastLogin = DateTime.UtcNow;
                await accountsCollection.ReplaceOneAsync(Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id), account);
                account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
            }

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string phoneNumber)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.PhoneNumber, phoneNumber);
            return await accountsCollection.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Token, token);
            var account = await accountsCollection.Find(filter).FirstOrDefaultAsync();
            if (account != null)
            {
                account.LastLogin = DateTime.UtcNow;
                await accountsCollection.ReplaceOneAsync(Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id), account);
                account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
            }

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.DeviceId, deviceId.ToLower());
            var account = await accountsCollection.Find(filter).FirstOrDefaultAsync();
            if (account != null)
            {
                account.LastLogin = DateTime.UtcNow;
                await accountsCollection.ReplaceOneAsync(Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id), account);
                account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
            }

            return account;
        }

        public async Task SavePasswordResetCodeAsync(string email, string code)
        {
            await resetCodesCollection.DeleteManyAsync(i => i.Email == email.ToLower());
            await resetCodesCollection.InsertOneAsync(new PasswordResetDataMongoDB()
            {
                Email = email.ToLower(),
                Code = code
            });
        }

        public async Task<string> CheckPasswordResetCodeAsync(string email)
        {
            PasswordResetDataMongoDB data = default;

            var filter = Builders<PasswordResetDataMongoDB>.Filter.Eq(i => i.Email, email.ToLower());
            data = await resetCodesCollection.Find(filter).FirstOrDefaultAsync();

            return data != null ? data.Code : "";
        }

        public async Task SaveEmailConfirmationCodeAsync(string email, string code)
        {
            await emailConfirmations.DeleteManyAsync(i => i.Email == email.ToLower());
            await emailConfirmations.InsertOneAsync(new EmailConfirmationDataMongoDB()
            {
                Code = code,
                Email = email
            });
        }

        public async Task<bool> CheckEmailConfirmationCodeAsync(string email, string code)
        {
            if (string.IsNullOrEmpty(email)) return false;
            if (string.IsNullOrEmpty(code)) return false;

            var entry = await emailConfirmations.Find(i => i.Email == email).FirstOrDefaultAsync();
            if (entry != null && entry.Code == code)
            {
                await emailConfirmations.DeleteOneAsync(i => i.Email == email.ToLower());
                return true;
            }

            return false;
        }

        public async Task UpdateAccountAsync(IAccountInfoData account)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id);
            await accountsCollection.ReplaceOneAsync(filter, account as AccountInfoMongoDB);
            await InsertOrUpdateExtraProperties(account.Id, account.ExtraProperties);
        }

        public async Task<string> InsertAccountAsync(IAccountInfoData account)
        {
            var acc = account as AccountInfoMongoDB;
            await accountsCollection.InsertOneAsync(acc);
            await InsertOrUpdateExtraProperties(acc.Id, account.ExtraProperties);
            return acc.Id;
        }

        public async Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id);
            var update = Builders<AccountInfoMongoDB>.Update.Set(e => e.Token, token);
            account.Token = token;
            await accountsCollection.UpdateOneAsync(filter, update);
        }

        public Task<string> GetPhoneNumberConfirmationCodeAsync(string phoneNumber)
        {
            throw new NotImplementedException();
        }

        public Task<bool> CheckPhoneNumberConfirmationCodeAsync(string confirmationCode)
        {
            throw new NotImplementedException();
        }

        public void Dispose() { }

        public Task<IAccountInfoData> GetAccountByPropertyAsync(string propertyKey, string propertyValue)
        {
            throw new NotImplementedException();
        }

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue)
        {
            var filter = Builders<ExtraPropertyDataMongoDB>.Filter.And(
                Builders<ExtraPropertyDataMongoDB>.Filter.Eq(e => e.PropertyKey, propertyKey),
                Builders<ExtraPropertyDataMongoDB>.Filter.Eq(e => e.PropertyValue, propertyValue)
            );
            var extraProperty = await extraPropertiesCollection.Find(filter).FirstOrDefaultAsync();
            if (extraProperty == null)
            {
                return null;
            }

            var accountFilter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, extraProperty.AccountId);
            return await accountsCollection.Find(accountFilter).FirstOrDefaultAsync();
        }

        private async Task InsertOrUpdateExtraProperties(string accountId, Dictionary<string, string> properties)
        {
            foreach (var (key, value) in properties)
            {
                var filter = Builders<ExtraPropertyDataMongoDB>.Filter.And(
                    Builders<ExtraPropertyDataMongoDB>.Filter.Eq(e => e.AccountId, accountId),
                    Builders<ExtraPropertyDataMongoDB>.Filter.Eq(e => e.PropertyKey, key)
                );

                var update = Builders<ExtraPropertyDataMongoDB>.Update.Set(e => e.PropertyValue, value);

                var options = new UpdateOptions {IsUpsert = true};

                await extraPropertiesCollection.UpdateOneAsync(filter, update, options);
            }
        }

        public async Task<Dictionary<string, string>> GetExtraPropertiesAsync(string accountId)
        {
            var filter = Builders<ExtraPropertyDataMongoDB>.Filter.Eq(e => e.AccountId, accountId);
            var extraProperties = await extraPropertiesCollection.Find(filter).ToListAsync();
            return extraProperties.ToDictionary(e => e.PropertyKey, e => e.PropertyValue);
        }

        public async Task<bool> CheckPasswordResetCodeAsync(string email, string code)
        {
            var filter = Builders<PasswordResetDataMongoDB>.Filter.Eq(i => i.Email, email.ToLower());
            var entry = await resetCodesCollection.Find(filter).FirstOrDefaultAsync();
            if (entry != null && entry.Code == code)
            {
                await resetCodesCollection.DeleteManyAsync(filter);
                return true;
            }

            return false;
        }
    }
}
#endif