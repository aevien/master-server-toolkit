using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// A lobby chat message 
    /// </summary>
    public class LobbyChatPacket : SerializablePacket
    {
        public string Sender { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsError { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Sender);
            writer.Write(Message);
            writer.Write(IsError);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Sender = reader.ReadString();
            Message = reader.ReadString();
            IsError = reader.ReadBoolean();
        }
    }
}