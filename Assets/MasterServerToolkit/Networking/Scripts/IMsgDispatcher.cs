namespace MasterServerToolkit.Networking
{
    public interface IMsgDispatcher<TPeer> where TPeer : IPeer
    {
        /// <summary>
        /// Peer, to which we have connected
        /// </summary>
        TPeer Peer { get; }

        void SendMessage(short opCode);
        void SendMessage(short opCode, ISerializablePacket packet);
        void SendMessage(short opCode, ISerializablePacket packet, DeliveryMethod method);
        void SendMessage(short opCode, ISerializablePacket packet, ResponseCallback responseCallback);
        void SendMessage(short opCode, ISerializablePacket packet, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(short opCode, ResponseCallback responseCallback);

        void SendMessage(short opCode, byte[] data);
        void SendMessage(short opCode, byte[] data, ResponseCallback responseCallback);
        void SendMessage(short opCode, byte[] data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(short opCode, string data);
        void SendMessage(short opCode, string data, ResponseCallback responseCallback);
        void SendMessage(short opCode, string data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(short opCode, int data);
        void SendMessage(short opCode, int data, ResponseCallback responseCallback);
        void SendMessage(short opCode, int data, ResponseCallback responseCallback, int timeoutSecs);

        void SendMessage(IMessage message);
        void SendMessage(IMessage message, ResponseCallback responseCallback);
        void SendMessage(IMessage message, ResponseCallback responseCallback, int timeoutSecs);
    }
}