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

        public async Task<IEnumerable<IAnalyticsInfoData>> Get(int size = 1000, int page = 0)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>()
                        .OrderBy(e => e.Timestamp)
                        .Skip(size * page)
                        .Take(size)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IAnalyticsInfoData> GetById(string eventId)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>()
                        .Where(e => e.Id == eventId)
                        .OrderBy(e => e.Timestamp)
                        .FirstAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IEnumerable<IAnalyticsInfoData>> GetByKey(string eventKey, int size, int page)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>()
                        .Where(async => async.Key == eventKey)
                        .OrderBy(e => e.Timestamp)
                        .Skip(size * page)
                        .Take(size)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IEnumerable<IAnalyticsInfoData>> GetByTimestamp(DateTime timestamp)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>()
                        .Where(async => async.Timestamp == timestamp)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IEnumerable<IAnalyticsInfoData>> GetByTimestampRange(DateTime timestampStart, DateTime timestampEnd, int size = 1000, int page = 0)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>()
                        .Where(async => async.Timestamp >= timestampStart && async.Timestamp <= timestampEnd)
                        .OrderBy(e => e.Timestamp)
                        .Skip(size * page)
                        .Take(size)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IEnumerable<IAnalyticsInfoData>> GetByUserId(string userId, int size = 1000, int page = 0)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>()
                        .Where(async => async.UserId == userId)
                        .OrderBy(e => e.Timestamp)
                        .Skip(size * page)
                        .Take(size)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IEnumerable<IAnalyticsInfoData>> GetWithQuery(string query, int size = 1000, int page = 1)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(query))
                {
                    throw new ArgumentException("Query cannot be null or empty.", nameof(query));
                }

                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.SqlQueryable<AnalyticsData>(query)
                        .OrderBy(e => e.Timestamp)
                        .Skip(size * page)
                        .Take(size)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task Insert(IEnumerable<IAnalyticsInfoData> eventsData)
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
                        Key = ed.Key,
                        Timestamp = ed.Timestamp,
                        Data = ed.Data,
                        Category = ed.Category
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
