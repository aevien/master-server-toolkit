using System.Threading.Tasks;
using System.Threading;

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

        /// <summary>
        /// Asynchronously handles the message within the current server run.
        /// </summary>
        /// <param name="message">Incoming message.</param>
        /// <param name="cancellationToken">Token canceled when the owning server run stops.</param>
        Task HandleAsync(IIncomingMessage message, CancellationToken cancellationToken);
    }
}
