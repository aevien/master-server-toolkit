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
                db.CodeFirst.InitTables(typeof(ProfilePropertyData));
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

        public Task UpdateProfileAsync(ObservableServerProfile profile)
        {
            return Task.Run(() =>
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    try
                    {
                        List<ProfilePropertyData> entries = new List<ProfilePropertyData>();

                        foreach (var property in profile)
                        {
                            entries.Add(new ProfilePropertyData()
                            {
                                AccountId = profile.UserId,
                                PropertyKey = Extensions.StringExtensions.FromHash(property.Key),
                                PropertyValue = property.ToJson().ToString()
                            });
                        }

                        db.Storageable(entries)
                        .WhereColumns(new string[] { "account_id", "property_key" })
                        .ExecuteCommand();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex);
                    }
                }
            });
        }
    }
}