using MasterServerToolkit.Networking;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class PingModule : BaseServerModule
    {
        [SerializeField, TextArea(3, 5)]
        private string pongMessage = "Hello, Pong!";

        public override void Initialize(IServer server)
        {
            server.RegisterMessageHandler(MstOpCodes.Ping, OnPingRequestListener);
        }

        private void OnPingRequestListener(IIncomingMessage message)
        {
            message.Respond(pongMessage, ResponseStatus.Success);
        }

        public override JObject JsonInfo()
        {
            var data = base.JsonInfo();
            data["description"] = $"This is just a ping testing module that sends a response message \"{pongMessage}\" to any client who has made a request.";
            return data;
        }

        public override MstProperties Info()
        {
            var properties = base.Info();
            properties.Set("Description", $"This is just a ping testing module that sends a response message \"<b>{pongMessage}</b>\" to any client who has made a request.");
            return properties;
        }
    }
}