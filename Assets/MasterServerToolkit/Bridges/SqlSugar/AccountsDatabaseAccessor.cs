using MasterServerToolkit.GameService;
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
                var tableTypes = new[]
                {
                    typeof(AccountInfoData),
                    typeof(EmailConfirmationData),
                    typeof(ExtraPropertyData),
                    typeof(PasswordResetData),
                    typeof(ProfilePropertyData)
                };

                foreach (var tableType in tableTypes)
                {
                    var tableName = db.EntityMaintenance.GetTableName(tableType);

                    if (!db.DbMaintenance.IsAnyTable(tableName))
                    {
                        db.CodeFirst.InitTables(tableType);
                    }
                }
            }
        }

        public MstProperties CustomProperties { get; private set; }

        public Logging.Logger Logger { get; set; }

        public async Task<bool> CheckEmailConfirmationCodeAsync(string email, string code)
        {
            using (SqlSugarClient db = new SqlSugarClient(configuration))
            {
                var entry = await db.Queryable<EmailConfirmationData>().Where(i => i.Email == email).FirstAsync();

                if (entry != null && entry.Code == code)
                {
                    await db.Deleteable<EmailConfirmationData>().Where(i => i.Email == email).ExecuteCommandAsync();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public async Task<bool> CheckPasswordResetCodeAsync(string email, string code)
        {
            using (SqlSugarClient db = new SqlSugarClient(configuration))
            {
                var entry = await db.Queryable<PasswordResetData>().Where(i => i.Email == email).FirstAsync();

                if (entry != null && entry.Code == code)
                {
                    await db.Deleteable<PasswordResetData>().Where(i => i.Email == email).ExecuteCommandAsync();
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        public IAccountInfoData CreateAccountInstance()
        {
            return new AccountInfoData();
        }

        public void Dispose() { }

        private async Task UpdateLastLogin(SqlSugarClient db, AccountInfoData account)
        {
            account.LastLogin = DateTime.UtcNow;
            await db.Updateable<AccountInfoData>(a => a.LastLogin == account.LastLogin).Where(a => a.Id == account.Id).ExecuteCommandAsync();
        }

        public async Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    var account = await db.Queryable<AccountInfoData>().Where(a => a.DeviceId == deviceId).FirstAsync();

                    if (account != null)
                    {
                        account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                        await UpdateLastLogin(db, account);
                    }

                    return account;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    var account = await db.Queryable<AccountInfoData>().Where(a => a.Email == email).FirstAsync();

                    if (account != null)
                    {
                        account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                        await UpdateLastLogin(db, account);
                    }

                    return account;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IAccountInfoData> GetAccountByExtraPropertyAsync(string propertyKey, string propertyValue)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    var property = await db.Queryable<ExtraPropertyData>()
                        .Where(ep => ep.PropertyKey == propertyKey
                         && ep.PropertyValue == propertyValue).FirstAsync();

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
        }

        public async Task<IAccountInfoData> GetAccountByIdAsync(string accountId)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    var account = await db.Queryable<AccountInfoData>().Where(a => a.Id == accountId).FirstAsync();

                    if (account != null)
                    {
                        account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                        await UpdateLastLogin(db, account);
                    }

                    return account;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    var account = await db.Queryable<AccountInfoData>().Where(a => a.Token == token).FirstAsync();

                    if (account != null)
                    {
                        account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                        await UpdateLastLogin(db, account);
                    }

                    return account;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    var account = await db.Queryable<AccountInfoData>().Where(a => a.Username == username).FirstAsync();

                    if (account != null)
                    {
                        account.ExtraProperties = await GetExtraPropertiesAsync(db, account.Id);
                        await UpdateLastLogin(db, account);
                    }

                    return account;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<Dictionary<string, string>> GetExtraPropertiesAsync(SqlSugarClient db, string accountId)
        {
            try
            {
                var extraProperties = new Dictionary<string, string>();

                foreach (var property in await db.Queryable<ExtraPropertyData>().Where(p => p.AccountId == accountId).ToListAsync())
                {
                    extraProperties.Add(property.PropertyKey, property.PropertyValue);
                }

                return extraProperties;
            }
            catch
            {
                Logger.Error("Could not get extra properties");
                return null;
            }
        }

        public async Task<string> InsertAccountAsync(IAccountInfoData account)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    await db.Insertable<AccountInfoData>(account).ExecuteCommandAsync();

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
                        await db.Insertable(extraProperties).ExecuteCommandAsync();
                    }
                }

                return account.Id;
            }
            catch
            {
                Logger.Error($"Could not insert user {account.Username} account with id {account.Id}");
                throw;
            }
        }

        public async Task InsertOrUpdateTokenAsync(IAccountInfoData account, string token)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    await db.Updateable<AccountInfoData>(a => a.Token == token).Where(a => a.Id == account.Id).ExecuteCommandAsync();
                }
            }
            catch
            {
                Logger.Error($"Could not insert token of account {account.Username} with id {account.Id}");
                throw;
            }
        }

        public async Task SaveEmailConfirmationCodeAsync(string email, string code)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    await db.Storageable(new EmailConfirmationData()
                    {
                        Email = email,
                        Code = code
                    }).WhereColumns(new string[] { "email" }).ExecuteCommandAsync();
                }
            }
            catch
            {
                Logger.Error($"Could not save email conformation code. Email {email}");
                throw;
            }
        }

        public async Task SavePasswordResetCodeAsync(string email, string code)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    await db.Storageable(new PasswordResetData()
                    {
                        Email = email,
                        Code = code
                    }).WhereColumns(new string[] { "email" }).ExecuteCommandAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Could not save pasword reset code. Email {email}");
                throw;
            }
        }

        public async Task UpdateAccountAsync(IAccountInfoData account)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    account.Updated = DateTime.UtcNow;

                    await db.Updateable<AccountInfoData>(account).WhereColumns(new string[] { "id" }).ExecuteCommandAsync();

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
                        await db.Storageable(extraProperties).WhereColumns(new string[] { "account_id", "property_key" }).ExecuteCommandAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }
        }
    }
}