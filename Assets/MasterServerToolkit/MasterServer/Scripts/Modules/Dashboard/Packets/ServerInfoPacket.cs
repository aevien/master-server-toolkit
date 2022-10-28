using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ServerInfoPacket : SerializablePacket
    {
        public string Id { get; set; } = string.Empty;
        public byte InitializedModules { get; set; }
        public byte UnitializedModules { get; set; }
        public ushort ActiveClients { get; set; }
        public ushort InactiveClients { get; set; }
        public int TotalClients { get; set; }
        public ushort HighestClients { get; set; }
        public short Updatebles { get; set; }
        public bool UseAuth { get; set; }
        public int PeersAccepted { get; set; }
        public int PeersRejected { get; set; }
        public bool UseSecure { get; set; }
        public string CertificatePath { get; set; } = string.Empty;
        public string CertificatePassword { get; set; } = string.Empty;
        public string ApplicationKey { get; set; } = string.Empty;
        public string LocalIp { get; set; } = string.Empty;
        public string PublicIp { get; set; } = string.Empty;
        public string Port { get; set; } = string.Empty;
        public long IncomingTraffic { get; set; }
        public long OutgoingTraffic { get; set; }
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

        public MstJson ToJson()
        {
            return MstJson.EmptyObject;
        }

        public static ServerInfoPacket FromJobject(MstJson json)
        {
            return new ServerInfoPacket();
        }
    }
}