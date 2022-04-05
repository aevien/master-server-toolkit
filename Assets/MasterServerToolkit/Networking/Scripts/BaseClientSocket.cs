namespace MasterServerToolkit.Networking
{
    public class BaseClientSocket : IMsgDispatcher
    {
        public IPeer Peer { get; set; }

        public void SendMessage(ushort opCode)
        {
            var msg = MessageHelper.Create(opCode);
            SendMessage(msg);
        }

        public void SendMessage(ushort opCode, ISerializablePacket packet)
        {
            SendMessage(opCode, packet, DeliveryMethod.Reliable);
        }

        public void SendMessage(ushort opCode, ISerializablePacket packet, DeliveryMethod method)
        {
            var msg = MessageHelper.Create(opCode, packet.ToBytes());
            Peer.SendMessage(msg, method);
        }

        public void SendMessage(ushort opCode, ISerializablePacket packet, ResponseCallback responseCallback)
        {
            var msg = MessageHelper.Create(opCode, packet.ToBytes());
            Peer.SendMessage(msg, responseCallback);
        }

        public void SendMessage(ushort opCode, ISerializablePacket packet, ResponseCallback responseCallback, int timeoutSecs)
        {
            var msg = MessageHelper.Create(opCode, packet.ToBytes());
            Peer.SendMessage(msg, responseCallback, timeoutSecs);
        }

        public void SendMessage(ushort opCode, ResponseCallback responseCallback)
        {
            var msg = MessageHelper.Create(opCode);
            SendMessage(msg, responseCallback);
        }

        public void SendMessage(ushort opCode, byte[] data)
        {
            SendMessage(opCode, data, DeliveryMethod.Reliable);
        }

        public void SendMessage(ushort opCode, byte[] data, DeliveryMethod method)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, method);
        }

        public void SendMessage(ushort opCode, byte[] data, ResponseCallback responseCallback)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, responseCallback);
        }

        public void SendMessage(ushort opCode, byte[] data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, responseCallback, timeoutSecs);
        }

        public void SendMessage(ushort opCode, string data)
        {
            SendMessage(opCode, data, DeliveryMethod.Reliable);
        }

        public void SendMessage(ushort opCode, string data, DeliveryMethod method)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, method);
        }

        public void SendMessage(ushort opCode, string data, ResponseCallback responseCallback)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, responseCallback);
        }

        public void SendMessage(ushort opCode, string data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, responseCallback, timeoutSecs);
        }

        public void SendMessage(ushort opCode, int data)
        {
            SendMessage(opCode, data, DeliveryMethod.Reliable);
        }

        public void SendMessage(ushort opCode, int data, DeliveryMethod method)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, method);
        }

        public void SendMessage(ushort opCode, int data, ResponseCallback responseCallback)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, responseCallback);
        }

        public void SendMessage(ushort opCode, int data, ResponseCallback responseCallback, int timeoutSecs)
        {
            var msg = MessageHelper.Create(opCode, data);
            Peer.SendMessage(msg, responseCallback, timeoutSecs);
        }

        public void SendMessage(IOutgoingMessage message)
        {
            SendMessage(message, DeliveryMethod.Reliable);
        }

        public void SendMessage(IOutgoingMessage message, DeliveryMethod method)
        {
            Peer.SendMessage(message, method);
        }

        public void SendMessage(IOutgoingMessage message, ResponseCallback responseCallback)
        {
            Peer.SendMessage(message, responseCallback);
        }

        public void SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs)
        {
            Peer.SendMessage(message, responseCallback, timeoutSecs);
        }
    }
}