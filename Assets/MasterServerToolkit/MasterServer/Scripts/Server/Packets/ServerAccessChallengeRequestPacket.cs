using MasterServerToolkit.Networking;
using System;
using System.Text;

namespace MasterServerToolkit.MasterServer
{
    public class ServerAccessChallengeRequestPacket : SerializablePacket
    {
        public const int MaxPermissionKeyByteCount = 64;

        public string PermissionKey { get; set; } = string.Empty;

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            if (Encoding.UTF8.GetByteCount(PermissionKey ?? string.Empty) > MaxPermissionKeyByteCount)
                throw new ArgumentException($"Permission key cannot exceed {MaxPermissionKeyByteCount} UTF-8 bytes", nameof(PermissionKey));

            writer.Write(PermissionKey ?? string.Empty);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            PermissionKey = reader.ReadString(MaxPermissionKeyByteCount);
        }
    }
}
