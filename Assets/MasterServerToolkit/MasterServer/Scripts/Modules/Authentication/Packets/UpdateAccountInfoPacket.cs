using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class UpdateAccountInfoPacket : SerializablePacket
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Facebook { get; set; }
        public MstProperties Properties { get; set; }

        public UpdateAccountInfoPacket()
        {
            Id = string.Empty;
            Username = string.Empty;
            Password = string.Empty;
            Email = string.Empty;
            PhoneNumber = string.Empty;
            Facebook = string.Empty;
            Properties = new MstProperties(Properties);
        }

        public UpdateAccountInfoPacket(IAccountInfoData account)
        {
            Id = account.Id;
            Username = account.Username ?? string.Empty;
            Password = account.Password ?? string.Empty;
            Email = account.Email ?? string.Empty;
            PhoneNumber = account.PhoneNumber ?? string.Empty;
            Facebook = account.Facebook ?? string.Empty;
            Properties = new MstProperties(account.Properties);
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Username);
            writer.Write(Password);
            writer.Write(Email);
            writer.Write(PhoneNumber);
            writer.Write(Facebook);
            writer.Write(Properties.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Username = reader.ReadString();
            Password = reader.ReadString();
            Email = reader.ReadString();
            PhoneNumber = reader.ReadString();
            Facebook = reader.ReadString();
            Properties = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("Id", Id);
            options.Add("Username", Username);
            options.Add("Password", "**********");
            options.Add("Email", Email);
            options.Add("PhoneNumber", PhoneNumber);
            options.Add("Facebook", Facebook);
            options.Append(Properties);

            return options.ToReadableString(";\n");
        }
    }
}
