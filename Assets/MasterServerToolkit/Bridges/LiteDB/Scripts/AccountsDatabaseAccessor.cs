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
        private ILiteCollection<AccountInfoLiteDb> accountsCollection;
        private ILiteCollection<PasswordResetDataLiteDb> resetCodesCollection;
        private ILiteCollection<EmailConfirmationDataLiteDb> emailConfirmationCodesCollection;
        private readonly LiteDatabase database;

        public AccountsDatabaseAccessor(string database)
        {
            Logs.Info($"Create {database} database accessor");
            this.database = new LiteDatabase($"{database}.db");
        }

        ~AccountsDatabaseAccessor()
        {
            database?.Dispose();
        }

        public void InitCollections()
        {
            accountsCollection = database.GetCollection<AccountInfoLiteDb>("accounts");
            accountsCollection.EnsureIndex(a => a.Id, true);

            resetCodesCollection = database.GetCollection<PasswordResetDataLiteDb>("resetCodes");
            resetCodesCollection.EnsureIndex(a => a.Email, true);

            emailConfirmationCodesCollection = database.GetCollection<EmailConfirmationDataLiteDb>("emailConfirmationCodes");
            emailConfirmationCodesCollection.EnsureIndex(a => a.Email, true);
        }

        public IAccountInfoData CreateAccountInstance()
        {
            return new AccountInfoLiteDb();
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username)
        {
            IAccountInfoData account = default;

            await Task.Run(() =>
            {
                account = accountsCollection.FindOne(a => a.Username == username);
            });

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token)
        {
            IAccountInfoData account = default;

            await Task.Run(() =>
            {
                account = accountsCollection.FindOne(a => a.Token == token);
            });

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email)
        {
            IAccountInfoData account = default;

            await Task.Run(() =>
            {
                account = accountsCollection.FindOne(i => i.Email == email.ToLower());
            });

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByIdAsync(string id)
        {
            return await Task.Run(() =>
            {
                return accountsCollection.FindOne(i => i.Id == id.ToLower());
            });
        }

        public async Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId)
        {
            return await Task.Run(() =>
            {
                return accountsCollection.FindOne(i => i.DeviceId == deviceId.ToLower());
            });
        }

        public async Task<IEnumerable<IAccountInfoData>> GetAccountsByIdAsync(IEnumerable<string> ids)
        {
            return await Task.Run(() =>
            {
                return accountsCollection.FindAll().Where(i => ids.Any(id => id == i.Id));
            });
        }

        public async Task<IEnumerable<IAccountInfoData>> GetPagedAccounts(int pageIndex = 0, int pageSize = 100)
        {
            IEnumerable<IAccountInfoData> accounts = default;

            await Task.Run(() =>
            {
                accounts = accountsCollection.FindAll().Skip(pageSize * pageIndex).Take(pageSize);
            });

            return accounts;
        }

        public async Task SavePasswordResetCodeAsync(IAccountInfoData account, string code)
        {
            await Task.Run(() =>
            {
                resetCodesCollection.DeleteMany(i => i.Email == account.Email.ToLower());
                resetCodesCollection.Insert(new PasswordResetDataLiteDb()
                {
                    Email = account.Email,
                    Code = code
                });
            });
        }

        public async Task<IPasswordResetData> GetPasswordResetDataAsync(string email)
        {
            IPasswordResetData data = default;

            await Task.Run(() =>
            {
                data = resetCodesCollection.FindOne(i => i.Email == email.ToLower());
            });

            return data;
        }

        public async Task SaveEmailConfirmationCodeAsync(string email, string code)
        {
            await Task.Run(() =>
            {
                emailConfirmationCodesCollection.DeleteMany(i => i.Email == email.ToLower());
                emailConfirmationCodesCollection.Insert(new EmailConfirmationDataLiteDb()
                {
                    Code = code,
                    Email = email
                });
            });
        }

        public async Task<string> GetEmailConfirmationCodeAsync(string email)
        {
            string code = string.Empty;

            await Task.Run(() =>
            {
                var entry = emailConfirmationCodesCollection.FindOne(i => i.Email == email);
                code = entry != null ? entry.Code : string.Empty;
            });

            return code;
        }

        public async Task<bool> UpdateAccountAsync(IAccountInfoData account)
        {
            string username = account.Username?.Trim();
            string email = account.Email?.Trim();

            // Check username duplicate
            if (!string.IsNullOrEmpty(username))
            {
                var existingAccount = await GetAccountByUsernameAsync(username);

                // if this account is not ours
                if (existingAccount != null && existingAccount.Id != account.Id)
                {
                    throw new Exception("You are trying to update your username but it is already taken by another user");
                }
            }

            // Check email duplicate
            if (!string.IsNullOrEmpty(email))
            {
                var existingAccount = await GetAccountByEmailAsync(email);

                // if this account is not ours
                if (existingAccount != null && existingAccount.Id != account.Id)
                {
                    throw new Exception("There is another user with this email");
                }
            }

            return await Task.Run(() => accountsCollection.Update(account as AccountInfoLiteDb));
        }

        public async Task<string> InsertNewAccountAsync(IAccountInfoData account)
        {
            string username = account.Username.Trim();
            string email = account.Email.Trim();

            // Check username duplicate
            if (!string.IsNullOrEmpty(username))
            {
                var existingAccount = await GetAccountByUsernameAsync(username);

                // if this account is not ours
                if (existingAccount != null && existingAccount.Id != account.Id)
                {
                    throw new Exception($"User with username \"{username}\" already exists");
                }
            }

            // Check email duplicate
            if (!string.IsNullOrEmpty(email))
            {
                var existingAccount = await GetAccountByEmailAsync(email);

                // if this account is not ours
                if (existingAccount != null && existingAccount.Id != account.Id)
                {
                    throw new Exception($"User with email \"{email}\" already exists");
                }
            }

            return await Task.Run(() => accountsCollection.Insert(account as AccountInfoLiteDb).AsString);
        }

        public async Task<bool> InsertTokenAsync(IAccountInfoData account, string token)
        {
            return await Task.Run(() =>
            {
                account.Token = token;
                return accountsCollection.Update(account as AccountInfoLiteDb);
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

        public Task<IAccountInfoData> GetAccountByPhoneNumberAsync(string phoneNumber)
        {
            throw new NotImplementedException();
        }
    }
}
