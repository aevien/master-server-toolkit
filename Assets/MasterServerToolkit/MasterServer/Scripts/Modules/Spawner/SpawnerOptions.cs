using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class SpawnerOptions : SerializablePacket
    {
        /// <summary>
        /// Public IP address of the machine, on which the spawner is running
        /// </summary>
        public string MachineIp { get; set; } = "xxx.xxx.xxx.xxx";

        /// <summary>
        /// Max number of processes that this spawner can handle. If 0 - unlimited
        /// </summary>
        public int MaxProcesses { get; set; } = 0;

        /// <summary>
        /// Region, to which the spawner belongs
        /// </summary>
        public string Region { get; set; } = "International";

        /// <summary>
        /// Spawner properties
        /// </summary>
        public MstProperties CustomOptions { get; set; }

        public SpawnerOptions()
        {
            CustomOptions = new MstProperties();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(MachineIp);
            writer.Write(MaxProcesses);
            writer.Write(Region);
            writer.Write(CustomOptions.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            MachineIp = reader.ReadString();
            MaxProcesses = reader.ReadInt32();
            Region = reader.ReadString();
            CustomOptions = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            var options = new MstProperties();
            options.Add("MachineIp", MachineIp);
            options.Add("MaxProcesses", MaxProcesses);
            options.Add("Region", string.IsNullOrEmpty(Region) ? "International" : Region);
            options.Append(CustomOptions);

            return options.ToReadableString();
        }
    }
}