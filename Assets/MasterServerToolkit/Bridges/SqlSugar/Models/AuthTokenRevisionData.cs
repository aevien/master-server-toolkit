using SqlSugar;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.AuthTokenRevisions)]
    public class AuthTokenRevisionData
    {
        [SugarColumn(ColumnName = "account_id", Length = 38, IsPrimaryKey = true)]
        public string AccountId { get; set; }

        [SugarColumn(ColumnName = "revision")]
        public int Revision { get; set; }
    }
}
