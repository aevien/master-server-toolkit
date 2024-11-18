using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IAnalyticsDatabaseAccessor : IDatabaseAccessor
    {
        Task Insert(IEnumerable<AnalyticsDataInfoPacket> eventsData);
        Task<IEnumerable<IAnalyticsData>> GetAll();
        Task<IAnalyticsData> GetById(string eventId);
        Task<IEnumerable<IAnalyticsData>> GetByUserId(string userId);
        Task<IEnumerable<IAnalyticsData>> GetWithQuery(string query);
    }
}