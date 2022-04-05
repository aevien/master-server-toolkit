namespace MasterServerToolkit.Networking
{
    public interface IMsgDispatcher
    {
        /// <summary>
        /// Peer, to which we have connected
        /// </summary>
        IPeer Peer { get; }

        void SendMessage(ushort opCode);
        void SendMessage(ushort opCode, ISerializablePacket packet);
        void SendMessage(ushort opCode, ISerializablePacket packet, DeliveryMethod method);
        void SendMessage(ushort opCode, ISerializablePacket packet, ResponseCallback responseCallback);
        void SendMessage(ushort opCode, ISerializablePacket packet, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(ushort opCode, ResponseCallback responseCallback);

        void SendMessage(ushort opCode, byte[] data);
        void SendMessage(ushort opCode, byte[] data, ResponseCallback responseCallback);
        void SendMessage(ushort opCode, byte[] data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(ushort opCode, string data);
        void SendMessage(ushort opCode, string data, ResponseCallback responseCallback);
        void SendMessage(ushort opCode, string data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(ushort opCode, int data);
        void SendMessage(ushort opCode, int data, ResponseCallback responseCallback);
        void SendMessage(ushort opCode, int data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(IOutgoingMessage message);
        void SendMessage(IOutgoingMessage message, ResponseCallback responseCallback);
        void SendMessage(IOutgoingMessage message, ResponseCallback responseCallback, int timeoutSecs);
    }
}