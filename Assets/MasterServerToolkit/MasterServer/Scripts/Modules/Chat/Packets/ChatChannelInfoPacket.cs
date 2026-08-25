using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ChatChannelInfoPacket : SerializablePacket
    {
        public string Name { get; set; } = string.Empty;
        public int OnlineCount { get; set; }
        public ChatChannelType Type { get; set; } = ChatChannelType.Public;
        public bool IsClosed { get; set; }
        public bool IsArchived { get; set; }
        public string OwnerUsername { get; set; } = string.Empty;
        public bool IsInvited { get; set; }
        public bool IsBanned { get; set; }
        public ChatChannelPermission CurrentUserPermissions { get; set; } = ChatChannelPermission.None;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(OnlineCount);
            writer.Write((int)Type);
            writer.Write(IsClosed);
            writer.Write(IsArchived);
            writer.Write(OwnerUsername);
            writer.Write(IsInvited);
            writer.Write(IsBanned);
            writer.Write((int)CurrentUserPermissions);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Name = reader.ReadString();
            OnlineCount = reader.ReadInt32();
            Type = (ChatChannelType)reader.ReadInt32();
            IsClosed = reader.ReadBoolean();
            IsArchived = reader.ReadBoolean();
            OwnerUsername = reader.ReadString();
            IsInvited = reader.ReadBoolean();
            IsBanned = reader.ReadBoolean();
            CurrentUserPermissions = (ChatChannelPermission)reader.ReadInt32();
        }
    }
}
