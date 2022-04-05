namespace MasterServerToolkit.Networking
{
    /// <summary>
    ///     Represents basic functionality of message factory
    /// </summary>
    public interface IMessageFactory
    {
        /// <summary>
        /// Creates an empty message
        /// </summary>
        /// <param name="opCode"></param>
        /// <returns></returns>
        IOutgoingMessage Create(ushort opCode);

        /// <summary>
        ///     Creates a message filled with data
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        IOutgoingMessage Create(ushort opCode, byte[] data);

        /// <summary>
        ///     Reconstructs message bytes into an incomming message
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="peer"></param>
        /// <returns></returns>
        IIncomingMessage FromBytes(byte[] buffer, int start, IPeer peer);
    }
}