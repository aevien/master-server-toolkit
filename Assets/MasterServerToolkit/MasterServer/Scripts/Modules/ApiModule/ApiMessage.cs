using MasterServerToolkit.Json;

namespace MasterServerToolkit.MasterServer
{
    public class ApiMessage
    {
        public ApiService Peer { get; private set; }
        public string OpCode { get; private set; }
        public MstJson Data { get; private set; }
        public string Error { get; private set; }
        public string Token { get; private set; }

        public ApiMessage(string opcode, MstJson data, string error, string token, ApiService peer)
        {
            OpCode = opcode;
            Data = data;
            Error = error;
            Peer = peer;
            Token = token;
        }

        public override string ToString()
        {
            var json = MstJson.EmptyObject;
            json.AddField("peerId", Peer.ID);
            json.AddField("opcode", OpCode);
            json.AddField("token", Token);
            json.AddField("data", Data);
            return json.ToString();
        }

        public void Authorize()
        {
            Token = Peer.CreateToken();
        }

        public bool IsAuthorized()
        {
            return Peer.IsAuthorized(Token);
        }

        public void ResponseOk()
        {
            ResponseOk(MstJson.EmptyObject);
        }

        public void ResponseOk(string data)
        {
            var json = MstJson.Create(data);
            ResponseOk(json);
        }

        public void ResponseOk(MstJson data)
        {
            Peer.SendMessage(new ApiMessage(OpCode, data, "", Token, null));
        }

        public void ResponseError(string error)
        {
            Peer.SendMessage(new ApiMessage(OpCode, null, error, Token, null));
        }

        public void ResponseUnauthorized()
        {
            Peer.SendMessage(new ApiMessage(OpCode, null, "Unauthorized", Token, null));
        }
    }
}