using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.MasterServer
{
    public class ObservableQuests : ObservableBaseList<ObservableQuests>
    {
        public ObservableQuests(ushort key) : base(key)
        {
        }

        public override void Deserialize(string value)
        {
            throw new System.NotImplementedException();
        }

        public override void FromJson(MstJson json)
        {
            throw new System.NotImplementedException();
        }

        public override void FromJson(string json)
        {
            throw new System.NotImplementedException();
        }

        public override string Serialize()
        {
            throw new System.NotImplementedException();
        }

        public override MstJson ToJson()
        {
            throw new System.NotImplementedException();
        }

        protected override ObservableQuests ReadValue(EndianBinaryReader reader)
        {
            throw new System.NotImplementedException();
        }

        protected override void WriteValue(ObservableQuests value, EndianBinaryWriter writer)
        {
            throw new System.NotImplementedException();
        }
    }
}