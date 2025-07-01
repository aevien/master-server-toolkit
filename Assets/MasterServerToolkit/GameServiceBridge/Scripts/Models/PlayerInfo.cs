using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System;

namespace MasterServerToolkit.GameService
{
    public class PlayerInfo : SerializablePacket
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = "John Doe";
        public string Avatar { get; set; } = "https://i.pravatar.cc/300";
        public bool IsGuest { get; set; } = true;
        public MstJson Extra { get; set; } = MstJson.EmptyObject;

        public PlayerInfo()
        {
            Id = Guid.NewGuid().ToString();
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            Name = reader.ReadString();
            Avatar = reader.ReadString();
            IsGuest = reader.ReadBoolean();
            Extra = reader.ReadJson();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(Name);
            writer.Write(Avatar);
            writer.Write(IsGuest);
            writer.Write(Extra);
        }

        public override MstJson ToJson()
        {
            var json = base.ToJson();
            json.AddField("id", Id);
            json.AddField("name", Name);
            json.AddField("avatar", Avatar);
            json.AddField("extra", Extra);
            return json;
        }
    }
}