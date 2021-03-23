using MasterServerToolkit.Logging;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    public class EchoService : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            //float size = e.RawData.LongLength / 1024f;
            //string name = SimpleNameGenerator.Generate(Gender.Male);
            //string response = $"Hi! My name is {name}. Thank you for your message. Your massage size is {size}kb";

            //Logs.Debug($"Message size: {size}kb. The number of concurrent clients is: {Sessions.Count}");

            Logs.Debug($"Message size: {e.RawData.LongLength}. The number of concurrent clients is: {Sessions.Count}");

            Send(e.Data);
        }
    }
}
