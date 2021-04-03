using MasterServerToolkit.Json;
using System;

namespace MasterServerToolkit.MasterServer
{
    [Serializable]
    public class WsControllerMessage
    {
        public long AckId { get; set; } = -1;
        public string OpCode { get; set; }
        public JSONObject Data { get; set; }
        public string Error { get; set; }

        public static WsControllerMessage FromJson(string json)
        {
            var jobject = new JSONObject(json);

            var msg = new WsControllerMessage
            {
                AckId = jobject.HasField("ackId") ? jobject["ackId"].Int : -1,
                OpCode = jobject.HasField("opcode") ? jobject["opcode"].String : string.Empty,
                Data = jobject.HasField("data") ? jobject["data"] : JSONObject.Null
            };

            return msg;
        }

        public JSONObject ToJson()
        {
            var jobject = new JSONObject(JSONObject.Type.OBJECT);
            jobject.AddField("ackId", AckId);
            jobject.AddField("opcode", OpCode);
            jobject.AddField("data", Data);
            jobject.AddField("error", Error);
            return jobject;
        }

        public override string ToString()
        {
            return ToJson().Print();
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