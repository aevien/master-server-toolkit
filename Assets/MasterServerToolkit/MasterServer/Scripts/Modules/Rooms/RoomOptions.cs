using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    /// <summary>
    /// List of options, which are sent to master server during registration
    /// </summary>
    public class RoomOptions : SerializablePacket
    {
        /// <summary>
        /// Name of the room
        /// </summary>
        public string Name { get; set; } = "Unnamed";

        /// <summary>
        /// IP of the machine on which the room was created
        /// (Only used in the <see cref="RoomController.DefaultAccessProvider"/>)
        /// </summary>
        public string RoomIp { get; set; } = string.Empty;

        /// <summary>
        /// Port, required to access the room 
        /// (Only used in the <see cref="RoomController.DefaultAccessProvider"/>)
        /// </summary>
        public ushort RoomPort { get; set; } = 0;

        /// <summary>
        /// If true, room will appear in public listings
        /// </summary>
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// The max number of players allowed. If 0 - player number is not limited
        /// </summary>
        public ushort MaxPlayers { get; set; } = 0;

        /// <summary>
        /// Room password
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Number of seconds, after which unconfirmed (pending) accesses will removed
        /// to allow new players. Make sure it's long enought to allow player to load gameplay scene
        /// </summary>
        public float AccessTimeoutPeriod { get; set; } = 10;

        /// <summary>
        /// Region, to which the spawned room belongs
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Extra properties that you might want to send to master server
        /// </summary>
        public MstProperties ExtraParameters { get; set; }

        public RoomOptions()
        {
            ExtraParameters = new MstProperties();
        }

        /// <summary>
        /// Creates an independent copy of these room options.
        /// </summary>
        /// <returns>A room options copy with its own extra-parameters collection.</returns>
        public virtual RoomOptions Clone()
        {
            var clone = (RoomOptions)MemberwiseClone();
            clone.ExtraParameters = new MstProperties(ExtraParameters);
            return clone;
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Name ?? string.Empty);
            writer.Write(RoomIp ?? string.Empty);
            writer.Write(RoomPort);
            writer.Write(IsPublic);
            writer.Write(MaxPlayers);
            writer.Write(Password ?? string.Empty);
            writer.Write(AccessTimeoutPeriod);
            writer.Write(Region ?? string.Empty);
            writer.Write((ExtraParameters ?? new MstProperties()).ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Name = reader.ReadString();
            RoomIp = reader.ReadString();
            RoomPort = reader.ReadUInt16();
            IsPublic = reader.ReadBoolean();
            MaxPlayers = reader.ReadUInt16();
            Password = reader.ReadString();
            AccessTimeoutPeriod = reader.ReadSingle();
            Region = reader.ReadString();
            ExtraParameters = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("RoomName", Name);
            options.Add("RoomIp", RoomIp);
            options.Add("RoomPort", RoomPort);
            options.Add("IsPublic", IsPublic);
            options.Add("MaxPlayers", MaxPlayers <= 0 ? "Unlimited" : MaxPlayers.ToString());
            options.Add("Use Password", !string.IsNullOrEmpty(Password));
            options.Add("AccessTimeoutPeriod", $"{AccessTimeoutPeriod} sec.");
            options.Add("Region", string.IsNullOrEmpty(Region) ? "International" : Region);
            if (ExtraParameters != null)
                options.Append(ExtraParameters);

            return options.ToReadableString();
        }
    }
}
