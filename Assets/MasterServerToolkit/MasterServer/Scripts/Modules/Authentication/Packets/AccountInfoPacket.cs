using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.MasterServer
{
    public class AccountInfoPacket : SerializablePacket
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public DateTime LastLogin { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public string Token { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsGuest { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public bool IsBanned { get; set; }
        public MstProperties ExtraProperties { get; private set; }

        public AccountInfoPacket() { }

        public AccountInfoPacket(IAccountInfoData account)
        {
            Id = account.Id ?? string.Empty;
            Username = account.Username ?? string.Empty;
            Email = account.Email ?? string.Empty;
            LastLogin = account.LastLogin;
            Created = account.Created;
            Updated = account.Updated;
            Token = account.Token ?? string.Empty;
            IsAdmin = account.IsAdmin;
            IsGuest = account.IsGuest;
            IsEmailConfirmed = account.IsEmailConfirmed;
            IsBanned = account.IsBanned;
            ExtraProperties = new MstProperties(account.ExtraProperties);
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Username);
            writer.Write(Email);
            writer.Write(LastLogin);
            writer.Write(Created);
            writer.Write(Updated);
            writer.Write(Token);
            writer.Write(IsAdmin);
            writer.Write(IsGuest);
            writer.Write(IsEmailConfirmed);
            writer.Write(IsBanned);
            writer.Write(ExtraProperties.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Username = reader.ReadString();
            Email = reader.ReadString();
            LastLogin = reader.ReadDateTime();
            Created = reader.ReadDateTime();
            Updated = reader.ReadDateTime();
            Token = reader.ReadString();
            IsAdmin = reader.ReadBoolean();
            IsGuest = reader.ReadBoolean();
            IsEmailConfirmed = reader.ReadBoolean();
            IsBanned = reader.ReadBoolean();
            ExtraProperties = new MstProperties(reader.ReadDictionary());
        }

        public override MstJson ToJson()
        {
            var json = new MstJson();
            json.AddField("id", Id);
            json.AddField("username", Username);
            json.AddField("email", Email);
            json.AddField("lastLogin", LastLogin.ToString());
            json.AddField("created", Created.ToString());
            json.AddField("updated", Updated.ToString());
            json.AddField("token", Token);
            json.AddField("isAdmin", IsAdmin);
            json.AddField("isGuest", IsGuest);
            json.AddField("isEmailConfirmed", IsEmailConfirmed);
            json.AddField("isBanned", IsBanned);
            json.AddField("extraProperties", new MstJson(ExtraProperties.ToDictionary()));

            return json;
        }

        public override string ToString()
        {
            return ToJson().Print(true);
        }
    }
}