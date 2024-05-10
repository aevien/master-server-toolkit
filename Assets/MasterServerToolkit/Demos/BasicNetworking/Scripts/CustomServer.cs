using MasterServerToolkit.Extensions;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;

namespace MasterServerToolkit.Examples.BasicNetworking
{
    public class CustomServer : ServerBehaviour
    {
        protected override void Start()
        {
            base.Start();

            // Start the server on next frame
            MstTimer.WaitForEndOfFrame(() =>
            {
                StartServer();
            });
        }

        protected override void OnStartedServer()
        {
            base.OnStartedServer();

            RegisterMessageHandler(MessageCodes.Message, OnMessageReceivedHandler);
            RegisterMessageHandler(MessageCodes.MessageWithResponse, OnMessageWithResponseReceivedHandler);
        }

        private void OnMessageWithResponseReceivedHandler(IIncomingMessage message)
        {
            Logs.Info($"Server received messge from client: {message.AsString()}. Sending response to client");
            message.Respond("Hello from server!");
        }

        private void OnMessageReceivedHandler(IIncomingMessage message)
        {
            Logs.Info($"Server received messge from client: {message.AsString()}");
        }
    }

    public struct MessageCodes
    {
        public static ushort Message = nameof(Message).ToUint16Hash();
        public static ushort MessageWithResponse = nameof(MessageWithResponse).ToUint16Hash();
    }
}