using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using MySql.Data.MySqlClient;
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
                                if (profile.TryGet(reader.GetString("property_key").ToUint16Hash(), out IObservableProperty property))
                                {
                                    property.Deserialize(reader.GetString("property_value"));
                                }
                            }
                        }
                    }
                }
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
                        sql.Append($"('{profile.UserId}','{Mst.Registry.GetProfilePropertyOpCodeName(property.Key)}','{property.Serialize()}'){(index < profile.Count ? "," : "")} ");
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
