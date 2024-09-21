using System.Threading.Tasks;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Represents an object that can handle packets
    /// </summary>
    public interface IAsyncPacketHandler
    {
        /// <summary>
        /// Operation code of the message to be handled
        /// </summary>
        ushort OpCode { get; }

        /// <summary>
        /// Asynchronous handling of the message
        /// </summary>
        /// <param name="message"></param>
        Task HandleAsync(IIncomingMessage message);
    }
}