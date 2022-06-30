namespace MasterServerToolkit.MasterServer
{
    public delegate void WsControllerMessageEventHandler(WsControllerMessage message, WsControllerService service);

    public class WsControllerMessageHandler
    {
        private event WsControllerMessageEventHandler OnMessage;

        public void SetHandler(WsControllerMessageEventHandler handler)
        {
            OnMessage += handler;
        }

        public void Handle(WsControllerMessage message, WsControllerService service)
        {
            OnMessage?.Invoke(message, service);
        }
    }
}