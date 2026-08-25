using LiteDB;
using MasterServerToolkit.GameService;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Logger = MasterServerToolkit.Logging.Logger;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor, IDisposable
    {
        private readonly ILiteCollection<AccountInfoData> accountsCollection;
        private readonly ILiteCollection<AccountBlockData> accountBlocksCollection;
        private readonly ILiteCollection<ExtraPropertyData> extraPropertiesCollection;
        private readonly ILiteCollection<AccountServiceBindingData> serviceBindingsCollection;
        private readonly ILiteCollection<PasswordResetData> resetCodesCollection;
        private readonly ILiteCollection<EmailConfirmationData> emailConfirmationCodesCollection;
        private readonly ILiteCollection<AuthTokenRevisionData> authTokenRevisionsCollection;

        private readonly LiteDatabase database;

        /// <summary>
        /// Ensures that LiteDB operations are executed sequentially to avoid locking issues.
        /// </summary>
        private readonly SemaphoreSlim dbSemaphore = new(1, 1);

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public AccountsDatabaseAccessor(string databaseName)
        {
            database = new LiteDatabase($"{databaseName}.db");
            database.UtcDate = true;

            accountsCollection = database.GetCollection<AccountInfoData>("accounts");
            accountsCollection.EnsureIndex(a => a.Id, true);
            accountsCollection.EnsureIndex(a => a.Username, true);
            accountsCollection.EnsureIndex(x => x.Email);
            accountsCollection.EnsureIndex(x => x.CreatedAt);
            accountsCollection.EnsureIndex(x => x.UpdatedAt);
            accountsCollection.EnsureIndex(x => x.LastLoginAt);

            accountBlocksCollection = database.GetCollection<AccountBlockData>("account_blocks");
            accountBlocksCollection.EnsureIndex(a => a.Id, true);
            accountBlocksCollection.EnsureIndex(a => a.AccountId);
            accountBlocksCollection.EnsureIndex(a => a.BlockedUntil);
            accountBlocksCollection.EnsureIndex(a => a.RemovedAt);

            extraPropertiesCollection = database.GetCollection<ExtraPropertyData>("extra_properties");
            extraPropertiesCollection.EnsureIndex(a => a.AccountId);
            extraPropertiesCollection.EnsureIndex(a => a.PropertyKey);

            serviceBindingsCollection = database.GetCollection<AccountServiceBindingData>("account_service_bindings");
            serviceBindingsCollection.EnsureIndex(a => a.Id, true);
            serviceBindingsCollection.EnsureIndex(a => a.AccountId);
            serviceBindingsCollection.EnsureIndex(a => a.ServiceId);
            serviceBindingsCollection.EnsureIndex(a => a.PlayerId);

            resetCodesCollection = database.GetCollection<PasswordResetData>("reset_codes");
            resetCodesCollection.EnsureIndex(a => a.Email, true);

            emailConfirmationCodesCollection = database.GetCollection<EmailConfirmationData>("email_confirmation_codes");
            emailConfirmationCodesCollection.EnsureIndex(a => a.Email, true);

            authTokenRevisionsCollection = database.GetCollection<AuthTokenRevisionData>("auth_token_revisions");
            authTokenRevisionsCollection.EnsureIndex(a => a.AccountId, true);

            //CreateTestAccounts();
        }

        private void CreateTestAccounts()
        {
            int total = 10_000;

            for (int i = 0; i < total; i++)
            {
                Gender gender = (Gender)Mst.Helper.Random.Next(0, 2);
                string username = $"{SimpleNameGenerator.Generate(gender).Replace(" ", "_")}_{i}".ToLower();
                int days = Mst.Helper.Random.Next(-100, 0);
                var account = CreateAccountInstance();
                account.Username = username;
                account.Email = $"{username}@mst-demo.com";
                account.CreatedAt = DateTime.UtcNow.AddDays(days);
                _ = InsertAccountAsync(account);
            }
        }

        public IAccountInfoData CreateAccountInstance()
        {
            return new AccountInfoData();
        }

        public IAccountBlockData CreateAccountBlockInstance()
        {
            return new AccountBlockData();
        }

        public IAccountServiceBindingData CreateServiceBindingInstance()
        {
            return new AccountServiceBindingData();
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

        private static AccountBlockData ToAccountBlockData(IAccountBlockData block)
        {
            if (block is AccountBlockData blockData)
                return blockData;

            return new AccountBlockData
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

            // A binding represents one service slot owned by one MST account.
            // The external player id may change when that slot is rebound.
            binding.Id = $"{binding.AccountId}:{binding.ServiceId}";

            if (binding.CreatedAt == default)
                binding.CreatedAt = utcNow;

            binding.UpdatedAt = utcNow;
            binding.LastLoginAt = utcNow;
        }

        private static AccountServiceBindingData ToServiceBindingData(IAccountServiceBindingData binding)
        {
            if (binding is AccountServiceBindingData bindingData)
                return bindingData;

            return new AccountServiceBindingData
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
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string service = serviceId.ToString();
                string normalizedPlayerId = playerId?.Trim() ?? string.Empty;

                return serviceBindingsCollection.FindOne(binding =>
                    binding.ServiceId == service && binding.PlayerId == normalizedPlayerId);
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<IReadOnlyList<IAccountServiceBindingData>> GetServiceBindingsByAccountIdAsync(string accountId, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return serviceBindingsCollection
                    .Find(binding => binding.AccountId == accountId)
                    .Cast<IAccountServiceBindingData>()
                    .ToList();
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task UpsertServiceBindingAsync(IAccountServiceBindingData binding, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                PrepareServiceBinding(binding);
                ThrowIfServicePlayerAlreadyBound(binding);
                serviceBindingsCollection.Upsert(ToServiceBindingData(binding));
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task ReplaceServiceBindingAsync(IAccountServiceBindingData binding, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                PrepareServiceBinding(binding);
                ThrowIfServicePlayerAlreadyBound(binding);

                // The stable account/service id turns replacement into one atomic upsert.
                serviceBindingsCollection.Upsert(ToServiceBindingData(binding));
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        private void ThrowIfServicePlayerAlreadyBound(IAccountServiceBindingData binding)
        {
            AccountServiceBindingData conflict = serviceBindingsCollection.FindOne(existingBinding =>
                existingBinding.ServiceId == binding.ServiceId &&
                existingBinding.PlayerId == binding.PlayerId &&
                existingBinding.Id != binding.Id);

            if (conflict != null)
            {
                throw new InvalidOperationException(
                    $"Service player '{binding.ServiceId}:{binding.PlayerId}' is already bound to another account");
            }
        }

        public async Task DeleteServiceBindingAsync(string accountId, GameServiceId serviceId, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                serviceBindingsCollection.DeleteMany(
                    binding => binding.AccountId == accountId && binding.ServiceId == serviceId.ToString());
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<IAccountBlockData> GetActiveAccountBlockAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return null;

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                DateTime utcNow = DateTime.UtcNow;

                return accountBlocksCollection
                    .Find(block => block.AccountId == accountId && block.RemovedAt == null && block.BlockedUntil > utcNow)
                    .OrderByDescending(block => block.CreatedAt)
                    .FirstOrDefault();
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<DatabaseEntriesInfo<IAccountBlockData>> GetAccountBlockHistoryAsync(string accountId, int size, int page, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return new DatabaseEntriesInfo<IAccountBlockData>();

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                int skip = page * size;
                var blocks = accountBlocksCollection
                    .Find(block => block.AccountId == accountId)
                    .OrderByDescending(block => block.CreatedAt)
                    .ToList();

                int total = blocks.Count;

                return new DatabaseEntriesInfo<IAccountBlockData>
                {
                    entries = blocks.Skip(skip).Take(size).Cast<IAccountBlockData>().ToList(),
                    total = total,
                    filtered = total
                };
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<string> InsertAccountBlockAsync(IAccountBlockData block, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareAccountBlock(block);

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                DateTime utcNow = DateTime.UtcNow;
                var activeBlocks = accountBlocksCollection
                    .Find(item => item.AccountId == block.AccountId && item.RemovedAt == null && item.BlockedUntil > utcNow)
                    .ToList();

                foreach (var activeBlock in activeBlocks)
                {
                    activeBlock.RemovedAt = utcNow;
                    activeBlock.RemoveReason = "Replaced by a new account block";
                    accountBlocksCollection.Update(activeBlock);
                }

                accountBlocksCollection.Insert(ToAccountBlockData(block));
                return block.Id;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<bool> UnblockAccountAsync(string accountId, string removeReason, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return false;

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                DateTime utcNow = DateTime.UtcNow;
                var activeBlocks = accountBlocksCollection
                    .Find(item => item.AccountId == accountId && item.RemovedAt == null && item.BlockedUntil > utcNow)
                    .ToList();

                foreach (var activeBlock in activeBlocks)
                {
                    activeBlock.RemovedAt = utcNow;
                    activeBlock.RemoveReason = removeReason?.Trim() ?? string.Empty;
                    accountBlocksCollection.Update(activeBlock);
                }

                return activeBlocks.Count > 0;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<IAccountInfoData> GetAccountByIdAsync(string id, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var account = accountsCollection.FindOne(i => i.Id == id);

                if (account != null)
                {
                    account.ExtraProperties = GetExtraProperties(account.Id);
                }

                return account;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var account = accountsCollection.FindOne(i => i.Username == username);

                if (account != null)
                {
                    account.ExtraProperties = GetExtraProperties(account.Id);
                }

                return account;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var account = accountsCollection.FindOne(i => i.Token == token);

                if (account != null)
                {
                    account.ExtraProperties = GetExtraProperties(account.Id);
                }

                return account;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedEmail = NormalizeEmail(email);

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var account = accountsCollection.FindOne(i => i.Email == normalizedEmail);

                if (account != null)
                {
                    account.ExtraProperties = GetExtraProperties(account.Id);
                }

                return account;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var extraProperty = extraPropertiesCollection.FindOne(i => i.PropertyKey == propertyKey && i.PropertyValue == propertyValue);

                if (extraProperty == null)
                    return null;

                var account = accountsCollection.FindOne(i => i.Id == extraProperty.AccountId);

                if (account != null)
                {
                    account.ExtraProperties = GetExtraProperties(account.Id);
                }

                return account;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<DateTime> UpdateLastLoginAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime lastLoginAt = DateTime.UtcNow;

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var account = accountsCollection.FindOne(i => i.Id == accountId);

                if (account != null)
                {
                    account.LastLoginAt = lastLoginAt;
                    accountsCollection.Update(account);
                }

                return lastLoginAt;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        /// <summary>
        /// Reads extra properties for an account in a synchronous manner.
        /// Must be called under dbSemaphore lock.
        /// </summary>
        private Dictionary<string, string> GetExtraProperties(string accountId)
        {
            return extraPropertiesCollection
                .Find(i => i.AccountId == accountId)
                .ToDictionary(i => i.PropertyKey, i => i.PropertyValue);
        }

        public async Task<VerificationCodeResult> CheckEmailConfirmationCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return VerificationCodeResult.Invalid;

            var normalizedEmail = NormalizeEmail(email);

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ExecuteInTransaction(() =>
                {
                    EmailConfirmationData entry = emailConfirmationCodesCollection.FindById(normalizedEmail);

                    if (entry == null)
                        return VerificationCodeResult.Invalid;

                    if (entry.ExpiresAt <= DateTime.UtcNow)
                    {
                        emailConfirmationCodesCollection.Delete(normalizedEmail);
                        return VerificationCodeResult.Expired;
                    }

                    if (entry.AttemptsLeft <= 0)
                    {
                        emailConfirmationCodesCollection.Delete(normalizedEmail);
                        return VerificationCodeResult.AttemptsExceeded;
                    }

                    if (entry.Code == code)
                    {
                        emailConfirmationCodesCollection.Delete(normalizedEmail);
                        return VerificationCodeResult.Success;
                    }

                    entry.AttemptsLeft--;

                    if (entry.AttemptsLeft <= 0)
                    {
                        emailConfirmationCodesCollection.Delete(normalizedEmail);
                        return VerificationCodeResult.AttemptsExceeded;
                    }

                    emailConfirmationCodesCollection.Update(entry);
                    return VerificationCodeResult.Invalid;
                });
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<VerificationCodeResult> CheckPasswordResetCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return VerificationCodeResult.Invalid;

            var normalizedEmail = NormalizeEmail(email);

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ExecuteInTransaction(() =>
                {
                    PasswordResetData entry = resetCodesCollection.FindById(normalizedEmail);

                    if (entry == null)
                        return VerificationCodeResult.Invalid;

                    if (entry.ExpiresAt <= DateTime.UtcNow)
                    {
                        resetCodesCollection.Delete(normalizedEmail);
                        return VerificationCodeResult.Expired;
                    }

                    if (entry.AttemptsLeft <= 0)
                    {
                        resetCodesCollection.Delete(normalizedEmail);
                        return VerificationCodeResult.AttemptsExceeded;
                    }

                    if (entry.Code == code)
                    {
                        resetCodesCollection.Delete(normalizedEmail);
                        return VerificationCodeResult.Success;
                    }

                    entry.AttemptsLeft--;

                    if (entry.AttemptsLeft <= 0)
                    {
                        resetCodesCollection.Delete(normalizedEmail);
                        return VerificationCodeResult.AttemptsExceeded;
                    }

                    resetCodesCollection.Update(entry);
                    return VerificationCodeResult.Invalid;
                });
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task SavePasswordResetCodeAsync(
            string email,
            string code,
            DateTime expiresAt,
            int attempts,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVerificationCode(email, code, attempts);

            var normalizedEmail = NormalizeEmail(email);

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                resetCodesCollection.Upsert(new PasswordResetData
                {
                    Email = normalizedEmail,
                    Code = code,
                    ExpiresAt = ToUtc(expiresAt),
                    AttemptsLeft = attempts
                });
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task SaveEmailConfirmationCodeAsync(
            string email,
            string code,
            DateTime expiresAt,
            int attempts,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateVerificationCode(email, code, attempts);

            var normalizedEmail = NormalizeEmail(email);

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                emailConfirmationCodesCollection.Upsert(new EmailConfirmationData
                {
                    Code = code,
                    Email = normalizedEmail,
                    ExpiresAt = ToUtc(expiresAt),
                    AttemptsLeft = attempts
                });
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<int> GetAuthTokenRevisionAsync(
            string accountId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return 0;

            string normalizedAccountId = accountId.Trim();

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                AuthTokenRevisionData entry = authTokenRevisionsCollection.FindById(normalizedAccountId);
                return entry?.Revision ?? 0;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<int> IncrementAuthTokenRevisionAsync(
            string accountId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentException("Account id cannot be empty", nameof(accountId));

            string normalizedAccountId = accountId.Trim();

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ExecuteInTransaction(() =>
                {
                    AuthTokenRevisionData entry = authTokenRevisionsCollection.FindById(normalizedAccountId)
                        ?? new AuthTokenRevisionData
                        {
                            AccountId = normalizedAccountId,
                            Revision = 0
                        };

                    entry.Revision = checked(entry.Revision + 1);
                    authTokenRevisionsCollection.Upsert(entry);
                    return entry.Revision;
                });
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task<string> InsertAccountAsync(IAccountInfoData account, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var username = account.Username?.Trim() ?? string.Empty;
            var email = NormalizeEmail(account.Email);

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // Check username duplicate
                if (!string.IsNullOrEmpty(username))
                {
                    var existingAccount = accountsCollection.FindOne(i => i.Username == username);
                    if (existingAccount != null)
                        throw new Exception($"User with username \"{username}\" already exists");
                }

                // Check email duplicate
                if (!string.IsNullOrEmpty(email))
                {
                    var existingAccount = accountsCollection.FindOne(i => i.Email == email);
                    if (existingAccount != null)
                        throw new Exception($"User with email \"{email}\" already exists");
                }

                // Persist normalized email to stored model (if used)
                account.Email = email;

                string accountId = accountsCollection.Insert(account as AccountInfoData).AsString;
                InsertOrUpdateExtraProperties(accountId, account.ExtraProperties);

                return accountId;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        public async Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token, CancellationToken cancellationToken = default)
        {
            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                account.Token = token;
                accountsCollection.Update(account as AccountInfoData);
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        /// <summary>
        /// Inserts or updates extra properties for an account.
        /// Must be called under dbSemaphore lock.
        /// </summary>
        private void InsertOrUpdateExtraProperties(string accountId, Dictionary<string, string> properties)
        {
            if (properties == null)
                return;

            foreach (KeyValuePair<string, string> pair in properties)
            {
                extraPropertiesCollection.Upsert(new ExtraPropertyData
                {
                    Id = $"{accountId}_{pair.Key}",
                    AccountId = accountId,
                    PropertyKey = pair.Key,
                    PropertyValue = pair.Value
                });
            }
        }

        public async Task UpdateAccountAsync(IAccountInfoData account, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var username = account.Username?.Trim() ?? string.Empty;
            var email = NormalizeEmail(account.Email);

            await dbSemaphore.WaitAsync(cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!string.IsNullOrEmpty(username))
                {
                    var existingAccount = accountsCollection.FindOne(i => i.Username == username && i.Id != account.Id);
                    if (existingAccount != null)
                        throw new Exception($"User with username \"{username}\" already exists");
                }

                if (!string.IsNullOrEmpty(email))
                {
                    var existingAccount = accountsCollection.FindOne(i => i.Email == email && i.Id != account.Id);
                    if (existingAccount != null)
                        throw new Exception($"User with email \"{email}\" already exists");
                }

                account.Username = username;
                account.Email = email;
                accountsCollection.Update(account as AccountInfoData);
                InsertOrUpdateExtraProperties(account.Id, account.ExtraProperties);
            }
            finally
            {
                dbSemaphore.Release();
            }
        }

        /// <summary>
        /// Normalizes email for consistent indexing and lookups.
        /// </summary>
        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static void ValidateVerificationCode(string email, string code, int attempts)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email cannot be empty", nameof(email));

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Verification code cannot be empty", nameof(code));

            if (attempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "Attempts must be greater than zero");
        }

        private static DateTime ToUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;

            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();

            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private T ExecuteInTransaction<T>(Func<T> action)
        {
            if (!database.BeginTrans())
                throw new InvalidOperationException("Failed to begin LiteDB transaction");

            try
            {
                T result = action();

                if (!database.Commit())
                    throw new InvalidOperationException("Failed to commit LiteDB transaction");

                return result;
            }
            catch
            {
                database.Rollback();
                throw;
            }
        }

        public void Dispose()
        {
            CustomProperties?.Clear();
            database?.Dispose();
            dbSemaphore?.Dispose();
        }

        public async Task<DatabaseEntriesInfo<IAccountInfoData>> Search(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Page/size normalization
            int size = int.Parse((string)filter[MstParamKeys.QS_PARAM_SIZE]);
            int page = int.Parse((string)filter[MstParamKeys.QS_PARAM_PAGE]);
            int skip = page * size;

            // Safe read of filters
            string searchValue = filter.TryGetValue(MstParamKeys.QS_PARAM_VALUE, out var sv) ? sv as string ?? "" : "";
            string sortDir = filter.TryGetValue(MstParamKeys.QS_PARAM_SORT_DIR, out var sd) ? sd as string ?? "" : "";
            bool isDesc = sortDir == ((int)MstSortDirection.Desc).ToString() ||
                          (Enum.TryParse(sortDir, true, out MstSortDirection sortDirection) && sortDirection == MstSortDirection.Desc);
            string sortField = filter.TryGetValue(MstParamKeys.QS_PARAM_SORT_FIELD, out var sf) ? sf as string ?? "" : "";
            string blockedValue = filter.TryGetValue(MstParamKeys.QS_PARAM_IS_BLOCKED, out var ibv)
                ? ibv as string ?? ""
                : "";

            DateTime? lastLoginAt = null;
            DateTime? createAt = null;
            DateTime? updateAt = null;

            //if (filter.TryGetValue(MstParamKeys.QS_PARAM_SEARCH_DATE_FROM, out var llat))
            //{
            //    if (llat is string s && DateTime.TryParse(s, out var dt))
            //        lastLoginAt = dt;
            //}

            //if (filter.TryGetValue(MstParamKeys.QS_PARAM_SEARCH_CREATE_AT, out var crat))
            //{
            //    if (crat is string s && DateTime.TryParse(s, out var dt))
            //        createAt = dt;
            //}

            //if (filter.TryGetValue(MstParamKeys.QS_PARAM_SEARCH_DATE_VALUE, out var upat))
            //{
            //    if (upat is string s && DateTime.TryParse(s, out var dt))
            //        updateAt = dt;
            //}

            bool datesInUse = lastLoginAt != null || createAt != null || updateAt != null;

            await dbSemaphore.WaitAsync(cancellationToken);

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                HashSet<string> activeBlockedAccountIds = null;

                HashSet<string> GetActiveBlockedAccountIds()
                {
                    if (activeBlockedAccountIds != null)
                        return activeBlockedAccountIds;

                    DateTime utcNow = DateTime.UtcNow;
                    activeBlockedAccountIds = accountBlocksCollection
                        .Find(block => block.RemovedAt == null && block.BlockedUntil > utcNow)
                        .Select(block => block.AccountId)
                        .ToHashSet();

                    return activeBlockedAccountIds;
                }

                // 1) Total without any filter
                int totalAll = accountsCollection.Count();

                // 2) Apply filters (preferably in DB; here shown in-memory for clarity)
                // Materialize once to count filtered rows
                var filteredList = new List<AccountInfoData>();

                if (lastLoginAt != null)
                {
                    var filteredByLastLogin = accountsCollection
                        .Find(ac =>
                            ac.LastLoginAt.Date == ((DateTime)lastLoginAt).Date).ToList();

                    filteredList.AddRange(filteredByLastLogin);
                }

                if (createAt != null)
                {
                    var filteredByCreateAt = accountsCollection
                        .Find(ac =>
                            ac.CreatedAt.Date == ((DateTime)createAt).Date).ToList();

                    filteredList.AddRange(filteredByCreateAt);
                }

                if (updateAt != null)
                {
                    var filteredByUpdateAt = accountsCollection
                        .Find(ac =>
                            ac.CreatedAt.Date == ((DateTime)updateAt).Date).ToList();

                    filteredList.AddRange(filteredByUpdateAt);
                }

                // Search in filtered by dates
                if (datesInUse)
                {
                    var filteredBySearchValue = filteredList
                                            .Where(ac =>
                                                ac.Id.Contains(searchValue) ||
                                                ac.Username.Contains(searchValue) ||
                                                ac.Email.Contains(searchValue)).ToList();

                    filteredList.Clear();
                    filteredList.AddRange(filteredBySearchValue);
                }
                // Search directly in db
                else
                {
                    var filteredBySearchValue = accountsCollection
                                            .Find(ac =>
                                                ac.Id.Contains(searchValue) ||
                                                ac.Username.Contains(searchValue) ||
                                                ac.Email.Contains(searchValue)).ToList();

                    filteredList.AddRange(filteredBySearchValue);
                }


                filteredList = filteredList.Distinct().ToList();

                if (!string.IsNullOrWhiteSpace(blockedValue) && bool.TryParse(blockedValue, out bool isBlocked))
                {
                    var blockedAccountIds = GetActiveBlockedAccountIds();

                    filteredList = filteredList
                        .Where(account => isBlocked == blockedAccountIds.Contains(account.Id))
                        .ToList();
                }

                int filteredCount = filteredList.Count;

                // 3) Sorting (whitelist known fields)
                IEnumerable<IAccountInfoData> sorted = sortField?.ToLowerInvariant() switch
                {
                    "id" => isDesc ?
                                    filteredList.OrderByDescending(x => x.Id) : filteredList.OrderBy(x => x.Id),

                    "username" => isDesc ?
                                    filteredList.OrderByDescending(x => x.Username) : filteredList.OrderBy(x => x.Username),

                    "email" => isDesc ?
                                    filteredList.OrderByDescending(x => x.Email) : filteredList.OrderBy(x => x.Email),

                    "last_login_at" => isDesc ?
                                    filteredList.OrderByDescending(x => x.LastLoginAt) : filteredList.OrderBy(x => x.LastLoginAt),

                    "created_at" => isDesc ?
                                    filteredList.OrderByDescending(x => x.CreatedAt) : filteredList.OrderBy(x => x.CreatedAt),

                    "updated_at" => isDesc ?
                                    filteredList.OrderByDescending(x => x.UpdatedAt) : filteredList.OrderBy(x => x.UpdatedAt),

                    "isblocked" => isDesc ?
                                    filteredList.OrderByDescending(x => GetActiveBlockedAccountIds().Contains(x.Id)) :
                                    filteredList.OrderBy(x => GetActiveBlockedAccountIds().Contains(x.Id)),

                    _ => filteredList.OrderBy(x => x.Id)
                };

                // 4) Pagination (uses size only here)
                var pageItems = sorted.Skip(skip).Take(size).ToList();

                // 5) Return correctly mapped counts
                var result = new DatabaseEntriesInfo<IAccountInfoData>
                {
                    entries = pageItems, // current page only
                    total = totalAll, // all rows without filter
                    filtered = filteredCount // rows after filter, before pagination
                };

                return result;
            }
            finally
            {
                dbSemaphore.Release();
            }
        }
    }
}
