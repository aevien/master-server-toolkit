using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IAnalyticsDatabaseAccessor : IDatabaseAccessor
    {
        Task Insert(IEnumerable<IAnalyticsInfoData> eventsData);
        Task<IEnumerable<IAnalyticsInfoData>> Get(int size, int page);
        Task<IAnalyticsInfoData> GetById(string id);
        Task<IEnumerable<IAnalyticsInfoData>> GetByKey(string eventId, int size, int page);
        Task<IEnumerable<IAnalyticsInfoData>> GetByUserId(string userId, int size, int page);
        Task<IEnumerable<IAnalyticsInfoData>> GetByTimestamp(DateTime timestamp);
        Task<IEnumerable<IAnalyticsInfoData>> GetByTimestampRange(DateTime timestampStart, DateTime timestampEnd, int size, int page);
        Task<IEnumerable<IAnalyticsInfoData>> GetWithQuery(string query, int size, int page);
    }
}