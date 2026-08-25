using MasterServerToolkit.Json;
using MasterServerToolkit.Networking;
using System.Threading.Tasks;
using UnityEngine;

namespace MasterServerToolkit.MasterServer
{
    public class PingModule : BaseServerModule
    {
        #region INSPECTOR

        [SerializeField, TextArea(3, 5), Tooltip("Text returned by the Ping request handler. Keep it short because every ping response sends the complete string.")]
        private string pongMessage = "Hello, Pong!";

        #endregion

        /// <summary>
        /// 
        /// </summary>
        public string PongMessage
        {
            get => pongMessage;
            set => pongMessage = value;
        }

        public override void Initialize(IServer server)
        {
            server.RegisterMessageHandler(MstOpCodes.Ping, OnPingRequestListener);
        }

        private Task OnPingRequestListener(IIncomingMessage message)
        {
            message.Respond(pongMessage, ResponseStatus.Success);
            return Task.CompletedTask;
        }

        public override MstJson Details()
        {
            var info = base.Details();
            info.SetField("description", $"This is just a ping testing module that sends a response message \"{pongMessage}\" to any client who has made a request.");
            return info;
        }
    }
}
