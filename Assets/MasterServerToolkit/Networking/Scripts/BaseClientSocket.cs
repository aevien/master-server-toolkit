namespace MasterServerToolkit.Networking
{
    public class BaseClientSocket : IMsgDispatcher
    {
        public IPeer Peer { get; set; }

        public void SendMessage(IOutgoingMessage message)
        {
            SendMessage(message, DeliveryMethod.Reliable);
        }

        public void SendMessage(IOutgoingMessage message, DeliveryMethod method)
        {
            Peer.SendMessage(message, method);
        }

        public int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback)
        {
            return Peer.SendMessage(message, responseCallback);
        }

        public int SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            return Peer.SendMessage(message, responseCallback, timeoutSecs);
        }
    }
}
