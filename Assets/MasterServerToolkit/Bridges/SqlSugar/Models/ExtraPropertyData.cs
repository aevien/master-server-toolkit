using SqlSugar;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.ExtraProperties)]
    public class ExtraPropertyData
    {
        [SugarColumn(ColumnName = "account_id", ColumnDataType = "varchar(38)", IsPrimaryKey = true)]
        public string AccountId { get; set; }
        [SugarColumn(ColumnName = "property_key", ColumnDataType = "varchar(38)", IsPrimaryKey = true)]
        public string PropertyKey { get; set; }
        [SugarColumn(ColumnName = "property_value", ColumnDataType = "varchar(512)")]
        public string PropertyValue { get; set; }
    }
}
