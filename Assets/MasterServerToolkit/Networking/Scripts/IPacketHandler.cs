namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Represents an object that can handle packets
    /// </summary>
    public interface IPacketHandler
    {
        /// <summary>
        /// Operation code of the message to be handled
        /// </summary>
        ushort OpCode { get; }

        /// <summary>
        /// Handling of the message
        /// </summary>
        /// <param name="message"></param>
        void Handle(IIncomingMessage message);
    }
}