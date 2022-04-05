namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Generic packet handler
    /// </summary>
    public class PacketHandler : IPacketHandler
    {
        private event IncommingMessageHandler Handler;

        public PacketHandler(ushort opCode, IncommingMessageHandler handler)
        {
            OpCode = opCode;
            Handler += handler;
        }

        public ushort OpCode { get; }

        public void Handle(IIncomingMessage message)
        {
            Handler?.Invoke(message);
        }
    }
}