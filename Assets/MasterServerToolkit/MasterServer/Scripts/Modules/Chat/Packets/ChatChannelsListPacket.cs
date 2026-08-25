using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ChatChannelsListPacket : SerializablePacket
    {
        public List<ChatChannelInfo> Channels { get; set; } = new List<ChatChannelInfo>();

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Channels = new List<ChatChannelInfo>();

            int count = reader.ReadUInt16();

            for (int i = 0; i < count; i++)
            {
                var channel = new ChatChannelInfo()
                {
                    Name = reader.ReadString(),
                    OnlineCount = reader.ReadInt32()
                };

                Channels.Add(channel);
            }
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            if (Channels == null)
            {
                Channels = new List<ChatChannelInfo>();
            }

            if (Channels.Count > ushort.MaxValue)
            {
                throw new System.InvalidOperationException($"Chat channels list cannot contain more than {ushort.MaxValue} channels");
            }

            writer.Write((ushort)Channels.Count);

            foreach (var channel in Channels)
            {
                writer.Write(channel.Name);
                writer.Write(channel.OnlineCount);
            }
        }
    }
}
