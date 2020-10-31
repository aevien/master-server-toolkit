using MasterServerToolkit.Networking;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MasterServerToolkit.MasterServer
{
    public class ClientsSpawnRequestPacket : SerializablePacket
    {
        public MstProperties Options { get; set; }
        public MstProperties CustomOptions { get; set; }

        public ClientsSpawnRequestPacket()
        {
            Options = new MstProperties();
            CustomOptions = new MstProperties();
        }

        public override void ToBinaryWriter(EndianBinaryWriter writer)
        {
            writer.Write(Options.ToDictionary());
            writer.WriteDictionary(CustomOptions.ToDictionary());
        }

        public override void FromBinaryReader(EndianBinaryReader reader)
        {
            Options = new MstProperties(reader.ReadDictionary());
            CustomOptions = new MstProperties(reader.ReadDictionary());
        }

        public override string ToString()
        {
            var options = new MstProperties(Options);

            if (options.IsValueEmpty(MstDictKeys.ROOM_REGION))
            {
                options.Set(MstDictKeys.ROOM_REGION, "International");
            }

            return options.ToReadableString() + " " + CustomOptions.ToReadableString();
        }
    }
}