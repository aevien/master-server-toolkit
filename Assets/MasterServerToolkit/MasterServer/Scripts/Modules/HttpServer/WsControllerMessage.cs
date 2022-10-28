using MasterServerToolkit.Json;
using System;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class WsControllerMessage
    {
        public long AckId { get; set; } = -1;
        public string OpCode { get; set; }
        public MstJson Data { get; set; }
        public string Error { get; set; }

        public override string ToString()
        {
            return MstJson.EmptyObject.ToString();
        }

        public bool HasOpCode()
        {
            return !string.IsNullOrEmpty(OpCode);
        }

        public bool HasData()
        {
            return Data != null;
        }

        public bool HasError()
        {
            return !string.IsNullOrEmpty(Error);
        }
    }
}