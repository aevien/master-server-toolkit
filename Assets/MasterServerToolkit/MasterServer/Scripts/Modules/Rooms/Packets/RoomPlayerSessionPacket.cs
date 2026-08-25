using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// Identifies one exact account session currently owned by a room process.
    /// </summary>
    public class RoomPlayerSessionPacket : SerializablePacket
    {
        /// <summary>
        /// Gets or sets the stable MST account identifier.
        /// </summary>
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the peer identifier of the master session being released.
        /// </summary>
        public int MasterPeerId { get; set; }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(AccountId ?? string.Empty);
            writer.Write(MasterPeerId);
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            AccountId = reader.ReadString();
            MasterPeerId = reader.ReadInt32();
        }
    }
}
