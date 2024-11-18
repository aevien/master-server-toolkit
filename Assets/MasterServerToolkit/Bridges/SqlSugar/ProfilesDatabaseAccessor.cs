using MasterServerToolkit.Extensions;
using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class ProfilesDatabaseAccessor : IProfilesDatabaseAccessor
    {
        private ConnectionConfig configuration;

        public ProfilesDatabaseAccessor(ConnectionConfig configuration)
        {
            this.configuration = configuration;

            using (SqlSugarClient db = new SqlSugarClient(configuration))
            {
                var tableTypes = new[]
                {
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

        public void Dispose() { }

        public async Task RestoreProfileAsync(ObservableServerProfile profile)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    var entries = await db.Queryable<ProfilePropertyData>().Where(p => p.AccountId == profile.UserId).ToListAsync();

                    foreach (var entry in entries)
                    {
                        if (profile.TryGet(entry.PropertyKey.ToUint16Hash(), out IObservableProperty property))
                        {
                            property.FromJson(entry.PropertyValue);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        public async Task UpdateProfileAsync(ObservableServerProfile profile)
        {
            await UpdateProfilesAsync(new List<ObservableServerProfile>()
            {
                profile
            });
        }

        public async Task UpdateProfilesAsync(IEnumerable<ObservableServerProfile> profiles)
        {
            using (SqlSugarClient db = new SqlSugarClient(configuration))
            {
                try
                {
                    // Prepare a list to store all profile property data
                    List<ProfilePropertyData> entries = new List<ProfilePropertyData>();

                    foreach (var profile in profiles)
                    {
                        foreach (var property in profile)
                        {
                            entries.Add(new ProfilePropertyData()
                            {
                                AccountId = profile.UserId,
                                PropertyKey = Extensions.StringExtensions.FromHash(property.Key),
                                PropertyValue = property.ToJson().ToString()
                            });
                        }
                    }

                    // Use Storageable for batch update/insert
                    await db.Storageable(entries)
                      .WhereColumns(new string[] { "account_id", "property_key" }) // Define unique keys for matching
                      .ExecuteCommandAsync(); // Automatically handles insert or update
                }
                catch (Exception ex)
                {
                    // Log the error for debugging
                    Logger.Error(ex);
                }
            }
        }
    }
}