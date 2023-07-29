using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.MySQL
{
    public class AccountsDatabaseAccessor : IAccountsDatabaseAccessor
    {
        private string connectionString = string.Empty;
        public MstProperties CustomProperties { get; private set; } = new MstProperties();

        public AccountsDatabaseAccessor(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public IAccountInfoData CreateAccountInstance()
        {
            return new AccountInfoData();
        }

        public void Dispose() { }

        public async Task<IAccountInfoData> GetAccountByDeviceIdAsync(string deviceId)
        {
            return await GetAccountByPropertyAsync("device_id", deviceId);
        }

        public async Task<IAccountInfoData> GetAccountByPropertyAsync(string propertyName, string propertyValue)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var cmd = new MySqlCommand())
                {
                    cmd.Connection = connection;
                    cmd.CommandText = $"SELECT * FROM accounts WHERE {propertyName} = @{propertyName};";
                    cmd.Parameters.AddWithValue($"@{propertyName}", propertyValue);

                    using (MySqlDataReader reader = (MySqlDataReader)await cmd.ExecuteReaderAsync())
                    {
                        if (!reader.HasRows)
                            return null;

                        return await ReadAccountInfo(reader);
                    }
                }
            }
        }

        private async Task<IAccountInfoData> ReadAccountInfo(MySqlDataReader reader)
        {
            AccountInfoData account = null;

            while (await reader.ReadAsync())
            {
                account = new AccountInfoData()
                {
                    Id = reader.GetString("id"),
                    Username = reader.GetString("username"),
                    Password = reader.GetString("password"),
                    Email = reader.GetString("email"),
                    PhoneNumber = reader.GetString("phone_number"),
                    Token = reader.GetString("token"),
                    LastLogin = reader.GetDateTime("last_login"),
                    IsAdmin = reader.GetBoolean("is_admin"),
                    IsGuest = reader.GetBoolean("is_guest"),
                    DeviceId = reader.GetString("device_id"),
                    DeviceName = reader.GetString("device_name"),
                    IsEmailConfirmed = reader.GetBoolean("is_email_confirmed"),
                    Properties = new Dictionary<string, string>(),
                };

                var properties = new MstJson(reader.GetString("properties"));
                account.Properties = properties.ToDictionary();
            }

            if (account == null)
                return null;

            await reader.CloseAsync();

            return account;
        }

        public async Task<IAccountInfoData> GetAccountByEmailAsync(string email)
        {
            return await GetAccountByPropertyAsync("email", email);
        }

        public async Task<IAccountInfoData> GetAccountByIdAsync(string id)
        {
            return await GetAccountByPropertyAsync("id", id);
        }

        public async Task<IAccountInfoData> GetAccountByPhoneNumberAsync(string phoneNumber)
        {
            return await GetAccountByPropertyAsync("phone_number", phoneNumber);
        }

        public async Task<IAccountInfoData> GetAccountByTokenAsync(string token)
        {
            return await GetAccountByPropertyAsync("token", token);
        }

        public async Task<IAccountInfoData> GetAccountByUsernameAsync(string username)
        {
            return await GetAccountByPropertyAsync("username", username);
        }

        public async Task SaveEmailConfirmationCodeAsync(string email, string code)
        {
            using (var con = new MySqlConnection(connectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    con.Open();

                    cmd.Connection = con;
                    cmd.CommandText = "INSERT INTO email_confirmation_codes (email, code) " +
                        "VALUES(@email, @code) " +
                        "ON DUPLICATE KEY UPDATE code = @code";

                    cmd.Parameters.AddWithValue("@email", email);
                    cmd.Parameters.AddWithValue("@code", code);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<string> GetEmailConfirmationCodeAsync(string email)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var cmd = new MySqlCommand())
                {
                    cmd.Connection = connection;
                    cmd.CommandText = $"SELECT * FROM email_confirmation_codes WHERE email = @email;";
                    cmd.Parameters.AddWithValue($"@email", email);

                    var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

                    if (!reader.HasRows)
                        return null;

                    return reader.GetString("code");
                }
            }
        }

        public async Task SavePasswordResetCodeAsync(IAccountInfoData account, string code)
        {
            using (var con = new MySqlConnection(connectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    con.Open();

                    cmd.Connection = con;
                    cmd.CommandText = "INSERT INTO password_reset_codes (email, code) " +
                        "VALUES(@email, @code) " +
                        "ON DUPLICATE KEY UPDATE code = @code";

                    cmd.Parameters.AddWithValue("@email", account.Email);
                    cmd.Parameters.AddWithValue("@code", code);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<string> GetPasswordResetDataAsync(string email)
        {
            using (MySqlConnection connection = new MySqlConnection(connectionString))
            {
                await connection.OpenAsync();

                using (var cmd = new MySqlCommand())
                {
                    cmd.Connection = connection;
                    cmd.CommandText = "SELECT * FROM password_reset_codes WHERE email = @email";
                    cmd.Parameters.AddWithValue("@email", email);

                    var reader = (MySqlDataReader)await cmd.ExecuteReaderAsync();

                    if (!reader.HasRows)
                        return null;

                    return reader.GetString("code");
                }
            }
        }

        public Task<string> GetPhoneNumberConfirmationCodeAsync(string phoneNumber)
        {
            throw new System.NotImplementedException();
        }

        public Task<bool> CheckPhoneNumberConfirmationCodeAsync(string confirmationCode)
        {
            throw new System.NotImplementedException();
        }

        public async Task<bool> InsertTokenAsync(IAccountInfoData account, string token)
        {
            account.Token = token;

            using (var con = new MySqlConnection(connectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    con.Open();

                    cmd.Connection = con;
                    cmd.CommandText = "UPDATE accounts " +
                                      "SET token = @token " +
                                      "WHERE id = @id";

                    cmd.Parameters.AddWithValue("@id", account.Id);
                    cmd.Parameters.AddWithValue("@token", account.Token);

                    await cmd.ExecuteNonQueryAsync();

                    return true;
                }
            }
        }

        public async Task<string> InsertNewAccountAsync(IAccountInfoData account)
        {
            using (var con = new MySqlConnection(connectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    con.Open();

                    cmd.Connection = con;
                    cmd.CommandText = "INSERT INTO accounts (" +
                        "`id`," +
                        "`username`," +
                        "`password`," +
                        "`email`," +
                        "`phone_number`," +
                        "`token`," +
                        "`last_login`," +
                        "`is_admin`," +
                        "`is_guest`," +
                        "`device_id`," +
                        "`device_name`," +
                        "`is_email_confirmed`," +
                        "`properties`) VALUES (" +
                        $"'{account.Id}'," +
                        $"'{account.Username}'," +
                        $"'{account.Password}'," +
                        $"'{account.Email}'," +
                        $"'{account.PhoneNumber}'," +
                        $"'{account.Token}'," +
                        "UTC_TIMESTAMP()," +
                        $"{(account.IsAdmin ? 1 : 0)}," +
                        $"{(account.IsGuest ? 1 : 0)}," +
                        $"'{account.DeviceId}'," +
                        $"'{account.DeviceName}'," +
                        $"{(account.IsEmailConfirmed ? 1 : 0)}," +
                        $"'{new MstJson(account.Properties)}');";

                    await cmd.ExecuteNonQueryAsync();

                    return account.Id;
                }
            }
        }

        public async Task<bool> UpdateAccountAsync(IAccountInfoData account)
        {
            using (var con = new MySqlConnection(connectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    con.Open();

                    cmd.Connection = con;
                    cmd.CommandText = "UPDATE accounts SET " +
                        $"`password`='{account.Password}'," +
                        $"`email`='{account.Email}'," +
                        $"`phone_number`='{account.PhoneNumber}'," +
                        $"`token`='{account.Token}'," +
                        $"`last_login`=UTC_TIMESTAMP()," +
                        $"`is_admin`={(account.IsAdmin ? 1 : 0)}," +
                        $"`is_guest`={(account.IsGuest ? 1 : 0)}," +
                        $"`device_id`='{account.DeviceId}'," +
                        $"`device_name`='{account.DeviceName}'," +
                        $"`is_email_confirmed`={(account.IsEmailConfirmed ? 1 : 0)}" +
                        $"`properties`='{new MstJson(account.Properties)}'" +
                        $"WHERE id={account.Id};";

                    await cmd.ExecuteNonQueryAsync();

                    return true;
                }
            }
        }
    }
}