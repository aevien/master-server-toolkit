using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class AnalyticsDatabaseAccessor : IAnalyticsDatabaseAccessor
    {
        private readonly ConnectionConfig configuration;

        public AnalyticsDatabaseAccessor(ConnectionConfig configuration)
        {
            this.configuration = configuration;

            using (SqlSugarClient db = new SqlSugarClient(configuration))
            {
                var tableTypes = new[]
                {
                    typeof(AnalyticsData)
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

        public async Task<IEnumerable<IAnalyticsData>> GetAll()
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>().ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IAnalyticsData> GetById(string eventId)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>().Where(async => async.Id == eventId).FirstAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IEnumerable<IAnalyticsData>> GetByUserId(string userId)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>().Where(async => async.UserId == userId).ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IEnumerable<IAnalyticsData>> GetWithQuery(string query)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    throw new ArgumentException("Query cannot be null or empty.", nameof(query));
                }

                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.SqlQueryable<AnalyticsData>(query).ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task Insert(IEnumerable<AnalyticsDataInfoPacket> eventsData)
        {
            try
            {
                if (eventsData == null || !eventsData.Any())
                {
                    throw new ArgumentException("Event data cannot be null or empty.", nameof(eventsData));
                }

                var list = eventsData.Select(ed =>
                {
                    return new AnalyticsData()
                    {
                        Id = ed.Id,
                        UserId = ed.UserId,
                        EventId = ed.EventId,
                        Timestamp = ed.Timestamp,
                        Data = ed.Data
                    };
                }).ToList();

                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    await db.Storageable(list)
                      .WhereColumns(new string[] { "id", "user_id", "event_id" })
                      .ExecuteCommandAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }
    }
}
