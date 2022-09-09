using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using MasterServerToolkit.Utils;

namespace MasterServerToolkit.Examples.BasicNetworking
{
    public class CustomClient : SingletonBehaviour<CustomClient>
    {
        // Start is called before the first frame update
        void Start()
        {
            Mst.Client.Connection.AddConnectionOpenListener(Connection_OnConnectedEvent);
            Mst.Client.Connection.AddConnectionCloseListener(Connection_OnDisconnectedEvent);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            Mst.Client.Connection.RemoveConnectionOpenListener(Connection_OnConnectedEvent);
            Mst.Client.Connection.RemoveConnectionCloseListener(Connection_OnDisconnectedEvent);
        }

        /// <summary>
        /// Sends message to server
        /// </summary>
        public void SendNetMessage()
        {
            string message = "Hello from client";
            Mst.Client.Connection.SendMessage(MessageCodes.Message, message);
        }

        /// <summary>
        /// Sends message to server and waiting for response
        /// </summary>
        public void SendNetMessageWithResponse()
        {
            string text = "Hello from client and waiting for response from server";
            Mst.Client.Connection.SendMessage(MessageCodes.MessageWithResponse, text, (status, message) =>
            {
                if (status == ResponseStatus.Error)
                {
                    Logs.Error(message.AsString());
                    return;
                }

                Logs.Info($"Client received response message from server: {message.AsString()}");
            });
        }

        /// <summary>
        /// Connection callback handler
        /// </summary>
        private void Connection_OnConnectedEvent(IClientSocket client)
        {
            Logs.Info("Now the client is ready to send messages to server and receive responses from it");
        }

        /// <summary>
        /// Disconnection callback
        /// </summary>
        private void Connection_OnDisconnectedEvent(IClientSocket client)
        {
            Logs.Info("Client disconnected from server");
        }
    }
}