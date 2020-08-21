#if MIRROR
using MasterServerToolkit.Networking;
using Mirror;

namespace MasterServerToolkit.Bridges.Mirror
{
    public class ValidateRoomAccessResultMessage : IMessageBase
    {
        public string Error { get; set; }
        public ResponseStatus Status { get; set; }

        public void Deserialize(NetworkReader reader)
        {
            Error = reader.ReadString();
            Status = (ResponseStatus)reader.ReadUInt16();
        }

        public void Serialize(NetworkWriter writer)
        {
            writer.WriteString(Error);
            writer.WriteUInt16((ushort)Status);
        }
    }
}
#endif