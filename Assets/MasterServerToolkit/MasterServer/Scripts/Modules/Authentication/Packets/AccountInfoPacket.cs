using Aevien.Utilities;
using MasterServerToolkit.Networking;
using System;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class AccountInfoPacket : SerializablePacket
    {
        public string Username { get; private set; }
        public string Email { get; private set; }
        public string Token { get; private set; }
        public bool IsAdmin { get; private set; }
        public bool IsGuest { get; private set; }
        public bool IsEmailConfirmed { get; private set; }
        public Dictionary<string, string> Properties { get; private set; }

        public AccountInfoPacket() { }

        public AccountInfoPacket(IAccountInfoData account)
        {
            Username = account.Username;
            Email = account.Email;
            Token = account.Token;
            IsAdmin = account.IsAdmin;
            IsGuest = account.IsGuest;
            IsEmailConfirmed = account.IsEmailConfirmed;
            Properties = account.Properties;
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Username);
            writer.Write(Email);
            writer.Write(Token);
            writer.Write(IsAdmin);
            writer.Write(IsGuest);
            writer.Write(IsEmailConfirmed);
            writer.Write(Properties);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Username = reader.ReadString();
            Email = reader.ReadString();
            Token = reader.ReadString();
            IsAdmin = reader.ReadBoolean();
            IsGuest = reader.ReadBoolean();
            IsEmailConfirmed = reader.ReadBoolean();
            Properties = reader.ReadDictionary();
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("Username", Username);
            options.Add("Email", Email);
            options.Add("Token", Token);
            options.Add("IsAdmin", IsAdmin);
            options.Add("IsGuest", IsGuest);
            options.Add("IsEmailConfirmed", IsEmailConfirmed);
            options.Append(Properties);

            return options.ToReadableString();
        }
    }
}