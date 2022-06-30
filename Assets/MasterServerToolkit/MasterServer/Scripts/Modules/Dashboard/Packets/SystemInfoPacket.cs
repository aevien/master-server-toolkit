using MasterServerToolkit.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MasterServerToolkit.MasterServer
{
    public class SystemInfoPacket : SerializablePacket
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;
        [JsonProperty("deviceId")]
        public string DeviceId { get; set; } = string.Empty;
        [JsonProperty("deviceModel")]
        public string DeviceModel { get; set; } = string.Empty;
        [JsonProperty("deviceName")]
        public string DeviceName { get; set; } = string.Empty;
        [JsonProperty("deviceType")]
        public string DeviceType { get; set; } = string.Empty;
        [JsonProperty("graphicsDeviceId")]
        public string GraphicsDeviceId { get; set; } = string.Empty;
        [JsonProperty("graphicsDeviceName")]
        public string GraphicsDeviceName { get; set; } = string.Empty;
        [JsonProperty("graphicsDeviceVersion")]
        public string GraphicsDeviceVersion { get; set; } = string.Empty;
        [JsonProperty("graphicsDeviceType")]
        public string GraphicsDeviceType { get; set; } = string.Empty;
        [JsonProperty("graphicsDeviceVendorId")]
        public string GraphicsDeviceVendorId { get; set; } = string.Empty;
        [JsonProperty("graphicsDeviceVendor")]
        public string GraphicsDeviceVendor { get; set; } = string.Empty;
        [JsonProperty("graphicsDeviceMemory")]
        public int GraphicsDeviceMemory { get; set; }
        [JsonProperty("os")]
        public string Os { get; set; } = string.Empty;
        [JsonProperty("osFamily")]
        public string OsFamily { get; set; } = string.Empty;
        [JsonProperty("cpuType")]
        public string CpuType { get; set; } = string.Empty;
        [JsonProperty("cpuFrequency")]
        public int CpuFrequency { get; set; }
        [JsonProperty("cpuCount")]
        public int CpuCount { get; set; }
        [JsonProperty("ram")]
        public int Ram { get; set; }
        [JsonProperty("error")]
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

        public JObject ToJObject()
        {
            return JObject.FromObject(this);
        }

        public static SystemInfoPacket FromJobject(JObject json)
        {
            return json.ToObject<SystemInfoPacket>();
        }
    }
}