namespace MasterServerToolkit.Networking
{
    public class PacketHandler : IPacketHandler
    {
        private IncommingMessageHandler handler;

        public PacketHandler(ushort opCode, IncommingMessageHandler handler)
        {
            OpCode = opCode;
            this.handler += handler;
        }

        public ushort OpCode { get; }

        public void Handle(IIncomingMessage message)
        {
            if (handler != null)
            {
                handler.Invoke(message);
            }
        }
    }
}
