using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.Accounts)]
    public class AccountInfoData : IAccountInfoData
    {
        [SugarColumn(ColumnName = "id", ColumnDataType = "varchar(38)", IsPrimaryKey = true)]
        public string Id { get; set; }
        [SugarColumn(ColumnName = "username", ColumnDataType = "varchar(45)", IsPrimaryKey = true)]
        public string Username { get; set; }
        [SugarColumn(ColumnName = "password", ColumnDataType = "varchar(128)", IsNullable = true)]
        public string Password { get; set; }
        [SugarColumn(ColumnName = "email", ColumnDataType = "varchar(45)", IsNullable = true)]
        public string Email { get; set; }
        [SugarColumn(ColumnName = "token", ColumnDataType = "varchar(128)", IsNullable = true)]
        public string Token { get; set; }
        [SugarColumn(ColumnName = "last_login", ColumnDataType = "datetime", IsNullable = true)]
        public DateTime LastLogin { get; set; }
        [SugarColumn(ColumnName = "created", ColumnDataType = "datetime", IsNullable = true)]
        public DateTime Created { get; set; }
        [SugarColumn(ColumnName = "updated", ColumnDataType = "datetime", IsNullable = true)]
        public DateTime Updated { get; set; }
        [SugarColumn(ColumnName = "is_admin", ColumnDataType = "tinyint(1)")]
        public bool IsAdmin { get; set; }
        [SugarColumn(ColumnName = "is_guest", ColumnDataType = "tinyint(1)")]
        public bool IsGuest { get; set; }
        [SugarColumn(ColumnName = "is_email_confirmed", ColumnDataType = "tinyint(1)")]
        public bool IsEmailConfirmed { get; set; }
        [SugarColumn(ColumnName = "is_banned", ColumnDataType = "tinyint(1)")]
        public bool IsBanned { get; set; }
        [SugarColumn(ColumnName = "device_id", ColumnDataType = "varchar(45)")]
        public string DeviceId { get; set; }
        [SugarColumn(ColumnName = "device_name", ColumnDataType = "varchar(45)")]
        public string DeviceName { get; set; }
        [SugarColumn(IsIgnore = true)]
        public Dictionary<string, string> ExtraProperties { get; set; }

        public event Action<IAccountInfoData> OnChangedEvent;

        public AccountInfoData()
        {
            Id = Mst.Helper.CreateGuidString();
            Username = string.Empty;
            Password = string.Empty;
            Email = string.Empty;
            Token = string.Empty;
            IsAdmin = false;
            IsGuest = true;
            IsEmailConfirmed = false;
            IsBanned = false;
            LastLogin = DateTime.UtcNow;
            Created = DateTime.UtcNow;
            Updated = DateTime.UtcNow;
            ExtraProperties = new Dictionary<string, string>();
        }

        public void MarkAsDirty()
        {
            OnChangedEvent?.Invoke(this);
        }
    }
}
