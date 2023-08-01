using MasterServerToolkit.Extensions;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MySql.Data.MySqlClient;
using System;
using System.Text;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.MySQL
{
    public class ProfilesDatabaseAccessor : IProfilesDatabaseAccessor
    {
        private string connectionString = string.Empty;
        public MstProperties CustomProperties { get; private set; } = new MstProperties();

        public ProfilesDatabaseAccessor(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public void Dispose() { }

        public async Task RestoreProfileAsync(ObservableServerProfile profile)
        {
            try
            {
                using (MySqlConnection connection = new MySqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    using (var cmd = new MySqlCommand())
                    {
                        cmd.Connection = connection;
                        cmd.CommandText = $"SELECT `property_key`, `property_value` FROM profiles WHERE account_id = '{profile.UserId}';";

                        using (MySqlDataReader reader = (MySqlDataReader)await cmd.ExecuteReaderAsync())
                        {
                            if (reader.HasRows)
                            {
                                while (await reader.ReadAsync())
                                {
                                    string propertyKey = reader.GetString("property_key");

                                    if (profile.TryGet(propertyKey.ToUint16Hash(), out IObservableProperty property))
                                    {
                                        string propertyValue = reader.GetString("property_value");
                                        property.FromJson(propertyValue);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logs.Error(ex);
            }
        }

        public async Task UpdateProfileAsync(ObservableServerProfile profile)
        {
            using (var con = new MySqlConnection(connectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    con.Open();

                    cmd.Connection = con;

                    StringBuilder sql = new StringBuilder();
                    sql.Append($"INSERT INTO profiles ");
                    sql.Append($"(`account_id`,`property_key`,`property_value`) VALUES ");

                    int index = 0;

                    foreach (var property in profile)
                    {
                        index++;
                        sql.Append($"('{profile.UserId}','{Extensions.StringExtensions.FromHash(property.Key)}','{property.ToJson()}'){(index < profile.Count ? "," : "")} ");
                    }

                    sql.Append($"as p ");
                    sql.Append($"ON DUPLICATE KEY UPDATE account_id=p.account_id, property_key=p.property_key, property_value=p.property_value;");

                    cmd.CommandText = sql.ToString();

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
