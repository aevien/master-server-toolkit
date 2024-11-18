using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public interface IAnalyticsData
	{
        public string Id { get; set; }
        public string EventId { get; set; }
        public string UserId { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, string> Data { get; set; }
    }
}