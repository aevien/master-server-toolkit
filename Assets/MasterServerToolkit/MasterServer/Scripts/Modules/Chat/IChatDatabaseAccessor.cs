using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MasterServerToolkit.MasterServer
{
    public interface IChatDatabaseAccessor : IDatabaseAccessor
    {
        Task SaveMessageAsync(ChatMessageInfo message, CancellationToken cancellationToken = default);

        Task<List<ChatMessageInfo>> GetMessagesAsync(ChatMessageType messageType, string receiver, int limit,
            DateTime? beforeUtc = null, CancellationToken cancellationToken = default);

        Task<List<ChatMessageInfo>> GetPrivateMessagesAsync(string userA, string userB, int limit,
            DateTime? beforeUtc = null, CancellationToken cancellationToken = default);
    }
}
