using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.Analytics)]
    public class AnalyticsData : IAnalyticsInfoData
    {
        [SugarColumn(ColumnName = "id", ColumnDataType = "varchar(38)", IsPrimaryKey = true)]
        public string Id { get; set; }
        [SugarColumn(ColumnName = "user_id", ColumnDataType = "varchar(38)")]
        public string UserId { get; set; }
        [SugarColumn(ColumnName = "name", ColumnDataType = "varchar(16)", IsNullable = false)]
        public string Key { get; set; }
        [SugarColumn(ColumnName = "category", ColumnDataType = "varchar(64)", IsNullable = false)]
        public string Category { get; set; }
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

        public MstJson ToJson()
        {
            var json = MstJson.Create();
            json.AddField("id", Id);
            json.AddField("user_id", UserId);
            json.AddField("key", Key);
            json.AddField("category", Category);
            json.AddField("timestamp", Timestamp);
            json.AddField("data", MstJson.Create(Data));
            return json;
        }
    }
}