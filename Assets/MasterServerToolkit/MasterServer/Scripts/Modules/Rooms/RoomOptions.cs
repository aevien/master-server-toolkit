using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.Text;

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
        public int RoomPort { get; set; } = -1;

        /// <summary>
        /// If true, room will appear in public listings
        /// </summary>
        public bool IsPublic { get; set; } = true;

        /// <summary>
        /// If 0 - player number is not limited
        /// </summary>
        public int MaxConnections { get; set; } = 0;

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
        /// If set to false, users will no longer be able to request access directly.
        /// This is useful when you want players to get accesses through other means, for example
        /// through Lobby module,
        /// </summary>
        public bool AllowUsersRequestAccess { get; set; } = true;

        /// <summary>
        /// Region, to which the spawned room belongs
        /// </summary>
        public string Region { get; set; } = string.Empty;

        /// <summary>
        /// Extra properties that you might want to send to master server
        /// </summary>
        public MstProperties CustomOptions { get; set; }

        public RoomOptions()
        {
            CustomOptions = new MstProperties();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Name);
            writer.Write(RoomIp);
            writer.Write(RoomPort);
            writer.Write(IsPublic);
            writer.Write(MaxConnections);
            writer.Write(Password);
            writer.Write(AccessTimeoutPeriod);
            writer.Write(AllowUsersRequestAccess);
            writer.Write(Region);
            writer.Write(CustomOptions.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Name = reader.ReadString();
            RoomIp = reader.ReadString();
            RoomPort = reader.ReadInt32();
            IsPublic = reader.ReadBoolean();
            MaxConnections = reader.ReadInt32();
            Password = reader.ReadString();
            AccessTimeoutPeriod = reader.ReadSingle();
            AllowUsersRequestAccess = reader.ReadBoolean();
            Region = reader.ReadString();
            CustomOptions = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("RoomName", Name);
            options.Add("RoomIp", RoomIp);
            options.Add("RoomPort", RoomPort);
            options.Add("MaxConnections", MaxConnections <= 0 ? "Unlimited" : MaxConnections.ToString());
            options.Add("Use Password", !string.IsNullOrEmpty(Password));
            options.Add("AccessTimeoutPeriod", $"{AccessTimeoutPeriod} sec.");
            options.Add("AllowUsersRequestAccess", AllowUsersRequestAccess);
            options.Add("Region", string.IsNullOrEmpty(Region) ? "International" : Region);
            options.Append(CustomOptions);

            return options.ToReadableString();
        }
    }
}