using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Generic packet handler
    /// </summary>
    public class AsyncPacketHandler : IAsyncPacketHandler
    {
        private readonly CancellableAsyncIncomingMessageHandler handler;

        public AsyncPacketHandler(ushort opCode, AsyncIncommingMessageHandler handler)
        {
            OpCode = opCode;

            if (handler == null)
                return;

            this.handler = async (message, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                await handler.Invoke(message);
                cancellationToken.ThrowIfCancellationRequested();
            };
        }

        public AsyncPacketHandler(ushort opCode, CancellableAsyncIncomingMessageHandler handler)
        {
            OpCode = opCode;
            this.handler = handler;
        }

        public ushort OpCode { get; }

        public async Task HandleAsync(IIncomingMessage message)
        {
            await HandleAsync(message, CancellationToken.None);
        }

        public async Task HandleAsync(IIncomingMessage message, CancellationToken cancellationToken)
        {
            if (handler == null)
                return;

            cancellationToken.ThrowIfCancellationRequested();
            await handler.Invoke(message, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
