using SqlSugar;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.PasswordResetCodes)]
    public class PasswordResetData
    {
        [SugarColumn(ColumnName = "email", ColumnDataType = "varchar(45)", IsPrimaryKey = true)]
        public string Email { get; set; }
        [SugarColumn(ColumnName = "code", ColumnDataType = "varchar(128)")]
        public string Code { get; set; }
        [SugarColumn(ColumnName = "expires_at", IsNullable = true)]
        public DateTime? ExpiresAt { get; set; }
        [SugarColumn(ColumnName = "attempts_left", IsNullable = true)]
        public int? AttemptsLeft { get; set; }
    }
}
