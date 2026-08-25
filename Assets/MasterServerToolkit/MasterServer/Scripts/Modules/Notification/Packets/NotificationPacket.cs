using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class NotificationPacket : SerializablePacket
    {
        public int RoomId { get; set; } = -1;
        public string Message { get; set; } = string.Empty;
        public List<int> Recipients { get; set; } = new List<int>();
        public List<int> IgnoreRecipients { get; set; } = new List<int>();

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            RoomId = reader.ReadInt32();
            Message = reader.ReadString();

            int recipientsCount = reader.ReadCount32(
                MstNetworkLimits.MaxCollectionEntryCount,
                "Notification recipient list");

            for (int i = 0; i < recipientsCount; i++)
            {
                Recipients.Add(reader.ReadInt32());
            }

            int ignoreRecipientsCount = reader.ReadCount32(
                MstNetworkLimits.MaxCollectionEntryCount,
                "Notification ignored recipient list");

            for (int i = 0; i < ignoreRecipientsCount; i++)
            {
                IgnoreRecipients.Add(reader.ReadInt32());
            }
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(RoomId);
            writer.Write(Message);

            writer.WriteCount32(
                Recipients.Count,
                MstNetworkLimits.MaxCollectionEntryCount,
                "Notification recipient list");

            foreach (var recipient in Recipients)
            {
                writer.Write(recipient);
            }

            writer.WriteCount32(
                IgnoreRecipients.Count,
                MstNetworkLimits.MaxCollectionEntryCount,
                "Notification ignored recipient list");

            foreach (var recipient in IgnoreRecipients)
            {
                writer.Write(recipient);
            }
        }
    }
}
