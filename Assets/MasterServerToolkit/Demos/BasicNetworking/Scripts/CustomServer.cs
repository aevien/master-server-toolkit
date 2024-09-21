using MasterServerToolkit.Extensions;
using MasterServerToolkit.Logging;
using MasterServerToolkit.MasterServer;
using MasterServerToolkit.Networking;
using System.Threading.Tasks;

namespace MasterServerToolkit.Demos.BasicNetworking
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

        private async Task OnMessageWithResponseReceivedHandler(IIncomingMessage message)
        {
            Logs.Info($"Server received messge from client: {message.AsString()}. Sending response to client");
            message.Respond("Hello from server!");
            await Task.CompletedTask;
        }

        private async Task OnMessageReceivedHandler(IIncomingMessage message)
        {
            Logs.Info($"Server received messge from client: {message.AsString()}");
            await Task.CompletedTask;
        }
    }

    public struct MessageCodes
    {
        public static ushort Message = nameof(Message).ToUint16Hash();
        public static ushort MessageWithResponse = nameof(MessageWithResponse).ToUint16Hash();
    }
}