using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.Analytics)]
    public class AnalyticsData : IAnalyticsData
    {
        [SugarColumn(ColumnName = "id", ColumnDataType = "varchar(38)", IsPrimaryKey = true)]
        public string Id { get; set; }
        [SugarColumn(ColumnName = "user_id", ColumnDataType = "varchar(38)")]
        public string UserId { get; set; }
        [SugarColumn(ColumnName = "event_id", ColumnDataType = "varchar(16)", IsNullable = false)]
        public string EventId { get; set; }
        [SugarColumn(ColumnName = "timestamp", ColumnDataType = "datetime", IsNullable = false)]
        public DateTime Timestamp { get; set; }
        [SugarColumn(ColumnName = "data", ColumnDataType = "json", IsJson = true)]
        public Dictionary<string, string> Data { get; set; }

        public AnalyticsData()
        {
            Id = Guid.NewGuid().ToString();
            Data = new Dictionary<string, string>();
            Timestamp = DateTime.UtcNow;
        }
    }
}