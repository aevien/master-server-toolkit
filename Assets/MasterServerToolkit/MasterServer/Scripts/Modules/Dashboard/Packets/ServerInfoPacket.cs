using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class ServerInfoPacket : SerializablePacket
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        [JsonProperty("initializedModules")]
        public byte InitializedModules { get; set; }
        [JsonProperty("unitializedModules")]
        public byte UnitializedModules { get; set; }
        [JsonProperty("activeClients")]
        public ushort ActiveClients { get; set; }
        [JsonProperty("inactiveClients")]
        public ushort InactiveClients { get; set; }
        [JsonProperty("totalClients")]
        public int TotalClients { get; set; }
        [JsonProperty("highestClients")]
        public ushort HighestClients { get; set; }
        [JsonProperty("updatebles")]
        public short Updatebles { get; set; }
        [JsonProperty("useAuth")]
        public bool UseAuth { get; set; }
        [JsonProperty("peersAccepted")]
        public int PeersAccepted { get; set; }
        [JsonProperty("peersRejected")]
        public int PeersRejected { get; set; }
        [JsonProperty("useSecure")]
        public bool UseSecure { get; set; }
        [JsonProperty("certificatePath")]
        public string CertificatePath { get; set; } = string.Empty;
        [JsonProperty("certificatePassword")]
        public string CertificatePassword { get; set; } = string.Empty;
        [JsonProperty("applicationKey")]
        public string ApplicationKey { get; set; } = string.Empty;
        [JsonProperty("localIp")]
        public string LocalIp { get; set; } = string.Empty;
        [JsonProperty("publicIp")]
        public string PublicIp { get; set; } = string.Empty;
        [JsonProperty("port")]
        public string Port { get; set; } = string.Empty;
        [JsonProperty("incomingTraffic")]
        public long IncomingTraffic { get; set; }
        [JsonProperty("outgoingTraffic")]
        public long OutgoingTraffic { get; set; }
        [JsonProperty("error")]
        public string Error { get; set; } = string.Empty;

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            InitializedModules = reader.ReadByte();
            UnitializedModules = reader.ReadByte();
            ActiveClients = reader.ReadUInt16();
            InactiveClients = reader.ReadUInt16();
            TotalClients = reader.ReadInt32();
            HighestClients = reader.ReadUInt16();
            Updatebles = reader.ReadInt16();
            UseAuth = reader.ReadBoolean();
            PeersAccepted = reader.ReadInt32();
            PeersRejected = reader.ReadInt32();
            UseSecure = reader.ReadBoolean();
            CertificatePath = reader.ReadString();
            CertificatePassword = reader.ReadString();
            ApplicationKey = reader.ReadString();
            LocalIp = reader.ReadString();
            PublicIp = reader.ReadString();
            Port = reader.ReadString();
            IncomingTraffic = reader.ReadInt64();
            OutgoingTraffic = reader.ReadInt64();
            Error = reader.ReadString();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(InitializedModules);
            writer.Write(UnitializedModules);
            writer.Write(ActiveClients);
            writer.Write(InactiveClients);
            writer.Write(TotalClients);
            writer.Write(HighestClients);
            writer.Write(Updatebles);
            writer.Write(UseAuth);
            writer.Write(PeersAccepted);
            writer.Write(PeersRejected);
            writer.Write(UseSecure);
            writer.Write(CertificatePath);
            writer.Write(CertificatePassword);
            writer.Write(ApplicationKey);
            writer.Write(LocalIp);
            writer.Write(PublicIp);
            writer.Write(Port);
            writer.Write(IncomingTraffic);
            writer.Write(OutgoingTraffic);
            writer.Write(Error);
        }

        public JObject ToJObject()
        {
            return JObject.FromObject(this);
        }

        public static ServerInfoPacket FromJobject(JObject json)
        {
            return json.ToObject<ServerInfoPacket>();
        }
    }
}