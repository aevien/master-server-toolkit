using MasterServerToolkit.GameService;
using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor, IDisposable
    {
        private readonly struct VerificationCodeSnapshot
        {
            public VerificationCodeSnapshot(string code, DateTime? expiresAt, int? attemptsLeft)
            {
                Code = code;
                ExpiresAt = expiresAt;
                AttemptsLeft = attemptsLeft;
            }

            public string Code { get; }
            public DateTime? ExpiresAt { get; }
            public int? AttemptsLeft { get; }
        }

        private readonly ConnectionConfig configuration;

        public AccountsDatabaseAccessor(ConnectionConfig configuration)
        {
            this.configuration = configuration;

            using SqlSugarClient db = new(configuration);

            var tableTypes = new[]
            {
                typeof(AccountInfoData),
                typeof(AccountBlockData),
                typeof(EmailConfirmationData),
                typeof(ExtraPropertyData),
                typeof(AccountServiceBindingData),
                typeof(PasswordResetData),
                typeof(ProfilePropertyData),
                typeof(AuthTokenRevisionData)
            };

            foreach (var tableType in tableTypes)
            {
                var tableName = db.EntityMaintenance.GetTableName(tableType);

                if (!db.DbMaintenance.IsAnyTable(tableName, false))
                {
                    db.CodeFirst.InitTables(tableType);
                }

                if (!db.DbMaintenance.IsAnyTable(tableName, false))
                    throw new InvalidOperationException($"Required database table '{tableName}' was not created");
            }

            EnsureVerificationCodeSchema(db, typeof(EmailConfirmationData));
            EnsureVerificationCodeSchema(db, typeof(PasswordResetData));
            ExpireLegacyVerificationCodes(db);
        }

        public MstProperties CustomProperties { get; private set; }
        public Logging.Logger Logger { get; set; }

        public async Task<VerificationCodeResult> CheckEmailConfirmationCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return VerificationCodeResult.Invalid;

            string normalizedEmail = NormalizeEmail(email);
            using SqlSugarClient db = new(configuration);
            return await CheckVerificationCodeAsync(
                db,
                normalizedEmail,
                code,
                LoadEmailConfirmationCodeAsync,
                DeleteEmailConfirmationCodeAsync,
                UpdateEmailConfirmationAttemptsAsync,
                cancellationToken);
        }

        public async Task<VerificationCodeResult> CheckPasswordResetCodeAsync(
            string email,
            string code,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
                return VerificationCodeResult.Invalid;

            string normalizedEmail = NormalizeEmail(email);
            using SqlSugarClient db = new(configuration);
            return await CheckVerificationCodeAsync(
                db,
                normalizedEmail,
                code,
                LoadPasswordResetCodeAsync,
                DeletePasswordResetCodeAsync,
                UpdatePasswordResetAttemptsAsync,
                cancellationToken);
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

        public void Dispose() { }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static DateTime AsUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;

            return value.Kind == DateTimeKind.Local
                ? value.ToUniversalTime()
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        private static DateTime NormalizeExpirationUtc(DateTime value)
        {
            DateTime utc = AsUtc(value);
            return new DateTime(
                utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond,
                DateTimeKind.Utc);
        }

        private static async Task<VerificationCodeResult> CheckVerificationCodeAsync(
            SqlSugarClient db,
            string email,
            string code,
            Func<SqlSugarClient, string, CancellationToken, Task<VerificationCodeSnapshot?>> load,
            Func<SqlSugarClient, string, VerificationCodeSnapshot, CancellationToken, Task<int>> delete,
            Func<SqlSugarClient, string, VerificationCodeSnapshot, int, CancellationToken, Task<int>> updateAttempts,
            CancellationToken cancellationToken)
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VerificationCodeSnapshot? stored = await load(db, email, cancellationToken);

                if (!stored.HasValue)
                    return VerificationCodeResult.Invalid;

                VerificationCodeSnapshot snapshot = stored.Value;
                DateTime expiresAt = AsUtc(snapshot.ExpiresAt.GetValueOrDefault());
                int attemptsLeft = snapshot.AttemptsLeft.GetValueOrDefault();

                if (!snapshot.ExpiresAt.HasValue || expiresAt <= DateTime.UtcNow)
                {
                    if (await delete(db, email, snapshot, cancellationToken) > 0)
                        return VerificationCodeResult.Expired;

                    continue;
                }

                if (attemptsLeft <= 0)
                {
                    if (await delete(db, email, snapshot, cancellationToken) > 0)
                        return VerificationCodeResult.AttemptsExceeded;

                    continue;
                }

                if (string.Equals(snapshot.Code, code, StringComparison.Ordinal))
                {
                    if (await delete(db, email, snapshot, cancellationToken) > 0)
                        return VerificationCodeResult.Success;

                    continue;
                }

                if (attemptsLeft == 1)
                {
                    if (await delete(db, email, snapshot, cancellationToken) > 0)
                        return VerificationCodeResult.AttemptsExceeded;

                    continue;
                }

                if (await updateAttempts(db, email, snapshot, attemptsLeft - 1, cancellationToken) > 0)
                    return VerificationCodeResult.Invalid;
            }
        }

        private static async Task<VerificationCodeSnapshot?> LoadEmailConfirmationCodeAsync(
            SqlSugarClient db,
            string email,
            CancellationToken cancellationToken)
        {
            EmailConfirmationData stored = await db.Queryable<EmailConfirmationData>()
                .FirstAsync(item => item.Email == email, cancellationToken);

            return stored == null
                ? (VerificationCodeSnapshot?)null
                : new VerificationCodeSnapshot(stored.Code, stored.ExpiresAt, stored.AttemptsLeft);
        }

        private static Task<int> DeleteEmailConfirmationCodeAsync(
            SqlSugarClient db,
            string email,
            VerificationCodeSnapshot stored,
            CancellationToken cancellationToken)
        {
            return db.Deleteable<EmailConfirmationData>()
                .Where(item => item.Email == email &&
                    item.Code == stored.Code &&
                    item.ExpiresAt == stored.ExpiresAt &&
                    item.AttemptsLeft == stored.AttemptsLeft)
                .ExecuteCommandAsync(cancellationToken);
        }

        private static Task<int> UpdateEmailConfirmationAttemptsAsync(
            SqlSugarClient db,
            string email,
            VerificationCodeSnapshot stored,
            int attemptsLeft,
            CancellationToken cancellationToken)
        {
            return db.Updateable<EmailConfirmationData>()
                .SetColumns(item => item.AttemptsLeft == attemptsLeft)
                .Where(item => item.Email == email &&
                    item.Code == stored.Code &&
                    item.ExpiresAt == stored.ExpiresAt &&
                    item.AttemptsLeft == stored.AttemptsLeft)
                .ExecuteCommandAsync(cancellationToken);
        }

        private static async Task<VerificationCodeSnapshot?> LoadPasswordResetCodeAsync(
            SqlSugarClient db,
            string email,
            CancellationToken cancellationToken)
        {
            PasswordResetData stored = await db.Queryable<PasswordResetData>()
                .FirstAsync(item => item.Email == email, cancellationToken);

            return stored == null
                ? (VerificationCodeSnapshot?)null
                : new VerificationCodeSnapshot(stored.Code, stored.ExpiresAt, stored.AttemptsLeft);
        }

        private static Task<int> DeletePasswordResetCodeAsync(
            SqlSugarClient db,
            string email,
            VerificationCodeSnapshot stored,
            CancellationToken cancellationToken)
        {
            return db.Deleteable<PasswordResetData>()
                .Where(item => item.Email == email &&
                    item.Code == stored.Code &&
                    item.ExpiresAt == stored.ExpiresAt &&
                    item.AttemptsLeft == stored.AttemptsLeft)
                .ExecuteCommandAsync(cancellationToken);
        }

        private static Task<int> UpdatePasswordResetAttemptsAsync(
            SqlSugarClient db,
            string email,
            VerificationCodeSnapshot stored,
            int attemptsLeft,
            CancellationToken cancellationToken)
        {
            return db.Updateable<PasswordResetData>()
                .SetColumns(item => item.AttemptsLeft == attemptsLeft)
                .Where(item => item.Email == email &&
                    item.Code == stored.Code &&
                    item.ExpiresAt == stored.ExpiresAt &&
                    item.AttemptsLeft == stored.AttemptsLeft)
                .ExecuteCommandAsync(cancellationToken);
        }

        private static void EnsureVerificationCodeSchema(SqlSugarClient db, Type modelType)
        {
            string tableName = db.EntityMaintenance.GetTableName(modelType);
            EnsureColumn(db, modelType, tableName, nameof(EmailConfirmationData.ExpiresAt));
            EnsureColumn(db, modelType, tableName, nameof(EmailConfirmationData.AttemptsLeft));
        }

        private static void EnsureColumn(SqlSugarClient db, Type modelType, string tableName, string propertyName)
        {
            var entityInfo = db.EntityMaintenance.GetEntityInfo(modelType);
            var entityColumn = entityInfo.Columns.Single(column => column.PropertyName == propertyName);

            if (db.DbMaintenance.IsAnyColumn(tableName, entityColumn.DbColumnName, false))
                return;

            Type propertyType = Nullable.GetUnderlyingType(entityColumn.PropertyInfo.PropertyType)
                ?? entityColumn.PropertyInfo.PropertyType;
            string dataType = string.IsNullOrEmpty(entityColumn.DataType)
                ? db.Ado.DbBind.GetDbTypeName(propertyType.Name)
                : entityColumn.DataType;

            var column = new DbColumnInfo
            {
                TableName = tableName,
                DbColumnName = entityColumn.DbColumnName,
                PropertyName = entityColumn.PropertyName,
                PropertyType = propertyType,
                DataType = dataType,
                Length = entityColumn.Length,
                IsNullable = true
            };

            db.DbMaintenance.AddColumn(tableName, column);

            if (!db.DbMaintenance.IsAnyColumn(tableName, entityColumn.DbColumnName, false))
            {
                throw new InvalidOperationException(
                    $"Required database column '{tableName}.{entityColumn.DbColumnName}' was not created");
            }
        }

        private static void ExpireLegacyVerificationCodes(SqlSugarClient db)
        {
            DateTime expiredAt = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            db.Updateable<EmailConfirmationData>()
                .SetColumns(item => item.ExpiresAt == expiredAt)
                .Where(item => item.ExpiresAt == null)
                .ExecuteCommand();
            db.Updateable<EmailConfirmationData>()
                .SetColumns(item => item.AttemptsLeft == 0)
                .Where(item => item.AttemptsLeft == null)
                .ExecuteCommand();

            db.Updateable<PasswordResetData>()
                .SetColumns(item => item.ExpiresAt == expiredAt)
                .Where(item => item.ExpiresAt == null)
                .ExecuteCommand();
            db.Updateable<PasswordResetData>()
                .SetColumns(item => item.AttemptsLeft == 0)
                .Where(item => item.AttemptsLeft == null)
                .ExecuteCommand();
        }

        private static void PrepareAccount(IAccountInfoData account)
        {
            if (account == null)
                throw new ArgumentNullException(nameof(account));

            account.Username = account.Username?.Trim() ?? string.Empty;
            account.Email = NormalizeEmail(account.Email);
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

        private static async Task ThrowIfDuplicateAccountAsync(SqlSugarClient db, AccountInfoData account, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (account == null)
                throw new ArgumentNullException(nameof(account));

            if (!string.IsNullOrEmpty(account.Username))
            {
                bool usernameExists = await db.Queryable<AccountInfoData>()
                    .Where(a => a.Username == account.Username && a.Id != account.Id)
                    .AnyAsync();
                cancellationToken.ThrowIfCancellationRequested();

                if (usernameExists)
                    throw new Exception($"User with username \"{account.Username}\" already exists");
            }

            if (!string.IsNullOrEmpty(account.Email))
            {
                bool emailExists = await db.Queryable<AccountInfoData>()
                    .Where(a => a.Email == account.Email && a.Id != account.Id)
                    .AnyAsync();
                cancellationToken.ThrowIfCancellationRequested();

                if (emailExists)
                    throw new Exception($"User with email \"{account.Email}\" already exists");
            }
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

        public async Task<IAccountServiceBindingData> GetServiceBindingAsync(GameServiceId serviceId, string playerId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqlSugarClient db = new(configuration);

            string service = serviceId.ToString();
            string normalizedPlayerId = playerId?.Trim() ?? string.Empty;

            var binding = await db.Queryable<AccountServiceBindingData>()
                .SingleAsync(binding =>
                    binding.ServiceId == service && binding.PlayerId == normalizedPlayerId);
            cancellationToken.ThrowIfCancellationRequested();
            return binding;
        }

        public async Task<IReadOnlyList<IAccountServiceBindingData>> GetServiceBindingsByAccountIdAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqlSugarClient db = new(configuration);

            var bindings = await db.Queryable<AccountServiceBindingData>()
                .Where(binding => binding.AccountId == accountId)
                .ToListAsync();
            cancellationToken.ThrowIfCancellationRequested();

            return bindings.Cast<IAccountServiceBindingData>().ToList();
        }

        public async Task UpsertServiceBindingAsync(IAccountServiceBindingData binding, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareServiceBinding(binding);

            using SqlSugarClient db = new(configuration);
            await db.Storageable(new AccountServiceBindingData(binding))
                .WhereColumns(new[] { "id" })
                .ExecuteCommandAsync();
        }

        public async Task ReplaceServiceBindingAsync(IAccountServiceBindingData binding, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareServiceBinding(binding);

            using SqlSugarClient db = new(configuration);

            // The stable account/service primary key makes replacement a single write.
            await db.Storageable(new AccountServiceBindingData(binding))
                .WhereColumns(new[] { "id" })
                .ExecuteCommandAsync();
        }

        public async Task DeleteServiceBindingAsync(string accountId, GameServiceId serviceId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqlSugarClient db = new(configuration);

            await db.Deleteable<AccountServiceBindingData>()
                .Where(binding => binding.AccountId == accountId && binding.ServiceId == serviceId.ToString())
                .ExecuteCommandAsync();
        }

        public async Task<IAccountBlockData> GetActiveAccountBlockAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return null;

            using SqlSugarClient db = new(configuration);
            DateTime utcNow = DateTime.UtcNow;

            var block = await db.Queryable<AccountBlockData>()
                .Where(block => block.AccountId == accountId && block.RemovedAt == null && block.BlockedUntil > utcNow)
                .OrderBy(block => block.CreatedAt, OrderByType.Desc)
                .FirstAsync();
            cancellationToken.ThrowIfCancellationRequested();
            return block;
        }

        public async Task<DatabaseEntriesInfo<IAccountBlockData>> GetAccountBlockHistoryAsync(string accountId, int size, int page, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return new DatabaseEntriesInfo<IAccountBlockData>();

            using SqlSugarClient db = new(configuration);
            int skip = page * size;

            var query = db.Queryable<AccountBlockData>()
                .Where(block => block.AccountId == accountId);

            int total = await query.CountAsync();
            cancellationToken.ThrowIfCancellationRequested();

            List<AccountBlockData> pageItems = await query
                .OrderBy(block => block.CreatedAt, OrderByType.Desc)
                .Skip(skip)
                .Take(size)
                .ToListAsync();
            cancellationToken.ThrowIfCancellationRequested();

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
            using SqlSugarClient db = new(configuration);
            DateTime utcNow = DateTime.UtcNow;

            var tran = await db.Ado.UseTranAsync(async () =>
            {
                await db.Updateable<AccountBlockData>()
                    .SetColumns(item => item.RemovedAt == utcNow)
                    .SetColumns(item => item.RemoveReason == "Replaced by a new account block")
                    .Where(item => item.AccountId == block.AccountId && item.RemovedAt == null && item.BlockedUntil > utcNow)
                    .ExecuteCommandAsync();
                cancellationToken.ThrowIfCancellationRequested();

                await db.Insertable(new AccountBlockData(block)).ExecuteCommandAsync();
                cancellationToken.ThrowIfCancellationRequested();
            });

            if (!tran.IsSuccess)
                throw tran.ErrorException;

            return block.Id;
        }

        public async Task<bool> UnblockAccountAsync(string accountId, string removeReason, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(accountId))
                return false;

            using SqlSugarClient db = new(configuration);
            DateTime utcNow = DateTime.UtcNow;
            string normalizedRemoveReason = removeReason?.Trim() ?? string.Empty;

            int affectedRows = await db.Updateable<AccountBlockData>()
                .SetColumns(item => item.RemovedAt == utcNow)
                .SetColumns(item => item.RemoveReason == normalizedRemoveReason)
                .Where(item => item.AccountId == accountId && item.RemovedAt == null && item.BlockedUntil > utcNow)
                .ExecuteCommandAsync();

            return affectedRows > 0;
        }

        public async Task<DateTime> UpdateLastLoginAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DateTime lastLoginAt = DateTime.UtcNow;
            using SqlSugarClient db = new(configuration);

            await db.Updateable<AccountInfoData>()
                .SetColumns(a => a.LastLoginAt == lastLoginAt)
                .Where(a => a.Id == accountId)
                .ExecuteCommandAsync();

            return lastLoginAt;
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedEmail = NormalizeEmail(email);
            using SqlSugarClient db = new(configuration);

            var account = await db.Queryable<AccountInfoData>()
                .SingleAsync(a => a.Email == normalizedEmail);
            cancellationToken.ThrowIfCancellationRequested();

            if (account != null)
            {
                account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id, cancellationToken);
            }

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqlSugarClient db = new(configuration);

            var property = await db.Queryable<ExtraPropertyData>()
                .FirstAsync(ep => ep.PropertyKey == propertyKey && ep.PropertyValue == propertyValue);
            cancellationToken.ThrowIfCancellationRequested();

            if (property == null)
                return null;

            var account = await db.Queryable<AccountInfoData>()
                .SingleAsync(a => a.Id == property.AccountId);
            cancellationToken.ThrowIfCancellationRequested();

            if (account != null)
            {
                account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id, cancellationToken);
            }

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByIdAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqlSugarClient db = new(configuration);

            var account = await db.Queryable<AccountInfoData>()
                .SingleAsync(a => a.Id == accountId);
            cancellationToken.ThrowIfCancellationRequested();

            if (account != null)
            {
                account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id, cancellationToken);
            }

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqlSugarClient db = new(configuration);

            var account = await db.Queryable<AccountInfoData>()
                .SingleAsync(a => a.Token == token);
            cancellationToken.ThrowIfCancellationRequested();

            if (account != null)
            {
                account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id, cancellationToken);
            }

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqlSugarClient db = new(configuration);

            var account = await db.Queryable<AccountInfoData>()
                .SingleAsync(a => a.Username == username);
            cancellationToken.ThrowIfCancellationRequested();

            if (account != null)
            {
                account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id, cancellationToken);
            }

            return account;
        }

        public async Task<Dictionary<string, string>> GetExtraPropertiesAsync(SqlSugarClient db, string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var list = await db.Queryable<ExtraPropertyData>()
                               .Where(p => p.AccountId == accountId)
                               .ToListAsync();
            cancellationToken.ThrowIfCancellationRequested();

            var dict = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var p in list)
                dict[p.PropertyKey] = p.PropertyValue;

            return dict;
        }

        public async Task<string> InsertAccountAsync(IAccountInfoData account, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareAccount(account);
            using SqlSugarClient db = new(configuration);

            var tran = await db.Ado.UseTranAsync(async () =>
            {
                var accountData = new AccountInfoData(account);

                await ThrowIfDuplicateAccountAsync(db, accountData, cancellationToken);
                await db.Insertable(accountData).ExecuteCommandAsync();
                cancellationToken.ThrowIfCancellationRequested();

                if (account.ExtraProperties?.Any() == true)
                {
                    var extras = account.ExtraProperties.Select(kv => new ExtraPropertyData
                    {
                        AccountId = account.Id,
                        PropertyKey = kv.Key,
                        PropertyValue = kv.Value
                    }).ToList();

                    await db.Insertable(extras).ExecuteCommandAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            });

            if (!tran.IsSuccess)
                throw tran.ErrorException;

            return account.Id;
        }

        public async Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using SqlSugarClient db = new(configuration);

            await db.Updateable<AccountInfoData>()
                .SetColumns(a => a.Token == token)
                .Where(a => a.Id == account.Id)
                .ExecuteCommandAsync();
        }

        public async Task SaveEmailConfirmationCodeAsync(
            string email,
            string code,
            DateTime expiresAt,
            int attempts,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedEmail = NormalizeEmail(email);
            ValidateVerificationCode(normalizedEmail, code, attempts);

            using SqlSugarClient db = new(configuration);
            var data = new EmailConfirmationData
            {
                Email = normalizedEmail,
                Code = code,
                ExpiresAt = NormalizeExpirationUtc(expiresAt),
                AttemptsLeft = attempts
            };

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EmailConfirmationData stored = await db.Queryable<EmailConfirmationData>()
                    .FirstAsync(item => item.Email == normalizedEmail, cancellationToken);

                if (stored == null)
                {
                    try
                    {
                        await db.Insertable(data).ExecuteCommandAsync(cancellationToken);
                        return;
                    }
                    catch
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!await db.Queryable<EmailConfirmationData>()
                            .AnyAsync(item => item.Email == normalizedEmail, cancellationToken))
                        {
                            throw;
                        }

                        continue;
                    }
                }

                if (Matches(stored.Code, stored.ExpiresAt, stored.AttemptsLeft, data.Code, data.ExpiresAt, data.AttemptsLeft))
                    return;

                int updated = await db.Updateable<EmailConfirmationData>()
                    .SetColumns(item => item.Code == data.Code)
                    .SetColumns(item => item.ExpiresAt == data.ExpiresAt)
                    .SetColumns(item => item.AttemptsLeft == data.AttemptsLeft)
                    .Where(item => item.Email == normalizedEmail &&
                        item.Code == stored.Code &&
                        item.ExpiresAt == stored.ExpiresAt &&
                        item.AttemptsLeft == stored.AttemptsLeft)
                    .ExecuteCommandAsync(cancellationToken);

                if (updated > 0)
                    return;
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
            string normalizedEmail = NormalizeEmail(email);
            ValidateVerificationCode(normalizedEmail, code, attempts);

            using SqlSugarClient db = new(configuration);
            var data = new PasswordResetData
            {
                Email = normalizedEmail,
                Code = code,
                ExpiresAt = NormalizeExpirationUtc(expiresAt),
                AttemptsLeft = attempts
            };

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                PasswordResetData stored = await db.Queryable<PasswordResetData>()
                    .FirstAsync(item => item.Email == normalizedEmail, cancellationToken);

                if (stored == null)
                {
                    try
                    {
                        await db.Insertable(data).ExecuteCommandAsync(cancellationToken);
                        return;
                    }
                    catch
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!await db.Queryable<PasswordResetData>()
                            .AnyAsync(item => item.Email == normalizedEmail, cancellationToken))
                        {
                            throw;
                        }

                        continue;
                    }
                }

                if (Matches(stored.Code, stored.ExpiresAt, stored.AttemptsLeft, data.Code, data.ExpiresAt, data.AttemptsLeft))
                    return;

                int updated = await db.Updateable<PasswordResetData>()
                    .SetColumns(item => item.Code == data.Code)
                    .SetColumns(item => item.ExpiresAt == data.ExpiresAt)
                    .SetColumns(item => item.AttemptsLeft == data.AttemptsLeft)
                    .Where(item => item.Email == normalizedEmail &&
                        item.Code == stored.Code &&
                        item.ExpiresAt == stored.ExpiresAt &&
                        item.AttemptsLeft == stored.AttemptsLeft)
                    .ExecuteCommandAsync(cancellationToken);

                if (updated > 0)
                    return;
            }
        }

        public async Task<int> GetAuthTokenRevisionAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedAccountId = accountId?.Trim() ?? string.Empty;

            if (normalizedAccountId.Length == 0)
                return 0;

            using SqlSugarClient db = new(configuration);
            AuthTokenRevisionData revision = await db.Queryable<AuthTokenRevisionData>()
                .FirstAsync(item => item.AccountId == normalizedAccountId, cancellationToken);

            return revision?.Revision ?? 0;
        }

        public async Task<int> IncrementAuthTokenRevisionAsync(string accountId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string normalizedAccountId = accountId?.Trim() ?? string.Empty;

            if (normalizedAccountId.Length == 0)
                throw new ArgumentException("Account identifier is required", nameof(accountId));

            using SqlSugarClient db = new(configuration);

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AuthTokenRevisionData stored = await db.Queryable<AuthTokenRevisionData>()
                    .FirstAsync(item => item.AccountId == normalizedAccountId, cancellationToken);

                if (stored == null)
                {
                    try
                    {
                        await db.Insertable(new AuthTokenRevisionData
                        {
                            AccountId = normalizedAccountId,
                            Revision = 1
                        }).ExecuteCommandAsync(cancellationToken);
                        return 1;
                    }
                    catch
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!await db.Queryable<AuthTokenRevisionData>()
                            .AnyAsync(item => item.AccountId == normalizedAccountId, cancellationToken))
                        {
                            throw;
                        }

                        continue;
                    }
                }

                int nextRevision = checked(stored.Revision + 1);
                int updated = await db.Updateable<AuthTokenRevisionData>()
                    .SetColumns(item => item.Revision == nextRevision)
                    .Where(item => item.AccountId == normalizedAccountId && item.Revision == stored.Revision)
                    .ExecuteCommandAsync(cancellationToken);

                if (updated > 0)
                    return nextRevision;
            }
        }

        private static void ValidateVerificationCode(string normalizedEmail, string code, int attempts)
        {
            if (normalizedEmail.Length == 0)
                throw new ArgumentException("Email is required", nameof(normalizedEmail));

            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Verification code is required", nameof(code));

            if (attempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(attempts), attempts, "Attempt count must be greater than zero");
        }

        private static bool Matches(
            string storedCode,
            DateTime? storedExpiresAt,
            int? storedAttempts,
            string code,
            DateTime? expiresAt,
            int? attempts)
        {
            return string.Equals(storedCode, code, StringComparison.Ordinal) &&
                storedExpiresAt.HasValue == expiresAt.HasValue &&
                (!storedExpiresAt.HasValue || AsUtc(storedExpiresAt.Value) == AsUtc(expiresAt.Value)) &&
                storedAttempts == attempts;
        }

        public async Task UpdateAccountAsync(IAccountInfoData account, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PrepareAccount(account);
            using SqlSugarClient db = new(configuration);

            var tran = await db.Ado.UseTranAsync(async () =>
            {
                account.UpdatedAt = DateTime.UtcNow;
                var accountData = new AccountInfoData(account);

                await ThrowIfDuplicateAccountAsync(db, accountData, cancellationToken);

                await db.Updateable(accountData)
                    .WhereColumns(new[] { "id" })
                    .ExecuteCommandAsync();
                cancellationToken.ThrowIfCancellationRequested();

                if (account.ExtraProperties?.Any() == true)
                {
                    var extras = account.ExtraProperties.Select(kv => new ExtraPropertyData
                    {
                        AccountId = account.Id,
                        PropertyKey = kv.Key,
                        PropertyValue = kv.Value
                    }).ToList();

                    await db.Storageable(extras)
                        .WhereColumns(new[] { "account_id", "property_key" })
                        .ExecuteCommandAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                }
            });

            if (!tran.IsSuccess)
                throw tran.ErrorException;
        }

        private static ISugarQueryable<AccountInfoData> ApplySorting(
            ISugarQueryable<AccountInfoData> query,
            string sortField,
            bool isDesc)
        {
            return sortField switch
            {
                MstParamKeys.QS_PARAM_ID => isDesc
                    ? query.OrderBy(x => x.Id, OrderByType.Desc)
                    : query.OrderBy(x => x.Id, OrderByType.Asc),

                MstParamKeys.QS_PARAM_USERNAME => isDesc
                    ? query.OrderBy(x => x.Username, OrderByType.Desc)
                    : query.OrderBy(x => x.Username, OrderByType.Asc),

                MstParamKeys.QS_PARAM_EMAIL => isDesc
                    ? query.OrderBy(x => x.Email, OrderByType.Desc)
                    : query.OrderBy(x => x.Email, OrderByType.Asc),

                MstParamKeys.QS_PARAM_LAST_LOGIN => isDesc
                    ? query.OrderBy(x => x.LastLoginAt, OrderByType.Desc)
                    : query.OrderBy(x => x.LastLoginAt, OrderByType.Asc),

                MstParamKeys.QS_PARAM_CREATED_AT => isDesc
                    ? query.OrderBy(x => x.CreatedAt, OrderByType.Desc)
                    : query.OrderBy(x => x.CreatedAt, OrderByType.Asc),

                MstParamKeys.QS_PARAM_UPDATED_AT => isDesc
                    ? query.OrderBy(x => x.UpdatedAt, OrderByType.Desc)
                    : query.OrderBy(x => x.UpdatedAt, OrderByType.Asc),

                MstParamKeys.QS_PARAM_IS_GUEST => isDesc
                    ? query.OrderBy(x => x.IsGuest, OrderByType.Desc)
                    : query.OrderBy(x => x.IsGuest, OrderByType.Asc),

                MstParamKeys.QS_PARAM_IS_ADMIN => isDesc
                    ? query.OrderBy(x => x.IsAdmin, OrderByType.Desc)
                    : query.OrderBy(x => x.IsAdmin, OrderByType.Asc),

                _ => query.OrderBy(x => x.Id, OrderByType.Asc)
            };
        }

        public async Task<DatabaseEntriesInfo<IAccountInfoData>> Search(Dictionary<string, object> filter, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Page and size normalization
            int size = int.Parse(filter[MstParamKeys.QS_PARAM_SIZE].ToString());
            int page = int.Parse(filter[MstParamKeys.QS_PARAM_PAGE].ToString());
            int skip = page * size;

            // Safe read of filters
            string searchValue = filter.TryGetValue(MstParamKeys.QS_PARAM_VALUE, out var sv)
                ? sv?.ToString() ?? string.Empty
                : string.Empty;

            string sortDir = filter.TryGetValue(MstParamKeys.QS_PARAM_SORT_DIR, out var sd)
                ? sd?.ToString() ?? string.Empty
                : string.Empty;

            string sortField = filter.TryGetValue(MstParamKeys.QS_PARAM_SORT_FIELD, out var sf)
                ? sf?.ToString() ?? string.Empty
                : string.Empty;

            string searchDateValue = filter.TryGetValue(MstParamKeys.QS_PARAM_DATE_VALUE, out var sdv)
                ? sdv?.ToString() ?? string.Empty
                : string.Empty;

            string blockedValue = filter.TryGetValue(MstParamKeys.QS_PARAM_IS_BLOCKED, out var ibv)
                ? ibv?.ToString() ?? string.Empty
                : string.Empty;

            string[] searchDateValueProperties = searchDateValue.Split(",", StringSplitOptions.RemoveEmptyEntries);

            // Date filters
            DateTime searchDateFrom = DateTime.MinValue;
            DateTime searchDateTo = DateTime.UtcNow;

            if (filter.TryGetValue(MstParamKeys.QS_PARAM_DATE_FROM, out var sdf) &&
                DateTime.TryParse(sdf?.ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTime parsedFrom))
            {
                searchDateFrom = parsedFrom;
            }

            if (filter.TryGetValue(MstParamKeys.QS_PARAM_DATE_TO, out var sdt) &&
                DateTime.TryParse(sdt?.ToString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTime parsedTo) &&
                parsedTo >= searchDateFrom)
            {
                searchDateTo = parsedTo;
            }

            using SqlSugarClient db = new(configuration);

            // Total rows without filters
            int totalAll = await db.Queryable<AccountInfoData>().CountAsync();
            cancellationToken.ThrowIfCancellationRequested();

            // Current UTC time for account block filtering
            DateTime utcNow = DateTime.UtcNow;
            List<string> activeBlockedAccountIds = null;

            // Base query
            ISugarQueryable<AccountInfoData> query = db.Queryable<AccountInfoData>();

            // Date filtering
            if (searchDateValueProperties.Length > 0)
            {
                bool hasLastLoginAt = searchDateValueProperties.Contains(MstParamKeys.QS_PARAM_LAST_LOGIN);
                bool hasCreatedAt = searchDateValueProperties.Contains(MstParamKeys.QS_PARAM_CREATED_AT);
                bool hasUpdatedAt = searchDateValueProperties.Contains(MstParamKeys.QS_PARAM_UPDATED_AT);

                query = query.Where(ac =>
                    (hasLastLoginAt && ac.LastLoginAt >= searchDateFrom && ac.LastLoginAt <= searchDateTo) ||
                    (hasCreatedAt && ac.CreatedAt >= searchDateFrom && ac.CreatedAt <= searchDateTo) ||
                    (hasUpdatedAt && ac.UpdatedAt >= searchDateFrom && ac.UpdatedAt <= searchDateTo));
            }
            else
            {
                query = query.Where(ac =>
                    (ac.LastLoginAt >= searchDateFrom && ac.LastLoginAt <= searchDateTo) ||
                    (ac.CreatedAt >= searchDateFrom && ac.CreatedAt <= searchDateTo) ||
                    (ac.UpdatedAt >= searchDateFrom && ac.UpdatedAt <= searchDateTo));
            }

            // Text filtering
            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                string[] searchTerms = searchValue
                    .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string searchTerm in searchTerms)
                {
                    string term = searchTerm.Trim();

                    if (string.IsNullOrWhiteSpace(term))
                        continue;

                    query = query.Where(ac =>
                        ac.Username.Contains(term) ||
                        ac.Id.Contains(term) ||
                        ac.Email.Contains(term));
                }
            }

            // Account block filtering
            if (!string.IsNullOrWhiteSpace(blockedValue) &&
                bool.TryParse(blockedValue, out bool isBlocked))
            {
                activeBlockedAccountIds = await db.Queryable<AccountBlockData>()
                    .Where(block => block.RemovedAt == null && block.BlockedUntil > utcNow)
                    .Select(block => block.AccountId)
                    .ToListAsync();
                cancellationToken.ThrowIfCancellationRequested();

                if (isBlocked)
                {
                    query = activeBlockedAccountIds.Count == 0
                        ? query.Where(ac => ac.Id == "__no_blocked_accounts__")
                        : query.Where(ac => activeBlockedAccountIds.Contains(ac.Id));
                }
                else
                {
                    if (activeBlockedAccountIds.Count > 0)
                        query = query.Where(ac => !activeBlockedAccountIds.Contains(ac.Id));
                }
            }

            // Count after filters
            int filteredCount = await query.CountAsync();
            cancellationToken.ThrowIfCancellationRequested();

            // SQL sorting
            bool isDesc = sortDir == ((int)MstSortDirection.Desc).ToString()
                || Enum.TryParse(sortDir, true, out MstSortDirection sortDirection) && sortDirection == MstSortDirection.Desc;

            if (sortField == MstParamKeys.QS_PARAM_IS_BLOCKED)
            {
                activeBlockedAccountIds ??= await db.Queryable<AccountBlockData>()
                    .Where(block => block.RemovedAt == null && block.BlockedUntil > utcNow)
                    .Select(block => block.AccountId)
                    .ToListAsync();
                cancellationToken.ThrowIfCancellationRequested();

                query = activeBlockedAccountIds.Count == 0
                    ? query.OrderBy(ac => ac.Id, OrderByType.Asc)
                    : isDesc
                        ? query.OrderBy(ac => activeBlockedAccountIds.Contains(ac.Id), OrderByType.Desc)
                        : query.OrderBy(ac => activeBlockedAccountIds.Contains(ac.Id), OrderByType.Asc);
            }
            else
            {
                query = ApplySorting(query, sortField, isDesc);
            }

            // SQL paging
            List<AccountInfoData> pageItems = await query
                .Distinct()
                .Skip(skip)
                .Take(size)
                .ToListAsync();
            cancellationToken.ThrowIfCancellationRequested();

            // Load extra properties only for current page
            List<string> ids = pageItems.Select(x => x.Id).ToList();

            List<ExtraPropertyData> extras = ids.Count == 0
                ? new List<ExtraPropertyData>()
                : await db.Queryable<ExtraPropertyData>()
                    .Where(ep => ids.Contains(ep.AccountId))
                    .ToListAsync();
            cancellationToken.ThrowIfCancellationRequested();

            foreach (var item in pageItems)
            {
                item.ExtraProperties = extras
                    .Where(ep => ep.AccountId == item.Id)
                    .ToDictionary(ep => ep.PropertyKey, ep => ep.PropertyValue);
            }

            return new DatabaseEntriesInfo<IAccountInfoData>
            {
                entries = pageItems.Cast<IAccountInfoData>().ToList(),
                total = totalAll,
                filtered = filteredCount
            };
        }
    }
}

