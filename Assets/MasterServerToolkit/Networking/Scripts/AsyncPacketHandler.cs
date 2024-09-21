using System.Collections.Generic;
using System.Threading.Tasks;

namespace MasterServerToolkit.Networking
{
    /// <summary>
    /// Generic packet handler
    /// </summary>
    public class AsyncPacketHandler : IAsyncPacketHandler
    {
        private AsyncIncommingMessageHandler handler;

        public AsyncPacketHandler(ushort opCode, AsyncIncommingMessageHandler handler)
        {
            OpCode = opCode;
            this.handler += handler;
        }

        public ushort OpCode { get; }

        public async Task HandleAsync(IIncomingMessage message)
        {
            if (handler != null)
            {
                await handler.Invoke(message);
            }
        }
    }
}