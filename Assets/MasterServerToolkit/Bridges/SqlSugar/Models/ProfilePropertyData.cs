using SqlSugar;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.Profiles)]
    public class ProfilePropertyData
    {
        [SugarColumn(ColumnName = "account_id", ColumnDataType = "varchar(38)", IsPrimaryKey = true)]
        public string AccountId { get; set; } = string.Empty;
        [SugarColumn(ColumnName = "property_key", ColumnDataType = "varchar(38)", IsPrimaryKey = true)]
        public string PropertyKey { get; set; } = string.Empty;
        [SugarColumn(ColumnName = "property_value", ColumnDataType = "json")]
        public string PropertyValue { get; set; } = string.Empty;
    }
}