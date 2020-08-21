using LiteDB;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer.Examples.BasicAuthorization
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor
    {
        private readonly ILiteCollection<AccountInfoLiteDb> accounts;
        private readonly ILiteCollection<PasswordResetData> resetCodes;
        private readonly ILiteCollection<EmailConfirmationData> emailConfirmationCodes;

        private readonly LiteDatabase database;

        public AccountsDatabaseAccessor(LiteDatabase database)
        {
            this.database = database;

            accounts = this.database.GetCollection<AccountInfoLiteDb>("accounts");
            accounts.EnsureIndex(a => a.Username, true);
            accounts.EnsureIndex(a => a.Email, true);

            resetCodes = this.database.GetCollection<PasswordResetData>("resetCodes");
            resetCodes.EnsureIndex(a => a.Email, true);

            emailConfirmationCodes = this.database.GetCollection<EmailConfirmationData>("emailConfirmationCodes");
            emailConfirmationCodes.EnsureIndex(a => a.Email, true);
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
                account = accounts.FindOne(a => a.Username == username);
            });

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token)
        {
            IAccountInfoData account = default;

            await Task.Run(() =>
            {
                account = accounts.FindOne(a => a.Token == token);
            });

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email)
        {
            IAccountInfoData account = default;

            await Task.Run(() =>
            {
                account = accounts.FindOne(i => i.Email == email.ToLower());
            });

            return account;
        }

        public async Task SavePasswordResetCodeAsync(IAccountInfoData account, string code)
        {
            await Task.Run(() => {
                resetCodes.DeleteMany(i => i.Email == account.Email.ToLower());
                resetCodes.Insert(new PasswordResetData()
                {
                    Email = account.Email,
                    Code = code
                });
            });
        }

        public async Task<IPasswordResetData> GetPasswordResetDataAsync(string email)
        {
            IPasswordResetData data = default;

            await Task.Run(() => {
                data = resetCodes.FindOne(i => i.Email == email.ToLower());
            });

            return data;
        }

        public async Task SaveEmailConfirmationCodeAsync(string email, string code)
        {
            await Task.Run(() => {
                emailConfirmationCodes.DeleteMany(i => i.Email == email.ToLower());
                emailConfirmationCodes.Insert(new EmailConfirmationData()
                {
                    Code = code,
                    Email = email
                });
            });
        }

        public async Task<string> GetEmailConfirmationCodeAsync(string email)
        {
            string code = string.Empty;

            await Task.Run(() => {
                var entry = emailConfirmationCodes.FindOne(i => i.Email == email);
                code = entry != null ? entry.Code : string.Empty;
            });

            return code;
        }

        public async Task UpdateAccountAsync(IAccountInfoData account)
        {
            await Task.Run(() => accounts.Update(account as AccountInfoLiteDb));
        }

        public async Task InsertNewAccountAsync(IAccountInfoData account)
        {
            await Task.Run(() => accounts.Insert(account as AccountInfoLiteDb));
        }

        public async Task InsertTokenAsync(IAccountInfoData account, string token)
        {
            await Task.Run(() => {
                account.Token = token;
                accounts.Update(account as AccountInfoLiteDb);
            });
        }

        private class PasswordResetData : IPasswordResetData
        {
            [BsonId]
            public string Email { get; set; }
            public string Code { get; set; }
        }

        private class EmailConfirmationData
        {
            [BsonId]
            public string Email { get; set; }
            public string Code { get; set; }
        }

        /// <summary>
        /// LiteDB implementation of account data
        /// </summary>
        private class AccountInfoLiteDb : IAccountInfoData
        {
            [BsonId]
            public string Username { get; set; }
            public string Password { get; set; }
            public string Email { get; set; }
            public string Token { get; set; }
            public bool IsAdmin { get; set; }
            public bool IsGuest { get; set; }
            public bool IsEmailConfirmed { get; set; }
            public Dictionary<string, string> Properties { get; set; }

            public event Action<IAccountInfoData> OnChangedEvent;

            public AccountInfoLiteDb()
            {
                Username = string.Empty;
                Password = string.Empty;
                Email = string.Empty;
                Token = string.Empty;
                IsAdmin = false;
                IsGuest = false;
                IsEmailConfirmed = false;
                Properties = new Dictionary<string, string>();
            }

            public void MarkAsDirty()
            {
                OnChangedEvent?.Invoke(this);
            }

            public bool HasToken()
            {
                return !string.IsNullOrEmpty(Token);
            }

            public bool IsTokenExpired()
            {
                throw new NotImplementedException();
            }
        }
    }
}
