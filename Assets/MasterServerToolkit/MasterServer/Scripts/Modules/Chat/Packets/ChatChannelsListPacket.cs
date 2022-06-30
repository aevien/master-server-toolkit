using MasterServerToolkit.Networking;
using System.Collections.Generic;

namespace MasterServerToolkit.MasterServer
{
    public class ChatChannelsListPacket : SerializablePacket
    {
        public List<ChatChannelInfo> Channels { get; set; }

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
            writer.Write((ushort)Channels.Count);

            foreach (var channel in Channels)
            {
                writer.Write(channel.Name);
                writer.Write(channel.OnlineCount);
            }
        }
    }
}