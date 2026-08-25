using MasterServerToolkit.Json;
using MasterServerToolkit.MasterServer;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.Bridges.SqlSugar
{
    [SugarTable(TablesMapping.Accounts)]
    public class AccountInfoData : IAccountInfoData, IEquatable<AccountInfoData>
    {
        [SugarColumn(ColumnName = "id", Length = 38, IsPrimaryKey = true)]
        public string Id { get; set; }
        [SugarColumn(ColumnName = "username", Length = 64, IsPrimaryKey = true)]
        public string Username { get; set; }
        [SugarColumn(ColumnName = "password", Length = 128, IsNullable = true)]
        public string Password { get; set; }
        [SugarColumn(ColumnName = "email", Length = 64, IsNullable = true)]
        public string Email { get; set; }
        [SugarColumn(ColumnName = "token", Length = 512, IsNullable = true)]
        public string Token { get; set; }
        [SugarColumn(ColumnName = "last_login", IsNullable = true)]
        public DateTime LastLoginAt { get; set; }
        [SugarColumn(ColumnName = "created", IsNullable = false)]
        public DateTime CreatedAt { get; set; }
        [SugarColumn(ColumnName = "updated", IsNullable = false)]
        public DateTime UpdatedAt { get; set; }
        [SugarColumn(ColumnName = "is_admin")]
        public bool IsAdmin { get; set; }
        [SugarColumn(ColumnName = "is_guest")]
        public bool IsGuest { get; set; }
        [SugarColumn(ColumnName = "is_email_confirmed")]
        public bool IsEmailConfirmed { get; set; }
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
            LastLoginAt = DateTime.UtcNow;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
            ExtraProperties = new Dictionary<string, string>()
            {
                { MstParamKeys.EXTRA_PHONE_NUMBER, string.Empty },
                { MstParamKeys.EXTRA_GUEST_RESTORE_CODE, string.Empty }
            };
        }

        public AccountInfoData(IAccountInfoData data)
        {
            Id = data.Id;
            Username = data.Username;
            Password = data.Password;
            Email = data.Email;
            Token = data.Token;
            IsAdmin = data.IsAdmin;
            IsGuest = data.IsGuest;
            IsEmailConfirmed = data.IsEmailConfirmed; 
            LastLoginAt = data.LastLoginAt;
            CreatedAt = data.CreatedAt;
            UpdatedAt = data.UpdatedAt;
            ExtraProperties = data.ExtraProperties ?? new Dictionary<string, string>();
        }

        public void MarkAsDirty()
        {
            OnChangedEvent?.Invoke(this);
        }

        public bool Equals(AccountInfoData other)
        {
            return Id == other.Id;
        }

        public override int GetHashCode()
        {
            return Id.GetHashCode();
        }

        public MstJson ToJson()
        {
            var json = MstJson.CreateObject();
            json.AddField("id", Id);
            json.AddField("username", Username);
            json.AddField("email", Email);
            json.AddField("lastLoginAt", LastLoginAt);
            json.AddField("createdAt", CreatedAt);
            json.AddField("updatedAt", UpdatedAt);
            json.AddField("isAdmin", IsAdmin);
            json.AddField("isGuest", IsGuest);
            json.AddField("isEmailConfirmed", IsEmailConfirmed);
            json.AddField("extras", MstJson.Create(ExtraProperties));
            return json;
        }
    }
}
