using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.Diagnostics;

namespace MasterServerToolkit.MasterServer
{
    public class AccountInfoPacket : SerializablePacket
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Facebook { get; set; }
        public string Token { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsGuest { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public MstProperties Properties { get; private set; }

        public AccountInfoPacket() { }

        public AccountInfoPacket(IAccountInfoData account)
        {
            Id = account.Id ?? string.Empty;
            Username = account.Username ?? string.Empty;
            Email = account.Email ?? string.Empty;
            PhoneNumber = account.PhoneNumber ?? string.Empty;
            Facebook = account.Facebook ?? string.Empty;
            Token = account.Token ?? string.Empty;
            IsAdmin = account.IsAdmin;
            IsGuest = account.IsGuest;
            IsEmailConfirmed = account.IsEmailConfirmed;
            Properties = new MstProperties(account.Properties);
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Username);
            writer.Write(Email);
            writer.Write(PhoneNumber);
            writer.Write(Facebook);
            writer.Write(Token);
            writer.Write(IsAdmin);
            writer.Write(IsGuest);
            writer.Write(IsEmailConfirmed);
            writer.Write(Properties.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Username = reader.ReadString();
            Email = reader.ReadString();
            PhoneNumber = reader.ReadString();
            Facebook = reader.ReadString();
            Token = reader.ReadString();
            IsAdmin = reader.ReadBoolean();
            IsGuest = reader.ReadBoolean();
            IsEmailConfirmed = reader.ReadBoolean();
            Properties = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("Id", Id);
            options.Add("Username", Username);
            options.Add("Email", Email);
            options.Add("PhoneNumber", PhoneNumber);
            options.Add("Facebook", Facebook);
            options.Add("Token", Token);
            options.Add("IsAdmin", IsAdmin);
            options.Add("IsGuest", IsGuest);
            options.Add("IsEmailConfirmed", IsEmailConfirmed);
            options.Append(Properties);

            return options.ToReadableString(";\n");
        }
    }
}