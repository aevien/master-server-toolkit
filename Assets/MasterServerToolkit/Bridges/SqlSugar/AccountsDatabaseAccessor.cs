using MasterServerToolkit.MasterServer;
using Microsoft.Identity.Client;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor
    {
        private ConnectionConfig configuration;

        public AccountsDatabaseAccessor(ConnectionConfig configuration)
        {
            this.configuration = configuration;

            using (SqlSugarClient db = new SqlSugarClient(configuration))
            {
                db.CodeFirst.InitTables(
                    typeof(AccountInfoData),
                    typeof(EmailConfirmationData),
                    typeof(ExtraPropertyData),
                    typeof(PasswordResetData),
                    typeof(ProfilePropertyData)
                    );
            }
        }

        public MstProperties CustomProperties { get; private set; }

        public Logging.Logger Logger { get; set; }

        public async Task<bool> CheckEmailConfirmationCodeAsync(string email, string code)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        var entry = db.Queryable<EmailConfirmationData>().Where(i => i.Email == email).First();

                        if (entry != null && entry.Code == code)
                        {
                            db.Deleteable<EmailConfirmationData>().Where(i => i.Email == email).ExecuteCommand();
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return false;
                }
            });
        }

        public async Task<bool> CheckPasswordResetCodeAsync(string email, string code)
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        var entry = db.Queryable<PasswordResetData>().Where(i => i.Email == email).First();

                        if (entry != null && entry.Code == code)
                        {
                            db.Deleteable<PasswordResetData>().Where(i => i.Email == email).ExecuteCommand();
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return false;
                }
            });
        }

        public IAccountInfoData CreateAccountInstance()
        {
            return new AccountInfoData();
        }

        public void Dispose() { }

        private void UpdateLastLogin(SqlSugarClient db, AccountInfoData account)
        {
            try
            {
                account.LastLogin = DateTime.UtcNow;
                db.Updateable<AccountInfoData>(a => a.LastLogin == account.LastLogin).Where(a => a.Id == account.Id).ExecuteCommand();
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public async Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        var account = db.Queryable<AccountInfoData>().Where(a => a.DeviceId == deviceId).First();

                        if (account != null)
                        {
                            account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                            UpdateLastLogin(db, account);
                        }

                        return account;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return null;
                }
            });
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        var account = db.Queryable<AccountInfoData>().Where(a => a.Email == email).First();

                        if (account != null)
                        {
                            account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                            UpdateLastLogin(db, account);
                        }

                        return account;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return null;
                }
            });
        }

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        var property = db.Queryable<ExtraPropertyData>()
                            .Where(ep => ep.PropertyKey == propertyKey
                             && ep.PropertyValue == propertyValue).First();

                        if (property != null)
                        {
                            return await GetAccountByIdAsync(property.AccountId);
                        }

                        return null;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return null;
                }
            });
        }

        public async Task<IAccountInfoData> GetAccountByIdAsync(string accountId)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        var account = db.Queryable<AccountInfoData>().Where(a => a.Id == accountId).First();

                        if (account != null)
                        {
                            account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                            UpdateLastLogin(db, account);
                        }

                        return account;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return null;
                }
            });
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        var account = db.Queryable<AccountInfoData>().Where(a => a.Token == token).First();

                        if (account != null)
                        {
                            account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                            UpdateLastLogin(db, account);
                        }

                        return account;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return null;
                }
            });
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username)
        {
            return await Task.Run(async () =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        var account = db.Queryable<AccountInfoData>().Where(a => a.Username == username).First();

                        if (account != null)
                        {
                            account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                            UpdateLastLogin(db, account);
                        }

                        return account;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return null;
                }
            });
        }

        public async Task<Dictionary<string, string>> GetExtraPropertiesAsync(SqlSugarClient db, string accountId)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var extraProperties = new Dictionary<string, string>();

                    foreach (var property in db.Queryable<ExtraPropertyData>().Where(p => p.AccountId == accountId).ToList())
                    {
                        extraProperties.Add(property.PropertyKey, property.PropertyValue);
                    }

                    return extraProperties;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                    return new Dictionary<string, string>();
                }
            });
        }

        public Task<string> InsertAccountAsync(IAccountInfoData account)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        db.Insertable<AccountInfoData>(account).ExecuteCommand();

                        List<ExtraPropertyData> extraProperties = new List<ExtraPropertyData>();

                        foreach (var property in account.ExtraProperties)
                        {
                            extraProperties.Add(new ExtraPropertyData()
                            {
                                AccountId = account.Id,
                                PropertyKey = property.Key,
                                PropertyValue = property.Value
                            });
                        }

                        if (extraProperties.Count > 0)
                        {
                            db.Insertable(extraProperties).ExecuteCommand();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }

                return account.Id;
            });
        }

        public Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        db.Updateable<AccountInfoData>(a => a.Token == token).Where(a => a.Id == account.Id).ExecuteCommand();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            });
        }

        public Task SaveEmailConfirmationCodeAsync(string email, string code)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        db.Storageable(new EmailConfirmationData()
                        {
                            Email = email,
                            Code = code
                        }).WhereColumns(new string[] { "email" }).ExecuteCommand();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            });
        }

        public Task SavePasswordResetCodeAsync(string email, string code)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        db.Storageable(new PasswordResetData()
                        {
                            Email = email,
                            Code = code
                        }).WhereColumns(new string[] { "email" }).ExecuteCommand();
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            });
        }

        public Task UpdateAccountAsync(IAccountInfoData account)
        {
            return Task.Run(() =>
            {
                try
                {
                    using (SqlSugarClient db = new SqlSugarClient(configuration))
                    {
                        account.Updated = DateTime.UtcNow;

                        db.Updateable<AccountInfoData>(account).WhereColumns(new string[] { "id" }).ExecuteCommand();

                        List<ExtraPropertyData> extraProperties = new List<ExtraPropertyData>();

                        foreach (var property in account.ExtraProperties)
                        {
                            extraProperties.Add(new ExtraPropertyData()
                            {
                                AccountId = account.Id,
                                PropertyKey = property.Key,
                                PropertyValue = property.Value
                            });
                        }

                        if (extraProperties.Count > 0)
                        {
                            db.Storageable(extraProperties).WhereColumns(new string[] { "account_id", "property_key" }).ExecuteCommand();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(ex);
                }
            });
        }
    }
}