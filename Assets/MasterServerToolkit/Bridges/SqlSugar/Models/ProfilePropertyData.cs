using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using SqlSugar;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.Profiles)]
    public class ProfilePropertyData : IProfilePropertyData
    {
        [SugarColumn(ColumnName = "account_id", Length = 38, IsPrimaryKey = true, IsNullable = false)]
        public string AccountId { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "property_key", Length = 64, IsPrimaryKey = true, IsNullable = false)]
        public string PropertyKey { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "property_value", ColumnDataType = "text", IsNullable = false)]
        public string PropertyValue { get; set; } = string.Empty;

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.AddField(nameof(AccountId), AccountId);
            json.AddField(nameof(PropertyKey), PropertyKey);
            json.AddField(nameof(PropertyValue), PropertyValue);
            return json;
        }
    }
}
