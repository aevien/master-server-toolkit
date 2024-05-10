using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class SystemInfoPacket : SerializablePacket
    {
        public string Id { get; set; } = string.Empty;
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceModel { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public string DeviceType { get; set; } = string.Empty;
        public string GraphicsDeviceId { get; set; } = string.Empty;
        public string GraphicsDeviceName { get; set; } = string.Empty;
        public string GraphicsDeviceVersion { get; set; } = string.Empty;
        public string GraphicsDeviceType { get; set; } = string.Empty;
        public string GraphicsDeviceVendorId { get; set; } = string.Empty;
        public string GraphicsDeviceVendor { get; set; } = string.Empty;
        public int GraphicsDeviceMemory { get; set; }
        public string Os { get; set; } = string.Empty;
        public string OsFamily { get; set; } = string.Empty;
        public string CpuType { get; set; } = string.Empty;
        public int CpuFrequency { get; set; }
        public int CpuCount { get; set; }
        public int Ram { get; set; }
        public string Error { get; set; } = string.Empty;

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Id = reader.ReadString();
            DeviceId = reader.ReadString();
            DeviceModel = reader.ReadString();
            DeviceName = reader.ReadString();
            DeviceType = reader.ReadString();
            GraphicsDeviceId = reader.ReadString();
            GraphicsDeviceName = reader.ReadString();
            GraphicsDeviceVersion = reader.ReadString();
            GraphicsDeviceType = reader.ReadString();
            GraphicsDeviceVendorId = reader.ReadString();
            GraphicsDeviceVendor = reader.ReadString();
            GraphicsDeviceMemory = reader.ReadInt32();
            Os = reader.ReadString();
            OsFamily = reader.ReadString();
            CpuType = reader.ReadString();
            CpuFrequency = reader.ReadInt32();
            CpuCount = reader.ReadInt32();
            Ram = reader.ReadInt32();
            Error = reader.ReadString();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Id);
            writer.Write(DeviceId);
            writer.Write(DeviceModel);
            writer.Write(DeviceName);
            writer.Write(DeviceType);
            writer.Write(GraphicsDeviceId);
            writer.Write(GraphicsDeviceName);
            writer.Write(GraphicsDeviceVersion);
            writer.Write(GraphicsDeviceType);
            writer.Write(GraphicsDeviceVendorId);
            writer.Write(GraphicsDeviceVendor);
            writer.Write(GraphicsDeviceMemory);
            writer.Write(Os);
            writer.Write(OsFamily);
            writer.Write(CpuType);
            writer.Write(CpuFrequency);
            writer.Write(CpuCount);
            writer.Write(Ram);
            writer.Write(Error);
        }
    }
}