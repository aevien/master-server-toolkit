using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    public class AnalyticsDatabaseAccessor : IAnalyticsDatabaseAccessor
    {
        private readonly ConnectionConfig configuration;
        private const int DefaultPageSize = 1000;
        private const int DefaultPage = 0;

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

                    if (!db.DbMaintenance.IsAnyTable(tableName, false))
                    {
                        db.CodeFirst.InitTables(tableType);
                    }

                    if (!db.DbMaintenance.IsAnyTable(tableName, false))
                        throw new InvalidOperationException($"Required database table '{tableName}' was not created");
                }
            }
        }

        public MstProperties CustomProperties { get; private set; }
        public Logging.Logger Logger { get; set; }

        public void Dispose() { }

        private static int GetSkip(int size, int page)
        {
            int normalizedSize = size > 0 ? size : DefaultPageSize;
            int normalizedPage = page >= 0 ? page : DefaultPage;
            return normalizedSize * normalizedPage;
        }

        private static int GetTake(int size)
        {
            return size > 0 ? size : DefaultPageSize;
        }

        private static int GetFilterInt(Dictionary<string, object> filter, string key, int defaultValue)
        {
            if (filter == null || !filter.TryGetValue(key, out var rawValue) || rawValue == null)
                return defaultValue;

            return int.TryParse(rawValue.ToString(), out int value) ? value : defaultValue;
        }

        private static string GetFilterString(Dictionary<string, object> filter, string key)
        {
            if (filter == null || !filter.TryGetValue(key, out var rawValue) || rawValue == null)
                return string.Empty;

            return rawValue.ToString() ?? string.Empty;
        }

        private static bool IsSortDescending(string sortDir)
        {
            return sortDir == ((int)MstSortDirection.Desc).ToString()
                || Enum.TryParse(sortDir, true, out MstSortDirection sortDirection) && sortDirection == MstSortDirection.Desc;
        }

        private static ISugarQueryable<AnalyticsData> ApplySorting(
            ISugarQueryable<AnalyticsData> query,
            string sortField,
            bool isDesc)
        {
            return sortField switch
            {
                MstParamKeys.QS_PARAM_ID => isDesc
                    ? query.OrderBy(e => e.Id, OrderByType.Desc)
                    : query.OrderBy(e => e.Id, OrderByType.Asc),

                MstParamKeys.QS_PARAM_USER_ID => isDesc
                    ? query.OrderBy(e => e.UserId, OrderByType.Desc)
                    : query.OrderBy(e => e.UserId, OrderByType.Asc),

                "user_id" => isDesc
                    ? query.OrderBy(e => e.UserId, OrderByType.Desc)
                    : query.OrderBy(e => e.UserId, OrderByType.Asc),

                MstParamKeys.QS_PARAM_KEY => isDesc
                    ? query.OrderBy(e => e.Key, OrderByType.Desc)
                    : query.OrderBy(e => e.Key, OrderByType.Asc),

                MstParamKeys.QS_PARAM_CATEGORY => isDesc
                    ? query.OrderBy(e => e.Category, OrderByType.Desc)
                    : query.OrderBy(e => e.Category, OrderByType.Asc),

                MstParamKeys.QS_PARAM_TIMESTAMP => isDesc
                    ? query.OrderBy(e => e.Timestamp, OrderByType.Desc)
                    : query.OrderBy(e => e.Timestamp, OrderByType.Asc),

                _ => query.OrderBy(e => e.Timestamp, OrderByType.Desc)
            };
        }

        public async Task<DatabaseEntriesInfo<IAnalyticsInfoData>> Search(Dictionary<string, object> filter)
        {
            int size = GetFilterInt(filter, MstParamKeys.QS_PARAM_SIZE, DefaultPageSize);
            int page = GetFilterInt(filter, MstParamKeys.QS_PARAM_PAGE, DefaultPage);
            int skip = page * size;

            string id = GetFilterString(filter, MstParamKeys.QS_PARAM_ID).Trim();
            string userId = GetFilterString(filter, MstParamKeys.QS_PARAM_USER_ID).Trim();
            string eventKey = GetFilterString(filter, MstParamKeys.QS_PARAM_KEY).Trim();
            string timestampValue = GetFilterString(filter, MstParamKeys.QS_PARAM_TIMESTAMP).Trim();
            string startTimestampValue = GetFilterString(filter, MstParamKeys.QS_PARAM_START_TIMESTAMP).Trim();
            string endTimestampValue = GetFilterString(filter, MstParamKeys.QS_PARAM_END_TIMESTAMP).Trim();
            string searchValue = GetFilterString(filter, MstParamKeys.QS_PARAM_VALUE).Trim();
            string sortField = GetFilterString(filter, MstParamKeys.QS_PARAM_SORT_FIELD).Trim();
            string sortDir = GetFilterString(filter, MstParamKeys.QS_PARAM_SORT_DIR).Trim();

            bool isDesc = string.IsNullOrWhiteSpace(sortField)
                ? true
                : IsSortDescending(sortDir);

            using SqlSugarClient db = new SqlSugarClient(configuration);

            int totalAll = await db.Queryable<AnalyticsData>().CountAsync();
            ISugarQueryable<AnalyticsData> query = db.Queryable<AnalyticsData>();

            if (!string.IsNullOrWhiteSpace(id))
                query = query.Where(e => e.Id == id);

            if (!string.IsNullOrWhiteSpace(userId))
                query = query.Where(e => e.UserId == userId);

            if (!string.IsNullOrWhiteSpace(eventKey))
                query = query.Where(e => e.Key == eventKey);

            if (DateTime.TryParse(timestampValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTime timestamp))
            {
                query = query.Where(e => e.Timestamp == timestamp);
            }

            if (DateTime.TryParse(startTimestampValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTime startTimestamp) &&
                DateTime.TryParse(endTimestampValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces |
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out DateTime endTimestamp))
            {
                query = query.Where(e => e.Timestamp >= startTimestamp && e.Timestamp <= endTimestamp);
            }

            if (!string.IsNullOrWhiteSpace(searchValue))
            {
                string[] searchTerms = searchValue
                    .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);

                foreach (string searchTerm in searchTerms)
                {
                    string term = searchTerm.Trim();

                    if (string.IsNullOrWhiteSpace(term))
                        continue;

                    query = query.Where(e =>
                        e.Id.Contains(term) ||
                        e.UserId.Contains(term) ||
                        e.Key.Contains(term) ||
                        e.Category.Contains(term));
                }
            }

            int filteredCount = await query.CountAsync();

            query = ApplySorting(query, sortField, isDesc);

            List<AnalyticsData> pageItems = await query
                .Skip(skip)
                .Take(size)
                .ToListAsync();

            return new DatabaseEntriesInfo<IAnalyticsInfoData>
            {
                entries = pageItems.Cast<IAnalyticsInfoData>().ToList(),
                total = totalAll,
                filtered = filteredCount
            };
        }

        public async Task<IEnumerable<IAnalyticsInfoData>> Get(int size = DefaultPageSize, int page = DefaultPage)
        {
            try
            {
                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    return await db.Queryable<AnalyticsData>()
                        .OrderBy(e => e.Timestamp)
                        .Skip(GetSkip(size, page))
                        .Take(GetTake(size))
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
                        .Where(e => e.Key == eventKey)
                        .OrderBy(e => e.Timestamp)
                        .Skip(GetSkip(size, page))
                        .Take(GetTake(size))
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
                        .Where(e => e.Timestamp == timestamp)
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
                        .Where(e => e.Timestamp >= timestampStart && e.Timestamp <= timestampEnd)
                        .OrderBy(e => e.Timestamp)
                        .Skip(GetSkip(size, page))
                        .Take(GetTake(size))
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
                        .Where(e => e.UserId == userId)
                        .OrderBy(e => e.Timestamp)
                        .Skip(GetSkip(size, page))
                        .Take(GetTake(size))
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                return null;
            }
        }

        public async Task<IEnumerable<IAnalyticsInfoData>> GetWithQuery(string query, int size = DefaultPageSize, int page = DefaultPage)
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
                        .Skip(GetSkip(size, page))
                        .Take(GetTake(size))
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
                        Id = string.IsNullOrWhiteSpace(ed.Id) ? Guid.NewGuid().ToString() : ed.Id,
                        UserId = ed.UserId ?? string.Empty,
                        Key = ed.Key ?? string.Empty,
                        Timestamp = ed.Timestamp,
                        Data = ed.Data ?? new Dictionary<string, string>(),
                        Category = ed.Category ?? string.Empty
                    };
                }).ToList();

                using (SqlSugarClient db = new SqlSugarClient(configuration))
                {
                    await db.Storageable(list)
                      .WhereColumns(new[] { "id" })
                      .ExecuteCommandAsync();
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
