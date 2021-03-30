using MasterServerToolkit.Logging;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace MasterServerToolkit.Networking
{
    public class EchoService : WebSocketBehavior
    {
        protected override void OnMessage(MessageEventArgs e)
        {
            Logs.Debug($"Message size: {e.RawData.LongLength / 1024f}kb. The number of concurrent clients is: {Sessions.Count}");
            Send(e.Data);
        }
    }
}
