using LiteDB;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.LiteDB
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor
    {
        private readonly ILiteCollection<AccountInfoData> accountsCollection;
        private readonly ILiteCollection<ExtraPropertyData> extraPropertiesCollection;
        private readonly ILiteCollection<PasswordResetData> resetCodesCollection;
        private readonly ILiteCollection<EmailConfirmationData> emailConfirmationCodesCollection;

        private readonly LiteDatabase database;

        public MstProperties CustomProperties { get; private set; } = new MstProperties();
        public Logger Logger { get; set; }

        public AccountsDatabaseAccessor(string databaseName)
        {
            database = new LiteDatabase($"{databaseName}.db");

            accountsCollection = database.GetCollection<AccountInfoData>("accounts");
            accountsCollection.EnsureIndex(a => a.Id, true);
            accountsCollection.EnsureIndex(a => a.Username, true);

            extraPropertiesCollection = database.GetCollection<ExtraPropertyData>("extra_properties");
            extraPropertiesCollection.EnsureIndex(a => a.AccountId);
            extraPropertiesCollection.EnsureIndex(a => a.PropertyKey);

            resetCodesCollection = database.GetCollection<PasswordResetData>("reset_codes");
            resetCodesCollection.EnsureIndex(a => a.Email, true);

            emailConfirmationCodesCollection = database.GetCollection<EmailConfirmationData>("email_confirmation_codes");
            emailConfirmationCodesCollection.EnsureIndex(a => a.Email, true);
        }

        public IAccountInfoData CreateAccountInstance()
        {
            return new AccountInfoData();
        }

        public async Task<IAccountInfoData> GetAccountByIdAsync(string id)
        {
            return await Task.Run(async () =>
            {
                var account = accountsCollection.FindOne(i => i.Id == id);

                if (account != null)
                {
                    account.LastLogin = DateTime.UtcNow;
                    accountsCollection.Upsert(account);
                    account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
                }

                return account;
            });
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username)
        {
            return await Task.Run(async () =>
            {
                var account = accountsCollection.FindOne(i => i.Username == username);

                if (account != null)
                {
                    account.LastLogin = DateTime.UtcNow;
                    accountsCollection.Upsert(account);
                    account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
                }

                return account;
            });
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token)
        {
            return await Task.Run(async () =>
            {
                var account = accountsCollection.FindOne(i => i.Token == token);

                if (account != null)
                {
                    account.LastLogin = DateTime.UtcNow;
                    accountsCollection.Upsert(account);
                    account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
                }

                return account;
            });
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email)
        {
            return await Task.Run(async () =>
            {
                var account = accountsCollection.FindOne(i => i.Email == email);

                if (account != null)
                {
                    account.LastLogin = DateTime.UtcNow;
                    accountsCollection.Upsert(account);
                    account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
                }

                return account;
            });
        }

        public async Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId)
        {
            return await Task.Run(async () =>
            {
                var account = accountsCollection.FindOne(i => i.DeviceId == deviceId);

                if (account != null)
                {
                    account.LastLogin = DateTime.UtcNow;
                    accountsCollection.Upsert(account);
                    account.ExtraProperties = await GetExtraPropertiesAsync(account.Id);
                }

                return account;
            });
        }

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue)
        {
            return await Task.Run(async () =>
            {
                var extraProperty = extraPropertiesCollection.FindOne(i => i.PropertyKey == propertyKey && i.PropertyValue == propertyValue);

                if (extraProperty != null)
                {
                    var account = await GetAccountByIdAsync(extraProperty.AccountId);

                    if (account != null)
                    {
                        return account;
                    }
                }

                return null;
            });
        }

        private async Task<Dictionary<string, string>> GetExtraPropertiesAsync(string accountId)
        {
            return await Task.Run(() =>
            {
                return extraPropertiesCollection.Find(i => i.AccountId == accountId).ToDictionary(i => i.PropertyKey, i => i.PropertyValue);
            });
        }

        public async Task<bool> CheckEmailConfirmationCodeAsync(string email, string code)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            {
                return false;
            }

            bool result = await Task.Run(() =>
            {
                EmailConfirmationData entry = emailConfirmationCodesCollection.FindOne(i => i.Email == email);

                if (entry != null && entry.Code == code)
                {
                    emailConfirmationCodesCollection.DeleteMany(i => i.Email == email.ToLower());
                    return true;
                }

                return false;
            });

            return result;
        }

        public async Task<bool> CheckPasswordResetCodeAsync(string email, string code)
        {
            bool result = await Task.Run(() =>
            {
                PasswordResetData entry = resetCodesCollection.FindOne(i => i.Email == email.ToLower());

                if (entry != null && entry.Code == code)
                {
                    resetCodesCollection.DeleteMany(i => i.Email == email.ToLower());
                    return true;
                }

                return false;
            });

            return result;
        }

        public async Task SavePasswordResetCodeAsync(string email, string code)
        {
            await Task.Run(() =>
            {
                resetCodesCollection.Upsert(new PasswordResetData()
                {
                    Email = email,
                    Code = code
                });
            });
        }

        public async Task SaveEmailConfirmationCodeAsync(string email, string code)
        {
            await Task.Run(() =>
            {
                emailConfirmationCodesCollection.Upsert(new EmailConfirmationData()
                {
                    Code = code,
                    Email = email
                });
            });
        }

        public async Task<string> InsertAccountAsync(IAccountInfoData account)
        {
            string username = account.Username.Trim();
            string email = account.Email.Trim();
            IAccountInfoData existingAccount;

            // Check username duplicate
            if (!string.IsNullOrEmpty(username))
            {
                existingAccount = await GetAccountByUsernameAsync(username);

                if (existingAccount != null)
                {
                    throw new Exception($"User with username \"{username}\" already exists");
                }
            }

            // Check email duplicate
            if (!string.IsNullOrEmpty(email))
            {
                existingAccount = await GetAccountByEmailAsync(email);

                if (existingAccount != null)
                {
                    throw new Exception($"User with email \"{email}\" already exists");
                }
            }

            return await Task.Run(async () =>
            {
                string accountId = accountsCollection.Insert(account as AccountInfoData).AsString;
                await InsertOrUpdateExtraProperties(accountId, account.ExtraProperties);
                return accountId;
            });
        }

        public async Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token)
        {
            await Task.Run(() =>
            {
                account.Token = token;
                accountsCollection.Update(account as AccountInfoData);
            });
        }

        private async Task InsertOrUpdateExtraProperties(string accountId, Dictionary<string, string> properties)
        {
            await Task.Run(() =>
            {
                foreach (KeyValuePair<string, string> pair in properties)
                {
                    extraPropertiesCollection.Upsert(new ExtraPropertyData()
                    {
                        Id = $"{accountId}_{pair.Key}",
                        AccountId = accountId,
                        PropertyKey = pair.Key,
                        PropertyValue = pair.Value
                    });
                }
            });
        }

        public Task UpdateAccountAsync(IAccountInfoData account)
        {
            return Task.Run(async () =>
            {
                accountsCollection.Update(account as AccountInfoData);
                await InsertOrUpdateExtraProperties(account.Id, account.ExtraProperties);
            });
        }

        public void Dispose()
        {
            CustomProperties?.Clear();
            database?.Dispose();
        }
    }
}