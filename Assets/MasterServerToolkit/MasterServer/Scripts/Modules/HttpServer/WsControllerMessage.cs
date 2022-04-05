using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class WsControllerMessage
    {
        [JsonProperty("ackId")]
        public long AckId { get; set; } = -1;
        [JsonProperty("opcode")]
        public string OpCode { get; set; }
        [JsonProperty("data")]
        public JObject Data { get; set; }
        [JsonProperty("error")]
        public string Error { get; set; }

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
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