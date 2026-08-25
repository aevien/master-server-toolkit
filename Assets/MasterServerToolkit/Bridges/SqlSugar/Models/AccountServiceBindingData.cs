using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.AccountServiceBindings)]
    [SugarIndex("ux_account_service_binding_account_service",
        nameof(IAccountServiceBindingData.AccountId), OrderByType.Asc,
        nameof(IAccountServiceBindingData.ServiceId), OrderByType.Asc, true)]
    [SugarIndex("ux_account_service_binding_service_player",
        nameof(IAccountServiceBindingData.ServiceId), OrderByType.Asc,
        nameof(IAccountServiceBindingData.PlayerId), OrderByType.Asc, true)]
    public class AccountServiceBindingData : IAccountServiceBindingData
    {
        [SugarColumn(ColumnName = "id", Length = 256, IsPrimaryKey = true)]
        public string Id { get; set; }

        [SugarColumn(ColumnName = "account_id", Length = 38)]
        public string AccountId { get; set; }

        [SugarColumn(ColumnName = "service_id", Length = 64)]
        public string ServiceId { get; set; }

        [SugarColumn(ColumnName = "player_id", Length = 128)]
        public string PlayerId { get; set; }

        [SugarColumn(ColumnName = "player_name", Length = 128, IsNullable = true)]
        public string PlayerName { get; set; }

        [SugarColumn(ColumnName = "is_guest")]
        public bool IsGuest { get; set; }

        [SugarColumn(ColumnName = "created_at")]
        public DateTime CreatedAt { get; set; }

        [SugarColumn(ColumnName = "updated_at")]
        public DateTime UpdatedAt { get; set; }

        [SugarColumn(ColumnName = "last_login_at")]
        public DateTime LastLoginAt { get; set; }

        public AccountServiceBindingData() { }

        public AccountServiceBindingData(IAccountServiceBindingData data)
        {
            Id = data.Id;
            AccountId = data.AccountId;
            ServiceId = data.ServiceId;
            PlayerId = data.PlayerId;
            PlayerName = data.PlayerName;
            IsGuest = data.IsGuest;
            CreatedAt = data.CreatedAt;
            UpdatedAt = data.UpdatedAt;
            LastLoginAt = data.LastLoginAt;
        }
    }
}
