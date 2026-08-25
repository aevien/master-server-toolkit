#if (!UNITY_WEBGL && !UNITY_IOS) || UNITY_EDITOR
using MasterServerToolkit.Logging;
using MasterServerToolkit.GameService;
using MasterServerToolkit.MasterServer;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.MongoDB
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor
    {
        private readonly MongoClient client;
        private readonly IMongoDatabase database;

        private readonly IMongoCollection<AccountInfoMongoDB> accountsCollection;
        private readonly IMongoCollection<AccountBlockMongoDB> accountBlocksCollection;
        private readonly IMongoCollection<AccountServiceBindingMongoDB> serviceBindingsCollection;
        private readonly IMongoCollection<PasswordResetDataMongoDB> resetCodesCollection;
        private readonly IMongoCollection<EmailConfirmationDataMongoDB> emailConfirmations;
        private readonly IMongoCollection<AuthTokenRevisionMongoDB> authTokenRevisionsCollection;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public AccountsDatabaseAccessor(string connectionString, string databaseName)
            : this(new MongoClient(connectionString), databaseName) { }

        public AccountsDatabaseAccessor(MongoClient client, string databaseName)
        {
            this.client = client;
            database = this.client.GetDatabase(databaseName);

            accountsCollection = database.GetCollection<AccountInfoMongoDB>("accounts");
            accountBlocksCollection = database.GetCollection<AccountBlockMongoDB>("account_blocks");
            serviceBindingsCollection = database.GetCollection<AccountServiceBindingMongoDB>("account_service_bindings");
            resetCodesCollection = database.GetCollection<PasswordResetDataMongoDB>("resetCodes");
            emailConfirmations = database.GetCollection<EmailConfirmationDataMongoDB>("emailConfirmationCodes");
            authTokenRevisionsCollection = database.GetCollection<AuthTokenRevisionMongoDB>("auth_token_revisions");

            // force reindex
            accountsCollection.Indexes.DropAll();

            accountsCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountInfoMongoDB>(
                    Builders<AccountInfoMongoDB>.IndexKeys.Ascending(e => e.Username), new CreateIndexOptions() { Unique = true }
                )
            );

            serviceBindingsCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountServiceBindingMongoDB>(
                    Builders<AccountServiceBindingMongoDB>.IndexKeys.Ascending(e => e.Id), new CreateIndexOptions() { Unique = true }
                )
            );

            serviceBindingsCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountServiceBindingMongoDB>(
                    Builders<AccountServiceBindingMongoDB>.IndexKeys.Ascending(e => e.AccountId)
                )
            );

            serviceBindingsCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountServiceBindingMongoDB>(
                    Builders<AccountServiceBindingMongoDB>.IndexKeys
                        .Ascending(e => e.ServiceId)
                        .Ascending(e => e.PlayerId),
                    new CreateIndexOptions { Unique = true }
                )
            );

            serviceBindingsCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountServiceBindingMongoDB>(
                    Builders<AccountServiceBindingMongoDB>.IndexKeys
                        .Ascending(e => e.AccountId)
                        .Ascending(e => e.ServiceId),
                    new CreateIndexOptions { Unique = true }
                )
            );

            accountBlocksCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountBlockMongoDB>(
                    Builders<AccountBlockMongoDB>.IndexKeys.Ascending(e => e.Id), new CreateIndexOptions() { Unique = true }
                )
            );

            accountBlocksCollection.Indexes.CreateOne(
                new CreateIndexModel<AccountBlockMongoDB>(
                    Builders<AccountBlockMongoDB>.IndexKeys.Ascending(e => e.AccountId)
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

        public IAccountBlockData CreateAccountBlockInstance()
        {
            return new AccountBlockMongoDB();
        }

        public IAccountServiceBindingData CreateServiceBindingInstance()
        {
            return new AccountServiceBindingMongoDB();
        }

        private static void PrepareAccountBlock(IAccountBlockData block)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            block.Id = string.IsNullOrWhiteSpace(block.Id) ? Mst.Helper.CreateGuidString() : block.Id.Trim();
            block.AccountId = block.AccountId?.Trim() ?? string.Empty;
            block.BlockReason = block.BlockReason?.Trim() ?? string.Empty;
            block.RemoveReason = block.RemoveReason?.Trim() ?? string.Empty;

            if (block.CreatedAt == default)
                block.CreatedAt = DateTime.UtcNow;
        }

        private static AccountBlockMongoDB ToAccountBlockData(IAccountBlockData block)
        {
            if (block is AccountBlockMongoDB blockData)
                return blockData;

            return new AccountBlockMongoDB
            {
                Id = block.Id,
                AccountId = block.AccountId,
                BlockReason = block.BlockReason,
                BlockedUntil = block.BlockedUntil,
                CreatedAt = block.CreatedAt,
                RemovedAt = block.RemovedAt,
                RemoveReason = block.RemoveReason
            };
        }

        private static void PrepareServiceBinding(IAccountServiceBindingData binding)
        {
            if (binding == null)
                throw new ArgumentNullException(nameof(binding));

            DateTime utcNow = DateTime.UtcNow;

            binding.AccountId = binding.AccountId?.Trim() ?? string.Empty;
            binding.ServiceId = binding.ServiceId?.Trim() ?? string.Empty;
            binding.PlayerId = binding.PlayerId?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(binding.AccountId) ||
                string.IsNullOrEmpty(binding.ServiceId) ||
                string.IsNullOrEmpty(binding.PlayerId))
            {
                throw new ArgumentException("Service binding account, service and player identifiers are required", nameof(binding));
            }

            binding.Id = $"{binding.AccountId}:{binding.ServiceId}";

            if (binding.CreatedAt == default)
                binding.CreatedAt = utcNow;

            binding.UpdatedAt = utcNow;
            binding.LastLoginAt = utcNow;
        }

        private static AccountServiceBindingMongoDB ToServiceBindingData(IAccountServiceBindingData binding)
        {
            if (binding is AccountServiceBindingMongoDB bindingData)
                return bindingData;

            return new AccountServiceBindingMongoDB
            {
                Id = binding.Id,
                AccountId = binding.AccountId,
                ServiceId = binding.ServiceId,
                PlayerId = binding.PlayerId,
                PlayerName = binding.PlayerName,
                IsGuest = binding.IsGuest,
                CreatedAt = binding.CreatedAt,
                UpdatedAt = binding.UpdatedAt,
                LastLoginAt = binding.LastLoginAt
            };
        }

        public async Task<IAccountServiceBindingData> GetServiceBindingAsync(GameServiceId serviceId, string playerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedServiceId = serviceId.ToString();
            string normalizedPlayerId = playerId?.Trim();
            var filter = Builders<AccountServiceBindingMongoDB>.Filter.And(
                Builders<AccountServiceBindingMongoDB>.Filter.Eq(e => e.ServiceId, normalizedServiceId),
                Builders<AccountServiceBindingMongoDB>.Filter.Eq(e => e.PlayerId, normalizedPlayerId));

            return await serviceBindingsCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<IAccountServiceBindingData>> GetServiceBindingsByAccountIdAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = Builders<AccountServiceBindingMongoDB>.Filter.Eq(e => e.AccountId, accountId);

            List<AccountServiceBindingMongoDB> bindings =
                await serviceBindingsCollection.Find(filter).ToListAsync(cancellationToken);

            return bindings.Cast<IAccountServiceBindingData>().ToList();
        }

        public async Task UpsertServiceBindingAsync(IAccountServiceBindingData binding, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareServiceBinding(binding);

            var bindingData = ToServiceBindingData(binding);
            var filter = Builders<AccountServiceBindingMongoDB>.Filter.Eq(e => e.Id, bindingData.Id);

            cancellationToken.ThrowIfCancellationRequested();
            await serviceBindingsCollection.ReplaceOneAsync(
                filter,
                bindingData,
                new ReplaceOptions { IsUpsert = true },
                CancellationToken.None);
        }

        public async Task ReplaceServiceBindingAsync(IAccountServiceBindingData binding, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareServiceBinding(binding);

            var bindingData = ToServiceBindingData(binding);
            var existingBindingFilter = Builders<AccountServiceBindingMongoDB>.Filter.Eq(
                existingBinding => existingBinding.Id,
                bindingData.Id);
            var update = Builders<AccountServiceBindingMongoDB>.Update
                .Set(existingBinding => existingBinding.AccountId, bindingData.AccountId)
                .Set(existingBinding => existingBinding.ServiceId, bindingData.ServiceId)
                .Set(existingBinding => existingBinding.PlayerId, bindingData.PlayerId)
                .Set(existingBinding => existingBinding.PlayerName, bindingData.PlayerName)
                .Set(existingBinding => existingBinding.IsGuest, bindingData.IsGuest)
                .Set(existingBinding => existingBinding.UpdatedAt, bindingData.UpdatedAt)
                .Set(existingBinding => existingBinding.LastLoginAt, bindingData.LastLoginAt)
                .SetOnInsert(existingBinding => existingBinding.Id, bindingData.Id)
                .SetOnInsert(existingBinding => existingBinding.CreatedAt, bindingData.CreatedAt);
            var options = new FindOneAndUpdateOptions<AccountServiceBindingMongoDB>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            cancellationToken.ThrowIfCancellationRequested();
            AccountServiceBindingMongoDB persistedBinding = await serviceBindingsCollection.FindOneAndUpdateAsync(
                existingBindingFilter,
                update,
                options,
                CancellationToken.None);

            if (persistedBinding == null)
                throw new InvalidOperationException("MongoDB did not return the replaced service binding");

            binding.Id = persistedBinding.Id;
            binding.CreatedAt = persistedBinding.CreatedAt;
            binding.UpdatedAt = persistedBinding.UpdatedAt;
            binding.LastLoginAt = persistedBinding.LastLoginAt;
        }

        public async Task DeleteServiceBindingAsync(string accountId, GameServiceId serviceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = Builders<AccountServiceBindingMongoDB>.Filter.And(
                Builders<AccountServiceBindingMongoDB>.Filter.Eq(e => e.AccountId, accountId),
                Builders<AccountServiceBindingMongoDB>.Filter.Eq(e => e.ServiceId, serviceId.ToString())
            );

            cancellationToken.ThrowIfCancellationRequested();
            await serviceBindingsCollection.DeleteManyAsync(filter, CancellationToken.None);
        }

        public async Task<IAccountBlockData> GetActiveAccountBlockAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return null;

            DateTime utcNow = DateTime.UtcNow;
            var filter = Builders<AccountBlockMongoDB>.Filter.And(
                Builders<AccountBlockMongoDB>.Filter.Eq(e => e.AccountId, accountId),
                Builders<AccountBlockMongoDB>.Filter.Eq(e => e.RemovedAt, null),
                Builders<AccountBlockMongoDB>.Filter.Gt(e => e.BlockedUntil, utcNow)
            );

            return await accountBlocksCollection
                .Find(filter)
                .SortByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<DatabaseEntriesInfo<IAccountBlockData>> GetAccountBlockHistoryAsync(string accountId, int size, int page, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return new DatabaseEntriesInfo<IAccountBlockData>();

            int skip = page * size;
            var filter = Builders<AccountBlockMongoDB>.Filter.Eq(e => e.AccountId, accountId);

            long totalCount = await accountBlocksCollection.CountDocumentsAsync(
                filter,
                cancellationToken: cancellationToken);
            List<AccountBlockMongoDB> pageItems = await accountBlocksCollection
                .Find(filter)
                .SortByDescending(e => e.CreatedAt)
                .Skip(skip)
                .Limit(size)
                .ToListAsync(cancellationToken);
            int total = (int)totalCount;

            return new DatabaseEntriesInfo<IAccountBlockData>
            {
                entries = pageItems.Cast<IAccountBlockData>().ToList(),
                total = total,
                filtered = total
            };
        }

        public async Task<string> InsertAccountBlockAsync(IAccountBlockData block, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareAccountBlock(block);
            DateTime utcNow = DateTime.UtcNow;

            var activeFilter = Builders<AccountBlockMongoDB>.Filter.And(
                Builders<AccountBlockMongoDB>.Filter.Eq(e => e.AccountId, block.AccountId),
                Builders<AccountBlockMongoDB>.Filter.Eq(e => e.RemovedAt, null),
                Builders<AccountBlockMongoDB>.Filter.Gt(e => e.BlockedUntil, utcNow)
            );

            var update = Builders<AccountBlockMongoDB>.Update
                .Set(e => e.RemovedAt, utcNow)
                .Set(e => e.RemoveReason, "Replaced by a new account block");
            AccountBlockMongoDB blockData = ToAccountBlockData(block);

            cancellationToken.ThrowIfCancellationRequested();
            await accountBlocksCollection.UpdateManyAsync(activeFilter, update, cancellationToken: CancellationToken.None);
            await accountBlocksCollection.InsertOneAsync(
                blockData,
                cancellationToken: CancellationToken.None);

            return block.Id;
        }

        public async Task<bool> UnblockAccountAsync(string accountId, string removeReason, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return false;

            DateTime utcNow = DateTime.UtcNow;
            var activeFilter = Builders<AccountBlockMongoDB>.Filter.And(
                Builders<AccountBlockMongoDB>.Filter.Eq(e => e.AccountId, accountId),
                Builders<AccountBlockMongoDB>.Filter.Eq(e => e.RemovedAt, null),
                Builders<AccountBlockMongoDB>.Filter.Gt(e => e.BlockedUntil, utcNow)
            );

            var update = Builders<AccountBlockMongoDB>.Update
                .Set(e => e.RemovedAt, utcNow)
                .Set(e => e.RemoveReason, removeReason?.Trim() ?? string.Empty);

            cancellationToken.ThrowIfCancellationRequested();
            var result = await accountBlocksCollection.UpdateManyAsync(
                activeFilter,
                update,
                cancellationToken: CancellationToken.None);
            return result.ModifiedCount > 0;
        }

        public async Task<IAccountInfoData> GetAccountByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, id);
            return await accountsCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Username, username);
            return await accountsCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Email, NormalizeEmail(email));
            return await accountsCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string phoneNumber)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.PhoneNumber, phoneNumber);
            return await accountsCollection.Find(filter).FirstOrDefaultAsync(CancellationToken.None);
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Token, token);
            return await accountsCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<DateTime> UpdateLastLoginAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime lastLoginAt = DateTime.UtcNow;
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, accountId);
            var update = Builders<AccountInfoMongoDB>.Update.Set(e => e.LastLoginAt, lastLoginAt);

            cancellationToken.ThrowIfCancellationRequested();
            await accountsCollection.UpdateOneAsync(
                filter,
                update,
                cancellationToken: CancellationToken.None);

            return lastLoginAt;
        }

        public async Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId)
        {
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.DeviceId, deviceId.ToLower());
            return await accountsCollection.Find(filter).FirstOrDefaultAsync(CancellationToken.None);
        }

        public async Task SavePasswordResetCodeAsync(
            string email,
            string code,
            DateTime expiresAt,
            int attempts,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedEmail = ValidateVerificationCodeData(email, code, attempts);
            var filter = Builders<PasswordResetDataMongoDB>.Filter.Eq(entry => entry.Email, normalizedEmail);
            var update = Builders<PasswordResetDataMongoDB>.Update
                .Set(entry => entry.Email, normalizedEmail)
                .Set(entry => entry.Code, code)
                .Set(entry => entry.ExpiresAt, NormalizeUtc(expiresAt))
                .Set(entry => entry.AttemptsLeft, attempts);

            cancellationToken.ThrowIfCancellationRequested();
            await resetCodesCollection.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true },
                CancellationToken.None);
        }

        public Task<VerificationCodeResult> CheckPasswordResetCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            return CheckVerificationCodeAsync(
                resetCodesCollection,
                entry => entry.Email,
                entry => entry.Code,
                entry => entry.ExpiresAt,
                entry => entry.AttemptsLeft,
                email,
                code,
                cancellationToken);
        }

        public async Task SaveEmailConfirmationCodeAsync(
            string email,
            string code,
            DateTime expiresAt,
            int attempts,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedEmail = ValidateVerificationCodeData(email, code, attempts);
            var filter = Builders<EmailConfirmationDataMongoDB>.Filter.Eq(entry => entry.Email, normalizedEmail);
            var update = Builders<EmailConfirmationDataMongoDB>.Update
                .Set(entry => entry.Email, normalizedEmail)
                .Set(entry => entry.Code, code)
                .Set(entry => entry.ExpiresAt, NormalizeUtc(expiresAt))
                .Set(entry => entry.AttemptsLeft, attempts);

            cancellationToken.ThrowIfCancellationRequested();
            await emailConfirmations.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true },
                CancellationToken.None);
        }

        public Task<VerificationCodeResult> CheckEmailConfirmationCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            return CheckVerificationCodeAsync(
                emailConfirmations,
                entry => entry.Email,
                entry => entry.Code,
                entry => entry.ExpiresAt,
                entry => entry.AttemptsLeft,
                email,
                code,
                cancellationToken);
        }

        public async Task<int> GetAuthTokenRevisionAsync(
            string accountId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedAccountId = NormalizeAccountId(accountId);

            if (string.IsNullOrEmpty(normalizedAccountId))
                return 0;

            var filter = Builders<AuthTokenRevisionMongoDB>.Filter
                .Eq(entry => entry.AccountId, normalizedAccountId);
            AuthTokenRevisionMongoDB revision = await authTokenRevisionsCollection
                .Find(filter)
                .FirstOrDefaultAsync(cancellationToken);

            return revision?.Revision ?? 0;
        }

        public async Task<int> IncrementAuthTokenRevisionAsync(
            string accountId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedAccountId = NormalizeAccountId(accountId);

            if (string.IsNullOrEmpty(normalizedAccountId))
                throw new ArgumentException("Account identifier is required", nameof(accountId));

            var filter = Builders<AuthTokenRevisionMongoDB>.Filter
                .Eq(entry => entry.AccountId, normalizedAccountId);
            var update = Builders<AuthTokenRevisionMongoDB>.Update
                .Inc(entry => entry.Revision, 1);
            var options = new FindOneAndUpdateOptions<AuthTokenRevisionMongoDB>
            {
                IsUpsert = true,
                ReturnDocument = ReturnDocument.After
            };

            cancellationToken.ThrowIfCancellationRequested();
            AuthTokenRevisionMongoDB revision = await authTokenRevisionsCollection.FindOneAndUpdateAsync(
                filter,
                update,
                options,
                CancellationToken.None);

            if (revision == null)
                throw new InvalidOperationException("MongoDB did not return the incremented token revision");

            return revision.Revision;
        }

        public async Task UpdateAccountAsync(IAccountInfoData account, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareAccount(account);
            var accountData = account as AccountInfoMongoDB;
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id);

            await ThrowIfDuplicateAccountAsync(accountData, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await accountsCollection.ReplaceOneAsync(
                filter,
                accountData,
                cancellationToken: CancellationToken.None);
        }

        public async Task<string> InsertAccountAsync(IAccountInfoData account, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var acc = account as AccountInfoMongoDB;
            PrepareAccount(acc);

            await ThrowIfDuplicateAccountAsync(acc, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            await accountsCollection.InsertOneAsync(acc, cancellationToken: CancellationToken.None);

            return acc.Id;
        }

        public async Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var filter = Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, account.Id);
            var update = Builders<AccountInfoMongoDB>.Update.Set(e => e.Token, token);

            cancellationToken.ThrowIfCancellationRequested();
            await accountsCollection.UpdateOneAsync(
                filter,
                update,
                cancellationToken: CancellationToken.None);
            account.Token = token;
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

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(propertyKey))
                return null;

            var filter = Builders<AccountInfoMongoDB>.Filter.Eq($"{nameof(AccountInfoMongoDB.ExtraProperties)}.{propertyKey}", propertyValue);

            return await accountsCollection.Find(filter).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task InsertOrUpdateExtraProperties(string accountId, Dictionary<string, string> properties)
        {
            if (string.IsNullOrEmpty(accountId) || properties == null || properties.Count == 0)
                return;

            var updates = properties.Select(pair =>
                Builders<AccountInfoMongoDB>.Update.Set($"{nameof(AccountInfoMongoDB.ExtraProperties)}.{pair.Key}", pair.Value));

            await accountsCollection.UpdateOneAsync(
                Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, accountId),
                Builders<AccountInfoMongoDB>.Update.Combine(updates),
                cancellationToken: CancellationToken.None);
        }

        public async Task<Dictionary<string, string>> GetExtraPropertiesAsync(string accountId)
        {
            if (string.IsNullOrEmpty(accountId))
                return new Dictionary<string, string>();

            var account = await accountsCollection
                .Find(Builders<AccountInfoMongoDB>.Filter.Eq(e => e.Id, accountId))
                .FirstOrDefaultAsync(CancellationToken.None);

            return account?.ExtraProperties != null
                ? new Dictionary<string, string>(account.ExtraProperties)
                : new Dictionary<string, string>();
        }

        public Task<DatabaseEntriesInfo<IAccountInfoData>> Search(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new DatabaseEntriesInfo<IAccountInfoData>());
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeAccountId(string accountId)
        {
            return (accountId ?? string.Empty).Trim();
        }

        private static string ValidateVerificationCodeData(string email, string code, int attempts)
        {
            string normalizedEmail = NormalizeEmail(email);

            if (string.IsNullOrEmpty(normalizedEmail))
                throw new ArgumentException("Email is required", nameof(email));

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Verification code is required", nameof(code));

            if (attempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(attempts), "Attempts must be greater than zero");

            return normalizedEmail;
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static async Task<VerificationCodeResult> CheckVerificationCodeAsync<TDocument>(
            IMongoCollection<TDocument> collection,
            Expression<Func<TDocument, string>> emailField,
            Expression<Func<TDocument, string>> codeField,
            Expression<Func<TDocument, DateTime>> expiresAtField,
            Expression<Func<TDocument, int>> attemptsLeftField,
            string email,
            string code,
            CancellationToken cancellationToken)
            where TDocument : class
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return VerificationCodeResult.Invalid;

            string normalizedEmail = NormalizeEmail(email);
            DateTime utcNow = DateTime.UtcNow;
            FilterDefinitionBuilder<TDocument> filters = Builders<TDocument>.Filter;
            FilterDefinition<TDocument> emailFilter = filters.Eq(emailField, normalizedEmail);
            FieldDefinition<TDocument> expiresAtDefinition =
                new ExpressionFieldDefinition<TDocument>(expiresAtField);
            FieldDefinition<TDocument> attemptsLeftDefinition =
                new ExpressionFieldDefinition<TDocument>(attemptsLeftField);
            FilterDefinition<TDocument> activeFilter = filters.And(
                emailFilter,
                filters.Gt(expiresAtField, utcNow));

            FilterDefinition<TDocument> successFilter = filters.And(
                activeFilter,
                filters.Eq(codeField, code),
                filters.Gt(attemptsLeftField, 0));

            cancellationToken.ThrowIfCancellationRequested();
            TDocument consumed = await collection.FindOneAndDeleteAsync(
                successFilter,
                cancellationToken: CancellationToken.None);

            if (consumed != null)
                return VerificationCodeResult.Success;

            FilterDefinition<TDocument> expiredFilter = filters.And(
                emailFilter,
                filters.Or(
                    filters.Lte(expiresAtField, utcNow),
                    filters.Exists(expiresAtDefinition, false)));

            TDocument expired = await collection.FindOneAndDeleteAsync(
                expiredFilter,
                cancellationToken: CancellationToken.None);

            if (expired != null)
                return VerificationCodeResult.Expired;

            FilterDefinition<TDocument> invalidAttemptFilter = filters.And(
                activeFilter,
                filters.Ne(codeField, code),
                filters.Gt(attemptsLeftField, 1));
            UpdateDefinition<TDocument> decrementAttempt = Builders<TDocument>.Update
                .Inc(attemptsLeftField, -1);

            UpdateResult invalidAttempt = await collection.UpdateOneAsync(
                invalidAttemptFilter,
                decrementAttempt,
                cancellationToken: CancellationToken.None);

            if (invalidAttempt.ModifiedCount > 0)
                return VerificationCodeResult.Invalid;

            FilterDefinition<TDocument> exhaustedFilter = filters.And(
                activeFilter,
                filters.Or(
                    filters.Lte(attemptsLeftField, 1),
                    filters.Exists(attemptsLeftDefinition, false)));

            TDocument exhausted = await collection.FindOneAndDeleteAsync(
                exhaustedFilter,
                cancellationToken: CancellationToken.None);

            return exhausted != null
                ? VerificationCodeResult.AttemptsExceeded
                : VerificationCodeResult.Invalid;
        }

        private static void PrepareAccount(IAccountInfoData account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            account.Username = account.Username?.Trim() ?? string.Empty;
            account.Email = NormalizeEmail(account.Email);
        }

        private async Task ThrowIfDuplicateAccountAsync(AccountInfoMongoDB account, CancellationToken cancellationToken)
        {
            if (account == null)
                throw new ArgumentException("Account must be an AccountInfoMongoDB instance", nameof(account));

            if (!string.IsNullOrEmpty(account.Username))
            {
                var usernameDuplicate = await accountsCollection
                    .Find(i => i.Username == account.Username && i.Id != account.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (usernameDuplicate != null)
                    throw new Exception($"User with username \"{account.Username}\" already exists");
            }

            if (!string.IsNullOrEmpty(account.Email))
            {
                var emailDuplicate = await accountsCollection
                    .Find(i => i.Email == account.Email && i.Id != account.Id)
                    .FirstOrDefaultAsync(cancellationToken);

                if (emailDuplicate != null)
                    throw new Exception($"User with email \"{account.Email}\" already exists");
            }
        }
    }
}
#endif
